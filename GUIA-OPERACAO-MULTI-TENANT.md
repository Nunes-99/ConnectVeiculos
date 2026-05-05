# Guia operacional — Multi-tenant

Sistema agora suporta múltiplos clientes (concessionárias) num mesmo deployment, cada um com banco SQLite isolado em `data/{slug}.db`. O acesso é diferenciado por subdomain (`acme.connectveiculos.dev.br` → tenant `acme`).

Este guia cobre as operações do dia-a-dia. O plano arquitetural completo está em [`MULTI_TENANT.md`](MULTI_TENANT.md).

## Visão rápida

| | |
|---|---|
| Tenant principal | `default` (slug). Acessível em `connectveiculos.dev.br` (sem subdomain), `www.connectveiculos.dev.br`, e por padrão também IP nu / localhost. Banco: `data/cliente.db`. |
| Banco master (registry) | `data/_master.db` — tabela `Tenants` com {Id, Slug, Nome, DatabaseFile, Status}. |
| Tenants extras | `acme`, `motorsbr`, etc. → subdomains `{slug}.connectveiculos.dev.br` → bancos `data/{slug}.db`. |
| Onboarding | Endpoint `POST /api/admin/tenants` (autenticado por `X-Admin-Token`) ou script `scripts/criar-tenant.sh`. |
| Isolamento | Físico no banco. Bug em alguma query não vaza dados entre tenants — é outro arquivo. |

## Pré-requisitos (uma vez por instalação)

### 1) DNS wildcard

No painel do Registro.br (ou outro provider DNS), adicione:

```
A    *    →   136.248.77.154    TTL 300
```

Esse `A *` faz qualquer subdomain (`acme`, `demo`, `qualquer`) resolver para o mesmo IP da VM. Sem isso, cada tenant novo exige um A record específico.

### 2) `ADMIN_API_TOKEN` configurado

Já feito em 2026-05-05. O token está em `~/Documents/Vitor/ConnectVeiculos-CREDENCIAIS.txt` na linha `ADMIN_API_TOKEN=...`. Também está no `.env` da VM.

Se precisar rotacionar o token (suspeita de vazamento):
```bash
NEW_TOKEN=$(openssl rand -base64 36 | tr -d '/+=' | head -c 48)
# Atualiza no .env da VM
ssh -i ~/.ssh/connectveiculos-oracle.key ubuntu@136.248.77.154 \
    "sed -i 's|^ADMIN_API_TOKEN=.*|ADMIN_API_TOKEN='\"$NEW_TOKEN\"'|' /home/ubuntu/ConnectVeiculos/.env && \
     cd /home/ubuntu/ConnectVeiculos && sudo docker compose up -d --force-recreate --no-deps backend-cliente"
# Atualiza local em ConnectVeiculos-CREDENCIAIS.txt
```

## Operações do dia-a-dia

### Listar tenants existentes

```bash
curl -s -H "X-Admin-Token: $ADMIN_API_TOKEN" \
    https://connectveiculos.dev.br/api/admin/tenants | jq
```

### Criar tenant novo (via script)

Roda do PC local (recomendado) — o script cuida do JSON, do header e dá próximos passos:

```bash
bash scripts/criar-tenant.sh \
    --slug acme \
    --nome "Acme Veiculos LTDA" \
    --admin-email joao@acme.com.br \
    --admin-senha TempXyz123 \
    --admin-nome "João Silva"
```

Após criação, o script imprime os próximos passos manuais:

1. Garantir que o DNS resolve `{slug}.connectveiculos.dev.br` (já resolve se você fez o passo 1 dos Pré-requisitos).
2. Expandir o cert SSL Let's Encrypt para incluir o novo subdomain:
   ```bash
   ssh -i ~/.ssh/connectveiculos-oracle.key ubuntu@136.248.77.154 "sudo certbot certonly --webroot --webroot-path /var/www/certbot \
       --email contato@connectveiculos.dev.br --agree-tos --no-eff-email --non-interactive --expand \
       -d connectveiculos.dev.br -d www.connectveiculos.dev.br -d {slug}.connectveiculos.dev.br"
   ```
3. Reload do nginx para servir o cert atualizado:
   ```bash
   ssh -i ~/.ssh/connectveiculos-oracle.key ubuntu@136.248.77.154 \
       "sudo docker compose -f /home/ubuntu/ConnectVeiculos/docker-compose.yml exec nginx nginx -s reload"
   ```
4. Cliente acessa `https://{slug}.connectveiculos.dev.br` e loga com o email/senha que você passou.

### Suspender ou reativar um tenant

Hoje só via SQL direto no `_master.db` na VM (UI/endpoint dedicado fica para o futuro):

```bash
ssh -i ~/.ssh/connectveiculos-oracle.key ubuntu@136.248.77.154 \
    "sudo sqlite3 /home/ubuntu/ConnectVeiculos/data/_master.db \
     \"UPDATE Tenants SET TenStatus = 2 WHERE TenSlug = 'acme';\" && \
     curl -s -X POST http://localhost:8080/api/admin/tenants/_invalidate-cache -H 'X-Admin-Token: \$ADMIN_API_TOKEN' || true"
```

> **Status**: 1 = Active, 2 = Suspended. Tenant suspenso retorna 503 no acesso. Cache do `TenantStore` precisa ser invalidado — o backend lê do master no boot, alterações no master via SQL externo só pegam após restart do backend OU chamada do endpoint admin que invalida cache.

Caminho mais simples para suspender: restart do backend após o UPDATE:
```bash
sudo docker compose -f /home/ubuntu/ConnectVeiculos/docker-compose.yml restart backend-cliente
```

### Remover um tenant

Hoje manual (não há script `excluir-tenant.sh` ainda — TODO):

```bash
# 1. Remover do master
sudo sqlite3 /home/ubuntu/ConnectVeiculos/data/_master.db \
    "DELETE FROM Tenants WHERE TenSlug = 'acme';"

# 2. Mover .db para arquivo (não deletar imediatamente — backup):
sudo mv /home/ubuntu/ConnectVeiculos/data/acme.db \
        /home/ubuntu/ConnectVeiculos/data/_arquivado_acme_$(date +%Y%m%d).db

# 3. Restart backend para invalidar cache
sudo docker compose -f /home/ubuntu/ConnectVeiculos/docker-compose.yml restart backend-cliente

# 4. (opcional) Reduzir cert SSL via certbot --expand sem o subdomain do tenant.
```

## Backup e restore

O cron diário `scripts/backup-sqlite.sh` (03:30) hoje só faz backup do tenant `default` (`cliente.db`). Para fazer backup de todos os tenants, atualizar o script para iterar:

> **TODO** (próxima sessão se relevante): adaptar `backup-sqlite.sh` para listar `data/*.db` e fazer backup de cada um, mantendo a mesma retenção e upload off-site.

Restore de um tenant específico:

```bash
# 1. Pega o backup off-site mais recente do tenant 'acme'
ssh ubuntu@136.248.77.154 \
    "~/.local/bin/oci os object list --bucket-name connectveiculos-backups --prefix 'acme-' --query 'data[-1].name' --raw-output"
# (assumindo que adaptamos backup-sqlite.sh para usar o nome do tenant no prefixo do arquivo)

# 2. Stop backend
sudo docker compose -f /home/ubuntu/ConnectVeiculos/docker-compose.yml stop backend-cliente

# 3. Baixa o backup, descomprime, sobrescreve o banco
oci os object get --bucket-name connectveiculos-backups --name acme-XXXX.db.gz --file /tmp/acme.db.gz
gunzip /tmp/acme.db.gz
sudo mv /tmp/acme.db /home/ubuntu/ConnectVeiculos/data/acme.db
sudo chown root:root /home/ubuntu/ConnectVeiculos/data/acme.db

# 4. Start backend
sudo docker compose -f /home/ubuntu/ConnectVeiculos/docker-compose.yml start backend-cliente
```

## Troubleshooting

### Subdomain `acme.connectveiculos.dev.br` retorna 404 com mensagem "Tenant 'acme' nao encontrado"

Tenant não está cadastrado no master. Confirme via:
```bash
curl -s -H "X-Admin-Token: $ADMIN_API_TOKEN" https://connectveiculos.dev.br/api/admin/tenants
```

Se faltar, rode `bash scripts/criar-tenant.sh ...`.

### Subdomain dá warning de cert SSL (NET::ERR_CERT_COMMON_NAME_INVALID)

Cert atual não cobre o subdomain. Expandir via certbot --expand (instruções acima).

### `502 Bad Gateway` ao acessar tenant válido

Backend caiu. Logs:
```bash
sudo docker compose -f /home/ubuntu/ConnectVeiculos/docker-compose.yml logs --tail=50 backend-cliente
```

Causa comum: erro de migration no banco do tenant, ou banco corrompido. Restore do backup.

### Listar tenants retorna `503 ADMIN_API_TOKEN nao configurado`

Env var não chegou no container. Confirme:
```bash
sudo docker compose -f /home/ubuntu/ConnectVeiculos/docker-compose.yml exec backend-cliente printenv ADMIN_API_TOKEN
```

Se vazio, recreate o container:
```bash
sudo docker compose -f /home/ubuntu/ConnectVeiculos/docker-compose.yml up -d --force-recreate --no-deps backend-cliente
```

### Tenant criado mas não aparece na listagem (cache stale)

`TenantStore` faz cache em memória. Quando criar tenant via API, o controller invalida o cache automaticamente. Mas se você inserir tenant via SQL direto no master, precisa restart do backend ou (futuro) chamar endpoint de invalidação.

## Integrações por tenant

`ConfiguracaoSistema` é uma tabela **dentro do banco do tenant** — significa que cada tenant tem suas próprias configurações de integração. **Cada cliente operador deve criar contas/apps próprios e configurar via UI `/integracoes`**:

| Integração | O que cada cliente precisa |
|---|---|
| **Mercado Livre** | Criar app em `developers.mercadolivre.com.br`. Redirect URI configurado lá: `https://{slug}.connectveiculos.dev.br/api/integracoes/mercadolivre/callback`. App ID e Client Secret colados na UI `/integracoes`. |
| **Facebook Catalog** | Conta Meta Business + Catalog ID + Access Token na UI |
| **Google Merchant** | Merchant ID + OAuth do Google na UI |
| **WhatsApp Business** | Conta Meta Business + número verificado + tokens na UI |
| **SMTP** | Credenciais Gmail App Password (ou outro provider) na UI |

⚠️ **Atenção**: as env vars `ML_*`, `WHATSAPP_*`, `FB_*`, `GOOGLE_MERCHANT_*` no `docker-compose.yml` são **fallbacks globais** (compartilhados entre todos tenants). Se você configurar elas, valem para tenants que ainda não cadastraram credenciais próprias. Em produção real com vários clientes, cada um configura via UI — assim cada tenant tem suas próprias contas/credenciais corporativas.

## Limites conhecidos

- **Postgres como banco backend**: hoje suporte parcial — fallback funciona, mas multi-tenant via banco-por-tenant exige SQLite. Adaptação para Postgres (schema-per-tenant ou database-per-tenant) é refatoração separada.
- **Migrations EF formais**: o sistema usa `EnsureCreated` + `ApplySchemaUpdates` (raw SQL idempotente). `TenantsMigrationsRunner` aplica os schema updates em cada tenant no startup. Mudanças aditivas (novas colunas/tabelas) funcionam; renames/changes de tipo exigiriam migrations EF formais — fora de escopo.
- **Hangfire jobs (refatorados em 2026-05-05)**: jobs recurring iteram sobre todos os tenants ativos via `MultiTenantJobExecutor`. Falha isolada num tenant não interrompe os outros (apenas logada); job só é marcado Failed no Hangfire dashboard se TODOS os tenants falharem.
- **AtualizarCacheFipeJob**: único job que NÃO é tenant-aware — ele só popula um cache em memória da API FIPE pública, sem dado de cliente. Compartilhado entre todos os tenants intencionalmente.
- **CORS**: aceita qualquer subdomain de `connectveiculos.dev.br` automaticamente (via `ALLOWED_ROOT_DOMAINS`). Para adicionar outros domínios raiz no futuro, atualizar a env var.
- **SignalR Hubs**: conexão SignalR começa via HTTP request, então o middleware resolve o tenant. `INotificacaoService` injetado no hub recebe `ConnectVeiculosDbContext` do scope com tenant correto. Quando enviado a partir de Hangfire job, `MultiTenantJobExecutor` cuida do scope.
