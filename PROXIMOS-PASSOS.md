# ConnectVeiculos - Proximos Passos

Guia completo para colocar todas as novas funcionalidades em producao.

> **Documentos relacionados:**
> - [GUIA-SEO-GOOGLE.md](./GUIA-SEO-GOOGLE.md) - Detalhes sobre SEO e Google Search Console
> - [GUIA-INTEGRACOES.md](./GUIA-INTEGRACOES.md) - Passo-a-passo das integracoes (ML, Facebook, Google)

---

## Passo 1: Migrar o Banco de Dados

Antes de testar qualquer coisa, execute os SQLs abaixo no banco de dados (Oracle ou SQLite local).

### Se estiver usando SQLite (local):

```bash
sqlite3 ConnectVeiculos.db
```

### Se estiver usando Oracle/PostgreSQL (producao):

Adapte a sintaxe conforme o banco. Os comandos abaixo sao para SQLite/PostgreSQL:

```sql
-- Nova coluna na tabela Loja (URL do catalogo compartilhada)
ALTER TABLE Loja ADD COLUMN LojUrlCatalogo TEXT;

-- Tabela de configuracoes do sistema (tokens do Mercado Livre, Google, etc.)
CREATE TABLE IF NOT EXISTS ConfiguracaoSistema (
    CfgId INTEGER PRIMARY KEY AUTOINCREMENT,
    CfgChave TEXT NOT NULL UNIQUE,
    CfgValor TEXT,
    CfgDtAtualizacao TEXT
);

-- Tabela de publicacoes em plataformas externas (ML, Facebook, Google)
CREATE TABLE IF NOT EXISTS VeiculoPublicacao (
    PubId INTEGER PRIMARY KEY AUTOINCREMENT,
    R_VeiId INTEGER NOT NULL,
    PubPlataforma TEXT NOT NULL,
    PubExternoId TEXT,
    PubStatus TEXT DEFAULT 'ATIVO',
    PubUrl TEXT,
    PubDtPublicacao TEXT,
    PubDtRemocao TEXT,
    FOREIGN KEY (R_VeiId) REFERENCES Veiculo(VeiId)
);

-- Novas colunas na tabela Lead (para solicitacoes de financiamento)
ALTER TABLE Lead ADD COLUMN LeaCpf TEXT;
ALTER TABLE Lead ADD COLUMN LeaRenda REAL;
ALTER TABLE Lead ADD COLUMN LeaEntrada REAL;
ALTER TABLE Lead ADD COLUMN LeaParcelas INTEGER;
```

### Verificacao:
- Acesse o sistema e abra a tela de **Lojas** → deve aparecer o campo "URL do Catalogo"
- Acesse **Integracoes** no menu → a pagina deve carregar sem erros
- Cadastre um lead via catalogo (Solicitar Analise de Credito) → deve salvar com os campos novos

---

## Passo 2: Testar Localmente

### 2.1 Subir o backend

```bash
cd back-end/ConnectVeiculos.API
dotnet run
```

### 2.2 Subir o frontend (modo desenvolvimento)

```bash
cd front-end/ConnectVeiculos.Web
ng serve
```

> **Importante:** Em desenvolvimento (`ng serve`), o SSR esta **desabilitado** (so ativa em build de producao). Isso evita problemas de timeout na renderizacao.

### 2.3 O que testar

| Funcionalidade | Como testar |
|----------------|-------------|
| Carrossel de imagens no catalogo | Abra o catalogo, clique em um veiculo com muitas imagens. As thumbnails devem ter setas |
| Imagem principal | Edite um veiculo, nas imagens clique na estrela de uma imagem. Ela deve ganhar o badge "Principal" |
| URL do catalogo | Edite uma loja, preencha o campo "URL do Catalogo". Salve e verifique se outra loja tambem recebeu a URL |
| Pagina de integracoes | Acesse /integracoes no menu. Deve mostrar cards do ML, Facebook e Google |
| Solicitar Analise de Credito | No catalogo, clique em um veiculo > Simular Financiamento > Solicitar Analise. Preencha o form |
| Lead no admin | Logue como admin > Leads > deve aparecer com origem "Solicitacao de Financiamento" |
| Feed Facebook | Acesse http://localhost:5219/api/feed/facebook no navegador. Deve retornar dados TSV |
| Feed Google | Acesse http://localhost:5219/api/feed/google no navegador. Deve retornar XML |

---

## Passo 3: Deploy do SSR (Server-Side Rendering)

O SSR e necessario para o site aparecer no Google. Sem ele, o Google nao consegue ler o conteudo do catalogo.

### 3.1 Build para producao

```bash
cd front-end/ConnectVeiculos.Web
ng build
```

Isso gera:
- `dist/connect-veiculos.web/browser/` → arquivos estaticos
- `dist/connect-veiculos.web/server/` → servidor Node.js

### 3.2 Configurar variaveis de ambiente no servidor

```bash
export PORT=4000
export API_BASE_URL=http://localhost:5219
export SITE_BASE_URL=https://connectveiculos.dev.br
```

Ajuste `API_BASE_URL` para a URL interna da API .NET no seu servidor Oracle Cloud.

### 3.3 Executar o servidor Node

```bash
node dist/connect-veiculos.web/server/server.mjs
```

### 3.4 Configurar proxy reverso (Nginx ou similar)

```nginx
server {
    listen 80;
    server_name connectveiculos.dev.br;

    location / {
        proxy_pass http://localhost:4000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### 3.5 Configurar HTTPS (Let's Encrypt)

```bash
sudo certbot --nginx -d connectveiculos.dev.br
```

### 3.6 Verificar se o SSR esta funcionando

```bash
curl https://connectveiculos.dev.br/catalogo
```

O HTML retornado deve conter os dados dos veiculos (nomes, precos) diretamente no codigo. Se aparecer apenas `<app-root></app-root>`, o SSR nao esta funcionando.

### 3.7 Manter o servidor rodando (PM2)

```bash
npm install -g pm2
pm2 start dist/connect-veiculos.web/server/server.mjs --name connect-ssr
pm2 save
pm2 startup
```

---

## Passo 4: Cadastrar no Google Search Console

Detalhes completos em [GUIA-SEO-GOOGLE.md](./GUIA-SEO-GOOGLE.md).

### Resumo:

1. Acesse: https://search.google.com/search-console
2. Adicione propriedade: `https://connectveiculos.dev.br`
3. Verifique a propriedade (meta tag em `src/index.html` ou arquivo HTML)
4. Submeta o sitemap: `sitemap.xml`
5. Use "Inspecionar URL" para solicitar indexacao das paginas principais

---

## Passo 5: Configurar Integracoes (ML, Facebook, Google)

> Detalhes completos em [GUIA-INTEGRACOES.md](./GUIA-INTEGRACOES.md).

### Ordem recomendada:

#### 5.1 Mercado Livre (FACA PRIMEIRO - mais facil)
- **Tempo:** ~10 min
- **Custo:** Gratis (taxa apenas em vendas pelo ML)
- **Requisitos:** Conta ML
- **Funciona em local:** Sim
- **Resultado:** Veiculos publicados/removidos automaticamente no ML

#### 5.2 Facebook Marketplace / Instagram
- **Tempo:** ~15-30 min
- **Custo:** Gratis (organico). Pago se quiser Ads.
- **Requisitos:** Pagina Facebook + Conta Meta Business
- **Funciona em local:** Nao (precisa de URL publica)
- **Resultado:** Veiculos no Marketplace e Instagram Shopping

#### 5.3 Google Merchant / Vehicle Ads
- **Tempo:** ~30-60 min
- **Custo:** Gratis (listagem). Pago se quiser Vehicle Ads.
- **Requisitos:** Conta Google + verificacao de dominio
- **Funciona em local:** Nao (precisa de URL publica)
- **Resultado:** Veiculos no Google Shopping e Vehicle Ads

### Para todas:
1. Siga o passo-a-passo do [GUIA-INTEGRACOES.md](./GUIA-INTEGRACOES.md)
2. Gere as credenciais
3. Cole no `back-end/ConnectVeiculos.API/appsettings.json`
4. Reinicie o backend

---

## Passo 6: Financiamento com Bancos (DESATIVADO POR ENQUANTO)

> O modulo de financiamento real com bancos (BV, Pan) esta **comentado no menu e nas rotas**.
> Para reativar quando tiver parceria comercial, descomente:
> - `front-end/ConnectVeiculos.Web/src/app/layout/sidebar/sidebar.component.ts` (linha do menu)
> - `front-end/ConnectVeiculos.Web/src/app/app.routes.ts` (rota /financiamentos)

### Por que esta desativado:

Para usar a API real dos bancos, voce precisa de **parceria comercial** com cada banco:
- BV Financeira (https://www.bv.com.br/proximo-passo)
- Banco Pan (https://www.bancopan.com.br/atendimento/fale-com-o-pan)

Custo: zero para o desenvolvedor, mas exige aprovacao comercial e certificacao FEBRABAN/ANEPS.

### Solucao atual (sem custo):

Esta funcionando no catalogo o botao **"Solicitar Analise de Credito"** que captura os dados do cliente como **lead com origem FINANCIAMENTO**. O vendedor ve em **Leads** no admin e contata manualmente.

### Alternativas com custo (futuro):

Se quiser pagar uma plataforma agregadora (15+ bancos integrados):
- **Autoconf:** ~R$ 400-1.500/mes
- **FANDI:** ~R$ 800-3.000/mes
- **Smart Dealer:** ~R$ 200-600/mes

---

## Cronograma Sugerido

| Periodo | Tarefa |
|---------|--------|
| **Dia 1** | Rodar SQL de migracao + testar local |
| **Dia 2-3** | Deploy do SSR no servidor Oracle Cloud |
| **Dia 3** | Cadastrar no Google Search Console + submeter sitemap |
| **Semana 1** | Configurar Mercado Livre (passo 5.1 do GUIA-INTEGRACOES) |
| **Semana 1** | Configurar Facebook Catalog (passo 5.2) |
| **Semana 2** | Configurar Google Merchant (passo 5.3) |
| **Semana 3-4** | Acompanhar indexacao no Google (pode levar dias/semanas) |
| **Mes 2+** | (Opcional) Solicitar parceria com bancos para financiamento real |

---

## Resumo de Custos

| Item | Custo para voce (dev) | Custo para o cliente (loja) |
|------|----------------------|---------------------------|
| SSR / SEO | Gratis | Gratis |
| Google Search Console | Gratis | Gratis |
| Mercado Livre | Gratis | Taxa por venda no ML (~11-16%) |
| Facebook Catalog | Gratis | Gratis (organico) ou pago (Ads) |
| Google Merchant | Gratis | Gratis (listagem) ou pago (Ads, min ~R$5/dia) |
| Solicitacao de Credito (lead) | Gratis | Gratis |
| Financiamento bancos (correspondente) | Gratis | Banco paga comissao para a loja |
| Plataforma agregadora (opcional) | Gratis | R$ 200-3.000/mes (a depender do plano) |

---

## Arquivos de configuracao importantes

| Arquivo | O que configurar |
|---------|-----------------|
| `appsettings.json` | Credenciais ML, Facebook, Google (e BV/Pan se ativar financiamento) |
| `environment.prod.ts` | URL da API (ja configurado como `/api`) |
| `server.ts` | Variaveis de ambiente do SSR |
| `src/index.html` | Meta tag do Google Search Console |

---

## Contatos uteis

| Plataforma | Portal |
|------------|--------|
| Mercado Livre | https://developers.mercadolivre.com.br |
| Facebook Business | https://business.facebook.com |
| Meta for Developers | https://developers.facebook.com |
| Google Merchant | https://merchants.google.com |
| Google Cloud Console | https://console.cloud.google.com |
| Google Search Console | https://search.google.com/search-console |
| BV Financeira (parceria) | https://www.bv.com.br/proximo-passo |
| Banco Pan (parceria) | https://www.bancopan.com.br/atendimento/fale-com-o-pan |

---

## Status atual do projeto

| Funcionalidade | Status | Observacao |
|----------------|--------|-----------|
| Catalogo publico com SEO | Pronto | Precisa deploy SSR para indexar |
| Carrossel de imagens (com setas) | Pronto | |
| Imagem principal selecionavel | Pronto | |
| URL do catalogo unica entre lojas | Pronto | |
| Mercado Livre (publicar/remover automatico) | Pronto | Falta credenciais |
| Facebook Catalog (feed XML) | Pronto | Falta deploy publico |
| Facebook Catalog (push instantaneo) | Pronto | Falta credenciais |
| Google Merchant (feed XML) | Pronto | Falta deploy publico |
| Google Merchant (push instantaneo) | Pronto | Falta credenciais |
| Solicitacao de Credito no catalogo | Pronto | Funciona local |
| Financiamento real com bancos | Desativado | Requer parceria comercial |
| Sitemap dinamico | Pronto | |
| robots.txt | Pronto | |
| Tokens persistidos no banco | Pronto | |
