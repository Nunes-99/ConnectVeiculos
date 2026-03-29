# ConnectVeiculos - Checklist para Producao

## Visao Geral

Este documento descreve o passo a passo para preparar e deployar o sistema ConnectVeiculos em ambiente de producao.

---

## 1. Configuracoes de Ambiente

### 1.1 Backend (.NET)

#### Criar arquivo `appsettings.Production.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=SEU_HOST;Database=connectveiculos;Username=SEU_USER;Password=SUA_SENHA;SSL Mode=Require"
  },
  "JwtSettings": {
    "Secret": "CHAVE_SECRETA_COM_MINIMO_32_CARACTERES_ALEATORIOS",
    "Issuer": "ConnectVeiculos",
    "Audience": "ConnectVeiculosUsers",
    "ExpirationInMinutes": 60,
    "RefreshTokenExpirationInDays": 7
  },
  "EmailSettings": {
    "SmtpServer": "smtp.seudominio.com",
    "Port": 587,
    "Username": "noreply@seudominio.com",
    "Password": "SENHA_EMAIL",
    "FromEmail": "noreply@seudominio.com",
    "FromName": "Connect Veiculos",
    "EnableSsl": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "seudominio.com,www.seudominio.com"
}
```

#### Variaveis de Ambiente (alternativa mais segura)

```bash
# Banco de Dados
DATABASE_URL=Host=...;Database=...;Username=...;Password=...

# JWT
JWT_SECRET=sua_chave_secreta_aqui

# Email
SMTP_SERVER=smtp.seudominio.com
SMTP_PORT=587
SMTP_USERNAME=noreply@seudominio.com
SMTP_PASSWORD=senha_email
```

### 1.2 Frontend (Angular)

#### Criar/Atualizar `environment.prod.ts`

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://api.seudominio.com/api',
  signalRUrl: 'https://api.seudominio.com'
};
```

---

## 2. Banco de Dados

### 2.1 Escolher Provedor

- [ ] **PostgreSQL** (Recomendado para producao)
  - Render.com (gratuito ate 1GB)
  - Railway.app
  - Supabase
  - AWS RDS
  - Azure Database for PostgreSQL

### 2.2 Configurar Banco

```bash
# Aplicar migrations
cd back-end/ConnectVeiculos.API
dotnet ef database update --connection "SUA_CONNECTION_STRING"
```

### 2.3 Backup Automatico

- [ ] Configurar backup diario automatico
- [ ] Testar restauracao de backup

---

## 3. Seguranca

### 3.1 HTTPS/SSL

- [ ] Obter certificado SSL (Let's Encrypt gratuito)
- [ ] Configurar HTTPS no servidor
- [ ] Redirecionar HTTP para HTTPS

### 3.2 Configuracoes de Seguranca no Backend

Adicionar em `Program.cs` para producao:

```csharp
// HSTS (HTTP Strict Transport Security)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});
```

### 3.3 Rate Limiting

Adicionar pacote e configurar:

```bash
dotnet add package AspNetCoreRateLimit
```

```csharp
// Em Program.cs
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = 100
        }
    };
});
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// No pipeline
app.UseIpRateLimiting();
```

### 3.4 CORS para Producao

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://seudominio.com", "https://www.seudominio.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
```

### 3.5 Checklist de Seguranca

- [ ] JWT Secret com minimo 256 bits (32+ caracteres)
- [ ] Senhas de banco fortes
- [ ] Variaveis de ambiente para secrets (nao commitar)
- [ ] Remover endpoints de debug/swagger em producao (opcional)
- [ ] Configurar CORS corretamente
- [ ] Validar todos os inputs (ja implementado com FluentValidation)

---

## 4. Logging e Monitoramento

### 4.1 Configurar Serilog

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Console
```

```csharp
// Em Program.cs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
```

### 4.2 Health Checks

```bash
dotnet add package AspNetCore.HealthChecks.NpgSql
```

```csharp
// Em Program.cs
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database");

// No pipeline
app.MapHealthChecks("/health");
```

### 4.3 Monitoramento (Opcional)

- [ ] Application Insights (Azure)
- [ ] Sentry (erros)
- [ ] Grafana + Prometheus (metricas)

---

## 5. Build e Deploy

### 5.1 Build do Backend

```bash
cd back-end/ConnectVeiculos.API

# Build para producao
dotnet publish -c Release -o ./publish

# Os arquivos estarao em ./publish
```

### 5.2 Build do Frontend

```bash
cd front-end/ConnectVeiculos.Web

# Build para producao
npm run build -- --configuration production

# Os arquivos estarao em ./dist/connect-veiculos.web
```

### 5.3 Opcoes de Hospedagem

#### Backend (.NET)

| Plataforma | Custo | Observacoes |
|------------|-------|-------------|
| Render.com | Gratuito/Pago | Facil deploy |
| Railway.app | Gratuito/Pago | Bom para .NET |
| Azure App Service | Pago | Melhor integracao .NET |
| AWS Elastic Beanstalk | Pago | Escalavel |
| VPS (DigitalOcean, Linode) | ~$5/mes | Controle total |

#### Frontend (Angular)

| Plataforma | Custo | Observacoes |
|------------|-------|-------------|
| Vercel | Gratuito | Excelente para SPAs |
| Netlify | Gratuito | Facil configuracao |
| GitHub Pages | Gratuito | Simples |
| Cloudflare Pages | Gratuito | Rapido globalmente |
| Azure Static Web Apps | Gratuito | Integrado com Azure |

---

## 6. Docker (Opcional)

### 6.1 Dockerfile para Backend

```dockerfile
# back-end/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ConnectVeiculos.API/ConnectVeiculos.API.csproj", "ConnectVeiculos.API/"]
COPY ["ConnectVeiculos.Application/ConnectVeiculos.Application.csproj", "ConnectVeiculos.Application/"]
COPY ["ConnectVeiculos.Core/ConnectVeiculos.Core.csproj", "ConnectVeiculos.Core/"]
COPY ["ConnectVeiculos.Infrastructure/ConnectVeiculos.Infrastructure.csproj", "ConnectVeiculos.Infrastructure/"]
RUN dotnet restore "ConnectVeiculos.API/ConnectVeiculos.API.csproj"
COPY . .
WORKDIR "/src/ConnectVeiculos.API"
RUN dotnet build "ConnectVeiculos.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ConnectVeiculos.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConnectVeiculos.API.dll"]
```

### 6.2 Dockerfile para Frontend

```dockerfile
# front-end/Dockerfile
FROM node:18-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration production

FROM nginx:alpine
COPY --from=build /app/dist/connect-veiculos.web /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### 6.3 Docker Compose

```yaml
# docker-compose.yml
version: '3.8'

services:
  api:
    build:
      context: ./back-end
      dockerfile: Dockerfile
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DATABASE_URL=${DATABASE_URL}
      - JWT_SECRET=${JWT_SECRET}
    depends_on:
      - db

  web:
    build:
      context: ./front-end/ConnectVeiculos.Web
      dockerfile: Dockerfile
    ports:
      - "80:80"
    depends_on:
      - api

  db:
    image: postgres:15
    environment:
      POSTGRES_DB: connectveiculos
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

volumes:
  postgres_data:
```

---

## 7. CI/CD (Opcional)

### 7.1 GitHub Actions

```yaml
# .github/workflows/deploy.yml
name: Deploy

on:
  push:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Run Tests
        run: |
          cd back-end
          dotnet test --verbosity normal

  deploy-api:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      # Adicionar steps de deploy especificos da plataforma

  deploy-web:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup Node
        uses: actions/setup-node@v3
        with:
          node-version: '18'

      - name: Build
        run: |
          cd front-end/ConnectVeiculos.Web
          npm ci
          npm run build -- --configuration production

      # Adicionar steps de deploy especificos da plataforma
```

---

## 8. Checklist Final Pre-Deploy

### Configuracoes
- [ ] `appsettings.Production.json` configurado
- [ ] `environment.prod.ts` configurado
- [ ] Connection string do banco de producao
- [ ] JWT Secret forte e unico
- [ ] Configuracoes de email

### Seguranca
- [ ] HTTPS configurado
- [ ] CORS configurado para dominio de producao
- [ ] Rate limiting ativado
- [ ] Headers de seguranca configurados
- [ ] Secrets em variaveis de ambiente (nao no codigo)

### Banco de Dados
- [ ] Migrations aplicadas
- [ ] Backup automatico configurado
- [ ] Usuario admin inicial criado

### Testes
- [ ] Todos os testes passando (`dotnet test`)
- [ ] Build de producao funcionando
- [ ] Teste manual das funcionalidades principais

### Monitoramento
- [ ] Health check endpoint funcionando
- [ ] Logs configurados
- [ ] Alertas de erro (opcional)

### Deploy
- [ ] Build do backend (`dotnet publish -c Release`)
- [ ] Build do frontend (`npm run build --configuration production`)
- [ ] Deploy em servidor/plataforma escolhida
- [ ] DNS configurado
- [ ] SSL/HTTPS ativo

---

## 9. Pos-Deploy

- [ ] Testar login/logout
- [ ] Testar CRUD de veiculos
- [ ] Testar registro de vendas
- [ ] Testar notificacoes em tempo real (SignalR)
- [ ] Testar dashboard
- [ ] Testar exportacao de relatorios
- [ ] Verificar logs de erro
- [ ] Monitorar performance

---

## Contatos e Suporte

- Desenvolvedor: [Seu nome]
- Email: [seu@email.com]
- Repositorio: [URL do repositorio]

---

*Documento gerado em: 31/12/2025*
*Versao: 1.0*
