# Plano de Multi-Tenancy (Opção B)

Plano arquitetural para suportar **5-20 clientes** no mesmo deployment, cada um com banco isolado.

## Resumo da decisão

- **Modelo**: 1 instalação Docker, **N bancos SQLite**, 1 banco por tenant.
- **Identificação**: subdomain do request (`acme.connectveiculos.dev.br` → tenant slug `acme`).
- **Isolamento**: físico no banco (`data/{slug}.db`), lógico via middleware no request.
- **Onboarding**: script `criar-tenant.sh` (dev cria; self-service fica para v2).

## Por que assim

Vide discussão completa na conversa que originou esse plano. Resumo:

| Alternativa | Por que descartada |
|---|---|
| Tudo num banco (TenantId em ~20 tabelas) | Refator gigante; bug = vazamento entre clientes (LGPD); backup/restore por cliente complicado |
| 1 VM por cliente (multi-instance) | Mais barato em código (zero), mas Free Tier Oracle limita a 1-2 VMs por conta; provisionar leva 30-45 min vs ~2 min |

Ficamos com banco-por-tenant: **isolamento físico** + **deploy único** + **script de onboarding rápido**.

## Inventário de impacto (descoberta)

### Persistência (dois mecanismos coexistem hoje)

1. **EF Core** — `ConnectVeiculosDbContext` com 28 `DbSet<>` (Usuarios, Lojas, Veiculos, ...).
   - Hoje: registrado via `services.AddDbContext` com connection string fixa.
   - Vai virar: factory tenant-aware, monta connection string dinâmica `Data Source=/app/data/{slug}.db`.

2. **Dapper** — `DbSession` cria `IDbConnection` para 3 Operations:
   - `RecuperacaoSenhaOperations`
   - `UsuarioOperations`
   - `VeiculoOperations`
   - Hoje: connection string injetada por DI direto.
   - Vai virar: factory que olha o tenant context atual.

### Schema setup

- Hoje: `app.UseInitializeDatabase()` no startup chama `dbContext.Database.EnsureCreated()`. Sem migrations EF formais.
- Multi-tenant: vai precisar iterar pelos tenants registrados no master e rodar `EnsureCreated` em cada um. Lock/serialização para evitar race no primeiro boot.

### Identidade (Auth)

- JWT atual: `NameIdentifier`, `Email`, `Name`, `Jti`, opcionalmente `Role`. Sem `TenantId`.
- Vai adicionar claim `TenantId` (e `TenantSlug`) — preenchido pelo `LoginUseCase` baseado no tenant do request.
- Login passa a ser tenant-isolated naturalmente: o subdomain já entrega o tenant; busca de usuário por email é dentro do banco do tenant.

### Multi-loja (já parcial)

Entities com `LojId` hoje: Lojas, LojaUsuario, Veiculo, Lead, Negociacao, TestDrive (e descendentes via FK).

Após o multi-tenant, a hierarquia fica:

```
Tenant (banco)
├─ Loja A
│  └─ Veiculos, Vendas, Leads, Negociacoes, TestDrives
└─ Loja B
   └─ ...
```

Nada do código de multi-loja precisa mudar — ele já filtra por loja dentro do banco. Multi-tenant adiciona uma camada acima.

## Componentes a criar

### Banco "master" (registry de tenants)

- Arquivo: `data/_master.db` (underscore para ordenar primeiro e diferenciar de tenants).
- Tabela `Tenants`:
  - `Id INTEGER PRIMARY KEY`
  - `Slug TEXT UNIQUE NOT NULL` (ex: `acme`, `default`)
  - `Nome TEXT NOT NULL` (ex: `Acme Veículos`)
  - `Status TEXT` (`active` / `suspended`)
  - `DatabaseFile TEXT` (default: `{slug}.db`)
  - `DataCriacao DATETIME`

### `ITenantContext` (scoped DI)

```csharp
public interface ITenantContext {
    int TenantId { get; }
    string TenantSlug { get; }
    string ConnectionString { get; }
    bool IsResolved { get; }
}
```

### `TenantResolutionMiddleware`

- Lê `Host` header → extrai subdomain → busca tenant no master.
- Tenant "default" responde no domínio raiz `connectveiculos.dev.br` (sem subdomain) e no `www.`.
- Subdomain inválido / tenant inexistente → 404 com mensagem clara.
- Tenant `suspended` → 503 com mensagem.

### `DbContextFactory` tenant-aware

- Substitui `services.AddDbContext`.
- Constrói `ConnectVeiculosDbContext` com `UseSqlite(tenantContext.ConnectionString)`.
- Registra como Scoped (1 por request).

### `IDbConnectionFactory` tenant-aware (para Dapper)

- Substitui o `DbSession` atual.
- Mesma lógica: connection string vem do tenant context.

### `MigrationsRunner`

- No startup: lê tenants do master, para cada um cria DbContext apontando para o banco e roda `EnsureCreated`.
- Lock simples (file lock ou semaphore) para evitar concorrência em horizontal scaling futuro.

## Onboarding

`scripts/criar-tenant.sh`:

```bash
bash scripts/criar-tenant.sh \
  --slug acme \
  --nome "Acme Veiculos" \
  --admin-email joao@acme.com.br \
  --admin-senha TempXyz123
```

Faz, na ordem:
1. Valida slug (regex `^[a-z][a-z0-9-]{2,30}$`).
2. Insere registro no `_master.db`.
3. Cria `data/{slug}.db` vazio.
4. Roda `EnsureCreated` (via endpoint admin protegido, ou via comando `dotnet`).
5. Insere usuário admin do tenant.
6. Pede cert SSL para `{slug}.connectveiculos.dev.br` via certbot --webroot --expand.
7. Recarrega nginx.

## nginx

- `server_name *.connectveiculos.dev.br connectveiculos.dev.br www.connectveiculos.dev.br;` em ambos os blocos (HTTP e HTTPS).
- Backend continua único; o middleware do .NET resolve o tenant pelo Host header.
- Cert: por subdomain (vai expandindo). Wildcard fica como melhoria futura (exige Cloudflare DNS-01 ou similar).

## Migração do banco atual

O `data/cliente.db` que está rodando hoje vira o tenant `default`:

1. `mv data/cliente.db data/default.db`
2. Insere no `_master.db`: `(slug='default', nome='Tenant Padrão', database_file='default.db', status='active')`.
3. Middleware resolve `connectveiculos.dev.br` (sem subdomain) e `www.` para o tenant `default`.

Backup-sqlite.sh: itera sobre `data/*.db` (ou consulta o master) em vez de hardcode `cliente.db`.

## Riscos & mitigações

| Risco | Mitigação |
|---|---|
| Esquecer de propagar tenant em alguma camada (ex: cache key, signalr group, Hangfire job context) | Auditoria com grep + testes de isolamento por tenant na suite |
| Migrations EF Core precisam rodar em N bancos | Iterador no startup com lock; tempo proporcional a N (~1s por tenant na primeira inicialização) |
| Subdomain typo acessa tenant errado | Slug validado no master; subdomain inexistente retorna 404 antes de chegar no DbContext |
| Performance: abrir muitas conexões SQLite simultâneas | SQLite é file-based, pool por arquivo; com 5-20 tenants pequenos, sem problema |
| Cron jobs (Hangfire) sem tenant context | Cada job tem que receber tenant na inicialização; jobs cross-tenant rodam separadamente por tenant |

## Fora de escopo (próximas iterações)

- Self-service (cliente cadastra sozinho via web).
- Cobrança (Stripe/Asaas).
- White-label avançado (logo/cores próprias por tenant — hoje cada Loja já permite cores; tenant pode reusar).
- Wildcard SSL (Cloudflare DNS-01).
- Tenant-level rate limiting.
- Multi-region / sharding por região.

## Sequência de execução

Vide tasks Fase 2 a 6 no TaskList. Branch `feature/multi-tenant` separada do master até validação.
