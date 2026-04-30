# ConnectVeiculos

Sistema de gestao para revendas de veiculos: catalogo publico SEO + admin (RBAC) + integracoes com Mercado Livre, Facebook Catalog, Google Merchant e WhatsApp Business.

## Stack

- **Backend:** .NET 8 (API + Entity Framework Core + SignalR + Hangfire + Serilog)
- **Frontend:** Angular 18 (PWA + SSR + SignalR Client + Chart.js)
- **Banco:** SQLite em dev / PostgreSQL em prod (auto-migracoes idempotentes no startup)
- **Deploy:** Docker Compose + Nginx + Let's Encrypt

## Pre-requisitos (local)

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 22+](https://nodejs.org) — **nao funciona em Node 14**
- Git

## Setup local (5 minutos)

```bash
# 1. Clonar
git clone https://github.com/Nunes-99/ConnectVeiculos.git
cd ConnectVeiculos

# 2. Backend
cd back-end/ConnectVeiculos.API
cp appsettings.Development.example.json appsettings.Development.json
dotnet run --launch-profile http
# → http://localhost:5219 (banco SQLite criado automaticamente, admin seeded)

# 3. Frontend (em outro terminal)
cd front-end/ConnectVeiculos.Web
npm install
npx ng serve
# → http://localhost:4200
```

**Login default:** `admin@connectveiculos.com.br` / `admin123` (criado automaticamente no primeiro startup do backend).

## Configurar integracoes (opcionais em dev)

| Integracao | Funciona local? | O que precisa |
|---|---|---|
| **FIPE** (autocomplete marca/modelo) | ✅ | Nada — usa API publica gratuita |
| **Detran** (consulta debitos) | ✅ | Nada — redireciona pro site oficial do estado |
| **Mercado Livre** | ❌ (so prod) | App em developers.mercadolivre.com.br + HTTPS publico |
| **WhatsApp Business** | ❌ (so prod) | Conta Meta Business + numero verificado + HTTPS publico |
| **Facebook Catalog** | ❌ (so prod) | URL publica pro feed |
| **Google Merchant** | ❌ (so prod) | Dominio verificado |
| **Push PWA** | ⚠️ Opcional | Gerar VAPID keys (ver abaixo) |

### Push PWA (opcional)

Gerar uma vez:
```bash
npx web-push generate-vapid-keys
```

Adicione as 2 strings em `appsettings.Development.json`:
```json
"Vapid": {
  "PublicKey": "BCoEQ...",
  "PrivateKey": "PfIeB...",
  "Subject": "mailto:contato@suaempresa.com"
}
```

Sem isso, push fica desligado — resto do sistema funciona normal.

## Rodar testes

```bash
# Unitarios (197 testes)
cd back-end
dotnet test ConnectVeiculos.sln

# E2E (17 testes Playwright) — precisa backend rodando
cd front-end/ConnectVeiculos.Web
npx playwright install chromium  # primeira vez
npx playwright test
```

## Deploy producao

Veja [DEPLOY_ORACLE.md](./DEPLOY_ORACLE.md) para o passo-a-passo completo. Resumo:

1. VM Ubuntu 22.04+ com Docker
2. DNS apontando pro IP da VM
3. Portas 80/443 abertas no firewall
4. `cp .env.example .env` + preencher credenciais
5. `docker compose up -d --build`
6. `bash scripts/setup-https.sh` para HTTPS via Let's Encrypt

## Documentacao

- [MANUAL_USUARIO.md](./MANUAL_USUARIO.md) — manual nao tecnico para o dono da loja
- [GUIA-INTEGRACOES.md](./GUIA-INTEGRACOES.md) — passo-a-passo de cada integracao (ML, Facebook, Google, WhatsApp)
- [DEPLOY_ORACLE.md](./DEPLOY_ORACLE.md) — guia completo de deploy
- [GUIA-SEO-GOOGLE.md](./GUIA-SEO-GOOGLE.md) — Search Console + sitemap
- [PRODUCAO_CHECKLIST.md](./PRODUCAO_CHECKLIST.md) — checklist pre-deploy
- [PENDENCIAS.md](./PENDENCIAS.md) — historico de melhorias

## Estado atual

| Item | Status |
|---|---|
| Backend builda | ✅ 0 erros, 0 warnings |
| Frontend builda | ✅ |
| Testes unitarios | ✅ 197/197 |
| Testes E2E | ✅ 17/17 |
| Codigo pronto pra producao | ✅ |
| Deploy realizado | ⏳ pendente (humano) |
| Credenciais ML/Meta | ⏳ pendente (humano) |

## Estrutura

```
ConnectVeiculos/
├── back-end/                # .NET 8 (Clean Architecture)
│   ├── ConnectVeiculos.API/         # Controllers, middleware
│   ├── ConnectVeiculos.Application/ # UseCases, ViewModels
│   ├── ConnectVeiculos.Core/        # Entities, interfaces
│   ├── ConnectVeiculos.Infrastructure/ # EF Core, services externos
│   └── ConnectVeiculos.Tests/       # xUnit + Moq + FluentAssertions
├── front-end/ConnectVeiculos.Web/   # Angular 18 + SSR
├── nginx/                   # Config reverse proxy
├── scripts/                 # Setup HTTPS, criar cliente
├── banco-de-dados/          # Schema SQL de referencia
└── docker-compose.yml
```

## Troubleshooting

**Frontend nao builda com erro de Node:** rode `nvm use 22` antes de `npm install`.
**Backend porta 5219 em uso:** mate o processo ou troque a porta em `Properties/launchSettings.json`.
**E2E falha com timeout:** o backend precisa estar rodando antes de `npx playwright test`.
**Login admin nao funciona:** apague `back-end/ConnectVeiculos.API/ConnectVeiculos.db` e rode `dotnet run` de novo (recria com seed limpo).
