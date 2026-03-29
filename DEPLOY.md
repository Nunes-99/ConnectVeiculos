# Guia de Deploy - ConnectVeiculos

Este guia explica como fazer o deploy gratuito da aplicacao na nuvem.

## Arquitetura de Deploy

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│     Vercel      │────►│     Render      │────►│    Supabase     │
│   (Front-end)   │     │   (Back-end)    │     │  (PostgreSQL)   │
│     GRATIS      │     │     GRATIS      │     │     GRATIS      │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

---

## PASSO 1: Criar Banco de Dados no Supabase

### 1.1 Criar conta no Supabase
1. Acesse https://supabase.com
2. Clique em "Start your project"
3. Faca login com GitHub ou email

### 1.2 Criar novo projeto
1. Clique em "New project"
2. Preencha:
   - **Name**: `connectveiculos`
   - **Database Password**: (anote esta senha!)
   - **Region**: South America (Sao Paulo)
3. Clique em "Create new project"
4. Aguarde ~2 minutos para o projeto ser criado

### 1.3 Obter Connection String
1. No painel do projeto, va em **Settings** > **Database**
2. Role ate "Connection string"
3. Selecione **URI**
4. Copie a string e substitua `[YOUR-PASSWORD]` pela senha que voce criou
5. Guarde esta string, sera usada no Render

**Formato da string:**
```
postgresql://postgres:[SUA-SENHA]@db.[ID-DO-PROJETO].supabase.co:5432/postgres
```

---

## PASSO 2: Deploy do Back-end no Render

### 2.1 Subir codigo para GitHub
Primeiro, suba o projeto para um repositorio no GitHub:

```bash
cd C:\Users\vitor.nunes\Documents\Vitor\ConnectVeiculos
git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin https://github.com/SEU-USUARIO/connectveiculos.git
git push -u origin main
```

### 2.2 Criar conta no Render
1. Acesse https://render.com
2. Clique em "Get Started for Free"
3. Faca login com GitHub

### 2.3 Criar Web Service
1. No dashboard, clique em **New** > **Web Service**
2. Conecte seu repositorio GitHub
3. Selecione o repositorio `connectveiculos`
4. Configure:
   - **Name**: `connectveiculos-api`
   - **Region**: Ohio (US East) ou o mais proximo
   - **Branch**: `main`
   - **Root Directory**: `back-end`
   - **Runtime**: Docker
   - **Instance Type**: Free

### 2.4 Configurar Variaveis de Ambiente
Na secao "Environment Variables", adicione:

| Variavel | Valor |
|----------|-------|
| `DATABASE_URL` | `postgresql://postgres:SENHA@db.xxx.supabase.co:5432/postgres` |
| `JWT_SECRET_KEY` | `SuaChaveSecretaMuitoSegura2024!@#$%` (crie uma chave forte) |
| `ALLOWED_ORIGINS` | `https://connectveiculos.vercel.app` (ajustar apos deploy do front) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

### 2.5 Deploy
1. Clique em "Create Web Service"
2. Aguarde o build (~5-10 minutos)
3. Apos o deploy, voce tera uma URL tipo: `https://connectveiculos-api.onrender.com`

### 2.6 Testar API
Acesse: `https://connectveiculos-api.onrender.com/swagger`

---

## PASSO 3: Deploy do Front-end no Vercel

### 3.1 Atualizar URL da API
Antes do deploy, atualize o arquivo `front-end/ConnectVeiculos.Web/src/environments/environment.prod.ts`:

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://connectveiculos-api.onrender.com/api' // URL do seu Render
};
```

Faca commit e push:
```bash
git add .
git commit -m "Update API URL for production"
git push
```

### 3.2 Criar conta no Vercel
1. Acesse https://vercel.com
2. Clique em "Start Deploying"
3. Faca login com GitHub

### 3.3 Importar Projeto
1. Clique em "Add New" > "Project"
2. Selecione o repositorio `connectveiculos`
3. Configure:
   - **Framework Preset**: Other
   - **Root Directory**: `front-end/ConnectVeiculos.Web`
   - **Build Command**: `npm run build`
   - **Output Directory**: `dist/connect-veiculos.web/browser`

### 3.4 Deploy
1. Clique em "Deploy"
2. Aguarde o build (~2-3 minutos)
3. Voce tera uma URL tipo: `https://connectveiculos.vercel.app`

---

## PASSO 4: Configurar CORS no Render

Apos o deploy do front-end, atualize a variavel `ALLOWED_ORIGINS` no Render:

1. Va no dashboard do Render
2. Selecione o servico `connectveiculos-api`
3. Va em **Environment**
4. Edite `ALLOWED_ORIGINS` para incluir a URL do Vercel:
   ```
   https://connectveiculos.vercel.app,https://seu-dominio-customizado.com
   ```
5. Clique em "Save Changes"
6. O servico sera reiniciado automaticamente

---

## PASSO 5: Testar a Aplicacao

1. Acesse a URL do Vercel
2. Faca login com:
   - Email: `admin@connectveiculos.com.br`
   - Senha: `admin123`
3. Teste as funcionalidades

---

## Variaveis de Ambiente Completas

### Render (Back-end)

| Variavel | Descricao | Exemplo |
|----------|-----------|---------|
| `DATABASE_URL` | Connection string PostgreSQL | `postgresql://postgres:xxx@db.xxx.supabase.co:5432/postgres` |
| `JWT_SECRET_KEY` | Chave secreta para JWT | `MinhaChaveSecreta2024!@#` |
| `ALLOWED_ORIGINS` | URLs permitidas (separadas por virgula) | `https://app.vercel.app` |
| `ASPNETCORE_ENVIRONMENT` | Ambiente de execucao | `Production` |
| `SMTP_SERVER` | Servidor SMTP (opcional) | `smtp.gmail.com` |
| `SMTP_USERNAME` | Usuario SMTP (opcional) | `email@gmail.com` |
| `SMTP_PASSWORD` | Senha SMTP (opcional) | `senha-app` |

---

## Custos

| Servico | Plano | Custo | Limitacoes |
|---------|-------|-------|------------|
| Supabase | Free | R$ 0 | 500MB banco, 2 projetos |
| Render | Free | R$ 0 | Hiberna apos 15min inativo |
| Vercel | Hobby | R$ 0 | 100GB bandwidth |

**Nota**: O plano gratuito do Render "hiberna" o servico apos 15 minutos sem uso. A primeira requisicao apos hibernacao demora ~30 segundos. Para uso comercial, considere o plano pago ($7/mes).

---

## Manutencao Centralizada

Com esta arquitetura, voce tem controle total centralizado:

1. **Atualizacoes de codigo**: Push para GitHub > Deploy automatico
2. **Atualizacoes de banco**: Acesse Supabase > Execute SQL
3. **Logs e monitoramento**: Dashboard do Render
4. **Todos os clientes**: Usam a mesma API e banco (isolados por `lojaId`)

---

## Dominio Customizado (Opcional)

### Vercel
1. Va em Settings > Domains
2. Adicione seu dominio (ex: `app.connectveiculos.com.br`)
3. Configure DNS no seu provedor

### Render
1. Va em Settings > Custom Domains
2. Adicione seu dominio (ex: `api.connectveiculos.com.br`)
3. Configure DNS no seu provedor

---

## Troubleshooting

### Erro de CORS
- Verifique se `ALLOWED_ORIGINS` inclui a URL do front-end
- Certifique-se de usar HTTPS

### Erro de conexao com banco
- Verifique se `DATABASE_URL` esta correto
- Confirme que a senha nao tem caracteres especiais problemáticos

### API nao responde
- O servico pode estar hibernando (aguarde ~30s)
- Verifique os logs no dashboard do Render

### Build falha no Render
- Verifique se o Dockerfile esta na pasta `back-end`
- Confira os logs de build para erros especificos
