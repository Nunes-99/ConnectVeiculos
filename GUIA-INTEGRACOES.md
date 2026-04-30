# Guia de Configuracao das Integracoes

Passo-a-passo para conectar Mercado Livre, Facebook e Google ao sistema ConnectVeiculos.

> **Onde colocar as credenciais:**
> Arquivo: `back-end/ConnectVeiculos.API/appsettings.json`
> Apos qualquer alteracao no arquivo, **reinicie o backend** para aplicar.

---

# 1. MERCADO LIVRE

**Tempo:** ~10 minutos (uma unica vez no setup do produto)
**Custo:** Gratis (taxa apenas em vendas pelo ML)
**Resultado:** Anuncios criados/removidos automaticamente

> ## Importante: dois "personagens" diferentes nessa integracao
>
> 1. **DEV (quem mantem o ConnectVeiculos)** — cria UMA app no portal
>    de devs do ML uma unica vez. Essa app e so uma "ponte tecnica":
>    nao publica nada, nao possui anuncios, nao custa nada. Existe so para
>    pedir autorizacao OAuth aos vendedores reais. **Ja foi criada e o
>    Client ID esta em `appsettings.Development.json`** — voce nao precisa
>    refazer.
>
> 2. **DONO DA LOJA (cliente final que usa o ConnectVeiculos)** — clica em
>    "Conectar Mercado Livre" no admin uma unica vez, faz login com a propria
>    conta ML e autoriza. **Os anuncios sao publicados na conta DELE, em
>    nome DELE, com taxas/recebimentos para a conta DELE.** Pode trocar de
>    conta a qualquer momento via botao "Trocar conta" / "Desconectar".
>
> A app do dev e o token do dono sao independentes. O dev nao ve dados
> nem anuncios da loja — apenas habilita a integracao tecnica.
>
> Modelo: **uma conta ML por instalacao** (mesmo que a instalacao tenha
> varias lojas). Se precisar de uma conta ML por loja no futuro, vai
> requerer refatoracao para guardar tokens por R_LojId.

## 1.1 Criar conta de desenvolvedor (so o DEV faz isso, uma vez)

1. Acesse: https://developers.mercadolivre.com.br
2. Clique em **"Entrar"** no canto superior direito
3. Faca login com a conta Mercado Livre do desenvolvedor
4. Aceite os termos de desenvolvedor

## 1.2 Criar a aplicacao

1. No menu superior, clique em **"Suas aplicacoes"**
2. Clique em **"Criar nova aplicacao"**
3. Preencha:
   - **Nome da aplicacao:** `Diamante Veiculos - ConnectVeiculos`
   - **Descricao curta:** `Sistema de gestao de catalogo de veiculos`
   - **Descricao longa:** `Integracao para publicacao automatica de veiculos disponiveis no catalogo da loja`
   - **URL do site:** `https://connectveiculos.dev.br` (ou `http://localhost:4200` para teste)
   - **Redirect URI:** `http://localhost:5219/api/integracoes/mercadolivre/callback`
     > Quando publicar em producao, troque para: `https://connectveiculos.dev.br/api/integracoes/mercadolivre/callback`
   - **Topicos de notificacao:** marque `items` e `orders_v2`
   - **Escopos:** marque `read`, `write`, `offline_access`
4. Clique em **"Criar"**

## 1.3 Copiar credenciais

Apos criar, voce vera duas informacoes importantes:

- **App ID:** numero longo (ex: 1234567890123456)
- **Client Secret:** string aleatoria (ex: aBcDeFg123HiJkL456...)

**Copie ambos** - voce vai precisar agora.

## 1.4 Configurar no sistema

1. Abra o arquivo: `back-end/ConnectVeiculos.API/appsettings.json`
2. Localize a secao `MercadoLivreSettings`
3. Preencha:
```json
"MercadoLivreSettings": {
    "AppId": "COLE_O_APP_ID_AQUI",
    "ClientSecret": "COLE_O_CLIENT_SECRET_AQUI",
    "RedirectUri": "http://localhost:5219/api/integracoes/mercadolivre/callback",
    "AccessToken": "",
    "RefreshToken": "",
    "UserId": ""
}
```
4. **Reinicie o backend** (`Ctrl+C` no terminal e rode `dotnet run` novamente)

## 1.5 Conectar a conta

1. No sistema, faca login como Administrador
2. Va em **Integracoes** no menu lateral
3. No card **"Mercado Livre"**, clique em **"Conectar Mercado Livre"**
4. Abre um popup do ML pedindo autorizacao
5. Clique em **"Autorizar"**
6. O popup fecha sozinho e o card mostra **"Conectado"**

## 1.6 Testar

1. Va em **Veiculos** no sistema
2. Cadastre um veiculo novo com status **"Disponivel"**
3. Aguarde ~5 segundos
4. Acesse sua conta no app/site Mercado Livre → **"Vendas"** → **"Anuncios"**
5. O veiculo deve aparecer la automaticamente

## 1.7 Pronto

A partir de agora:
- Veiculo cadastrado "Disponivel" → publica no ML
- Veiculo "Vendido" / "Reservado" → remove do ML
- Veiculo inativado → remove do ML

---

# 2. FACEBOOK MARKETPLACE / INSTAGRAM

**Tempo:** ~15-30 minutos
**Custo:** Gratis (organico). Pago se quiser Ads.
**Resultado:** Veiculos aparecem no Marketplace e Instagram Shopping

> ## Importante: dois "personagens" diferentes
>
> 1. **DEV (quem mantem o ConnectVeiculos)** — pode ja ter criado uma app no portal de devs do Meta para usar a Marketing API. Nao precisa fazer nada para sua loja.
>
> 2. **DONO DA LOJA (cliente final)** — voce e o dono da Pagina do Facebook, do Catalogo e da conta Meta Business. **Os veiculos serao publicados no SEU Catalogo, sob a SUA Pagina, com a SUA marca.**
>
> A app do dev (se existir) e a sua conta Meta Business sao independentes. O dev nao ve seus dados — apenas pode habilitar a integracao tecnica via Marketing API.

## 2.1 Criar conta Meta Business

1. Acesse: https://business.facebook.com
2. Clique em **"Criar conta"** (se nao tem) ou faca login
3. Preencha:
   - Nome da empresa: nome da sua loja
   - Seu nome
   - Email de trabalho
4. Confirme o email

## 2.2 Conectar a Pagina do Facebook

1. No Meta Business, va em **"Configuracoes"** → **"Contas"** → **"Paginas"**
2. Clique em **"Adicionar"** → **"Adicionar uma pagina"**
3. Selecione a pagina da sua loja no Facebook (se nao tem, crie uma em facebook.com/pages/create)
4. Confirme

## 2.3 Criar o Catalogo de Veiculos

1. No Meta Business, va em **"Commerce Manager"** (ou acesse https://business.facebook.com/commerce)
2. Clique em **"Criar catalogo"**
3. Tipo: selecione **"Veiculos"**
4. Configure:
   - Nome: `<Nome da sua loja> - Catalogo`
   - Conta de negocios: selecione a conta criada
5. Clique em **"Criar"**

## 2.4 Adicionar fonte de dados (Feed)

1. Dentro do catalogo criado, va em **"Catalogo"** → **"Itens"** → **"Adicionar itens"**
2. Escolha **"Feed de dados"** → **"Feed agendado"**
3. Configure:
   - URL: `https://connectveiculos.dev.br/api/feed/facebook` (use a URL publica)
   - Frequencia: **A cada hora** (ou diaria)
   - Encoding: **UTF-8**
   - Delimitador: **Tab**
4. Clique em **"Iniciar carregamento"**
5. Aguarde processamento (alguns minutos)

> **Para teste local:** o Facebook nao consegue acessar `localhost`. Voce precisa publicar o backend em uma URL acessivel da internet (Oracle Cloud, ngrok, etc.) antes de configurar o feed.

## 2.5 Obter Access Token PERMANENTE (System User Token)

> Esta etapa e **opcional** - sem ela, o feed XML ainda funciona (atualizacao a cada 1h em vez de instantanea). Mas se for fazer, **use System User Token** (nao expira) em vez do token temporario do Graph Explorer (expira em 1-2h e quebra a integracao).

### 2.5.1 Criar app Meta tipo Business

1. Acesse: https://developers.facebook.com/apps
2. Clique em **"Meus apps"** → **"Criar aplicativo"**
3. Tipo: **"Empresa"** → **Avancar**
4. Configure:
   - Nome: `ConnectVeiculos Catalog` (so voce ve, qualquer nome serve)
   - Email de contato
   - Selecione sua conta Meta Business criada na 2.1
5. No painel do app, va em **"Adicionar produtos"** → **"Marketing API"** → **"Configurar"**

### 2.5.2 Criar System User na sua conta Meta Business

> System User e um "usuario do sistema" que serve so pra integracoes — token nao expira.

1. Acesse https://business.facebook.com → **Configuracoes**
2. **Usuarios** → **Usuarios do sistema** → **"Adicionar"**
3. Nome: `ConnectVeiculos System User`
4. Funcao: **Admin**
5. Salve

### 2.5.3 Conceder acesso ao System User aos ativos

1. Ainda em **Usuarios do sistema**, clique no usuario criado
2. Clique em **"Adicionar ativos"**
3. Selecione 3 tipos:
   - **Apps**: marque o app criado em 2.5.1
   - **Catalogos**: marque o catalogo criado em 2.3
   - **Paginas**: marque sua Pagina conectada em 2.2
4. Em cada um, marque permissao **"Controle total"**
5. Salve

### 2.5.4 Gerar o token permanente

1. Ainda no System User, clique em **"Gerar token"**
2. Selecione o app criado em 2.5.1
3. Validade: **Nunca** (System User Tokens nao expiram, e isso que queremos)
4. Marque os escopos:
   - `catalog_management` (obrigatorio)
   - `business_management` (obrigatorio)
   - `pages_manage_metadata` (opcional, para postagem na Pagina)
5. Clique em **"Gerar token"**
6. **Copie o token agora** (comeca com `EAA...`) — nao da pra ver depois. Guarde em local seguro.

## 2.6 Obter o Catalog ID

1. No Commerce Manager, dentro do catalogo
2. Va em **"Configuracoes"** → **"Detalhes do catalogo"**
3. Copie o **ID do catalogo** (numero longo)

## 2.7 Configurar no sistema

> **Atencao:** ao contrario de Mercado Livre, WhatsApp e SMTP (que tem UI no admin), Facebook ainda exige editar arquivo no servidor. Isso vai mudar em uma proxima versao do produto. Se voce nao tem acesso SSH, peca para quem cuida do deploy.

**Opcao A — via env vars no `.env` da VM (recomendado em producao):**

```bash
# SSH na VM
ssh -i sua-chave.key ubuntu@SEU_IP

# Editar .env
cd ~/ConnectVeiculos
nano .env
```

Adicione/edite as 2 linhas:
```
FB_ACCESS_TOKEN=EAAxxxxxxxxxxxxx (token permanente do passo 2.5.4)
FB_CATALOG_ID=123456789012345 (ID do passo 2.6)
```

Reinicie:
```bash
sudo docker compose restart backend-cliente
```

**Opcao B — via `appsettings.json` (so em desenvolvimento local):**

1. Abra: `back-end/ConnectVeiculos.API/appsettings.Development.json`
2. Adicione:
```json
"FacebookCatalogSettings": {
    "AccessToken": "EAAxxxxxxxxxxxxx",
    "CatalogId": "123456789012345",
    "ApiVersion": "v18.0"
}
```
3. **Reinicie o backend**

## 2.8 Testar

1. Cadastre um veiculo novo no sistema
2. Aguarde ~10 segundos
3. No Commerce Manager → Catalogo → Itens, o veiculo deve aparecer

## 2.9 Anuncios pagos (opcional)

Para criar anuncios pagos do veiculo:
1. No Meta Business, va em **"Gerenciador de Anuncios"**
2. Crie campanha **"Vendas"** → **"Catalogo"**
3. Selecione o catalogo criado
4. Defina orcamento, publico, segmentacao
5. Os anuncios aparecem no Facebook, Instagram e Marketplace

---

# 3. GOOGLE MERCHANT CENTER / VEHICLE ADS

**Tempo:** ~30-60 minutos
**Custo:** Gratis (listagem). Pago se quiser Vehicle Ads.
**Resultado:** Veiculos no Google Shopping e Vehicle Ads

> ## Importante: dois "personagens" diferentes
>
> 1. **DEV (quem mantem o ConnectVeiculos)** — pode plugar credenciais do Cloud Console em uma proxima versao via UI. Por enquanto so via SSH no servidor.
>
> 2. **DONO DA LOJA (cliente final)** — voce e o dono da conta Google, do Merchant Center, do projeto Cloud e do dominio verificado. Voce paga o Google Ads se ativar (anuncios pagos sao opcionais; listagem e gratis).
>
> Os custos do Google Ads (se ativados) sao cobrados no cartao da SUA conta Google, nunca do dev.

## 3.1 Criar Google Merchant Center

1. Acesse: https://merchants.google.com
2. Faca login com conta Google da empresa
3. Clique em **"Comecar"**
4. Preencha:
   - Pais: **Brasil**
   - Fuso horario: **America/Sao_Paulo**
   - Moeda: **BRL**
   - Nome da empresa: nome da sua loja
   - Site: `https://connectveiculos.dev.br`
5. Aceite os termos

## 3.2 Verificar o site

1. No Merchant Center, va em **"Ferramentas e configuracoes"** → **"Informacoes da empresa"** → **"Site"**
2. Insira: `https://connectveiculos.dev.br`
3. Escolha um metodo de verificacao:
   - **Tag HTML** (mais facil): copie a meta tag e adicione em `src/index.html` dentro de `<head>`
   - **Arquivo HTML:** baixe e coloque em `public/`
   - **Google Analytics:** se ja tiver
4. Faca build e deploy do frontend
5. Volte ao Merchant Center e clique em **"Verificar"**

## 3.3 Adicionar feed XML

1. No Merchant Center, va em **"Produtos"** → **"Feeds"**
2. Clique no botao **"+"**
3. Configure:
   - Pais: **Brasil**, Idioma: **Portugues**
   - Destino: **Anuncios do Shopping** + **Listagens gratis**
   - Tipo de feed: **Programacao de busca**
   - Nome: nome da sua loja
   - URL: `https://connectveiculos.dev.br/api/feed/google`
   - Frequencia: **Diaria** ou **a cada hora**
4. Clique em **"Criar feed"**
5. Aguarde processamento

## 3.4 Obter credenciais para push instantaneo (opcional)

> Esta parte e mais tecnica. Se quiser pular, o feed XML ja funciona.

### 3.4.1 Criar projeto no Google Cloud

1. Acesse: https://console.cloud.google.com
2. Clique em **"Criar projeto"**
3. Nome: `connectveiculos-merchant`
4. Selecione o projeto criado

### 3.4.2 Habilitar a Content API

1. No menu, va em **"APIs e servicos"** → **"Biblioteca"**
2. Pesquise: **"Content API for Shopping"**
3. Clique em **"Habilitar"**

### 3.4.3 Criar credenciais OAuth

1. Va em **"APIs e servicos"** → **"Tela de consentimento OAuth"**
2. Tipo: **"Externo"**
3. Preencha as informacoes da app
4. Adicione escopo: `https://www.googleapis.com/auth/content`
5. Salve
6. Va em **"Credenciais"** → **"Criar credenciais"** → **"ID do cliente OAuth"**
7. Tipo: **"Aplicativo da Web"**
8. URIs de redirecionamento autorizados:
   - `https://developers.google.com/oauthplayground`
9. Crie e copie:
   - **Client ID**
   - **Client Secret**

### 3.4.4 Gerar Refresh Token

1. Acesse: https://developers.google.com/oauthplayground
2. Clique na engrenagem (canto superior direito)
3. Marque **"Use your own OAuth credentials"**
4. Cole **Client ID** e **Client Secret**
5. No campo "Input your own scopes", cole: `https://www.googleapis.com/auth/content`
6. Clique em **"Authorize APIs"**
7. Faca login com a conta Google do Merchant Center
8. Clique em **"Exchange authorization code for tokens"**
9. Copie o **Refresh Token** (string longa)

### 3.4.5 Obter Merchant ID

1. No Google Merchant Center, no canto superior direito
2. Logo abaixo do nome da empresa, aparece o ID (numero)
3. Copie esse numero

## 3.5 Configurar no sistema

> **Atencao:** ao contrario de Mercado Livre, WhatsApp e SMTP, Google ainda exige editar arquivo no servidor. Isso vai mudar em uma proxima versao do produto. Se voce nao tem acesso SSH, peca para quem cuida do deploy.

**Opcao A — via env vars no `.env` da VM (recomendado em producao):**

```bash
# SSH na VM
ssh -i sua-chave.key ubuntu@SEU_IP
cd ~/ConnectVeiculos
nano .env
```

Adicione:
```
GOOGLE_MERCHANT_CLIENT_ID=xxx.apps.googleusercontent.com (passo 3.4.3)
GOOGLE_MERCHANT_CLIENT_SECRET=GOCSPX-xxxxxxxxxxxxx (passo 3.4.3)
GOOGLE_MERCHANT_REFRESH_TOKEN=1//xxxxxxxxxxxxx (passo 3.4.4)
GOOGLE_MERCHANT_ID=123456789 (passo 3.4.5)
```

Reinicie:
```bash
sudo docker compose restart backend-cliente
```

**Opcao B — via `appsettings.json` (so em desenvolvimento local):**

1. Abra: `back-end/ConnectVeiculos.API/appsettings.Development.json`
2. Adicione:
```json
"GoogleMerchantSettings": {
    "AccessToken": "",
    "RefreshToken": "1//xxxxxxxxxxxxx",
    "MerchantId": "123456789",
    "ClientId": "xxx.apps.googleusercontent.com",
    "ClientSecret": "GOCSPX-xxxxxxxxxxxxx"
}
```
3. **Reinicie o backend**

## 3.6 Testar

1. Cadastre um veiculo no sistema
2. Aguarde ~10 segundos
3. No Merchant Center, va em **"Produtos"** → **"Todos os produtos"**
4. O veiculo deve aparecer

## 3.7 Vehicle Ads (opcional)

Para criar Google Vehicle Ads:

1. Vincule o Merchant Center a uma conta Google Ads:
   - Merchant Center → **"Configuracoes"** → **"Contas vinculadas"** → **"Google Ads"**
2. No Google Ads:
   - Nova campanha → tipo **"Performance Max"**
   - Vincule o feed do Merchant Center
   - Configure orcamento (min ~R$5/dia)
   - Os anuncios sao gerados automaticamente

---

# CHECKLIST FINAL

Apos concluir os passos:

- [ ] Mercado Livre conectado (status "Conectado" em Integracoes)
- [ ] Facebook Catalog criado e feed configurado
- [ ] Google Merchant Center verificado e feed configurado
- [ ] `appsettings.json` preenchido com todas as credenciais
- [ ] Backend reiniciado
- [ ] Cadastrou um veiculo de teste
- [ ] Veiculo apareceu nas 3 plataformas

---

# CUSTOS RESUMIDOS

| Plataforma | Setup | Mensalidade | Por venda |
|-----------|-------|-------------|-----------|
| Mercado Livre | Gratis | Gratis | Taxa do ML em vendas pela plataforma (~11-16%) |
| Facebook Catalog | Gratis | Gratis | Gratis (organico) |
| Facebook Ads | Gratis | Voce define orcamento | Por clique/visualizacao |
| Google Merchant | Gratis | Gratis | Gratis (organico) |
| Google Vehicle Ads | Gratis | Min ~R$5/dia | Por clique |

---

# DUVIDAS COMUNS

**P: Posso testar com `localhost` no Facebook/Google?**
R: Nao. O Facebook/Google precisam acessar a URL publicamente. Use Oracle Cloud, ngrok ou similar para expor o sistema.

**P: O Mercado Livre funciona com localhost?**
R: Para o OAuth, precisa de URL acessivel pelo navegador (entao localhost funciona se voce estiver acessando localmente). Para producao, troque para a URL publica.

**P: Os tokens expiram?**
R: Sim:
- ML: access_token (6 horas) + refresh_token (6 meses) - sistema renova sozinho
- Facebook: access_token de longa duracao (60 dias) - precisa renovar manualmente. Para sistema permanente: usar System User
- Google: access_token (1 hora) + refresh_token (sem expiracao) - sistema renova sozinho

**P: Quantos veiculos cabem em cada plataforma?**
R:
- Mercado Livre: limite varia por reputacao/plano (geralmente 50+)
- Facebook: ate 1.000.000 produtos por catalogo
- Google: ate 1.000.000 produtos

**P: Quando me avisar que esta tudo pronto?**
R: Apos preencher as credenciais no `appsettings.json` e reiniciar o backend, me chame. Vou testar com voce cadastrando um veiculo e verificando que aparece em todas as plataformas.

---

# 4. WHATSAPP BUSINESS CLOUD API

**Tempo:** ~30-60 min (alguns passos sao automaticos, outros sao espera de aprovacao do Meta)
**Custo:** Gratis para 1.000 conversas iniciadas pelo cliente por mes. Acima disso: ~R$ 0,06/conversa (utility) ou ~R$ 0,30/conversa (marketing). Voce paga direto ao Meta com o cartao da sua conta Business.
**Resultado:**
- Mensagens recebidas no seu numero WhatsApp Business viram Leads automaticamente
- Voce pode responder do admin e disparar templates (mensagens prontas aprovadas pelo Meta)

> ## Diferenca importante vs Mercado Livre
> No ML, o desenvolvedor cria UMA app e voce so loga via OAuth.
> No WhatsApp **cada cliente cria a propria estrutura no Meta**, porque o numero de telefone e identidade central da plataforma. Voce e o "dono" da conta Meta — paga, gerencia, recebe.

## 4.0 Pre-requisitos

- Um **numero de telefone empresarial** (celular ou fixo) que **NAO** esteja em uso no WhatsApp comum (vai ser dedicado ao Business). Pode ser um chip novo ou um numero VoIP dedicado.
- Um **cartao de credito** (Meta vai cobrar conversas excedentes — voce define o limite mensal no painel)
- Conta Facebook pessoal ativa

## 4.1 Criar Meta Business Manager

1. Acesse https://business.facebook.com
2. Faca login com sua conta Facebook pessoal
3. Clique em **"Criar conta"** — preencha nome do negocio, seu nome e e-mail empresarial
4. Aceite os termos. Pronto, voce tem o **Business Manager** (gratis, 5 min)

## 4.2 Criar WhatsApp Business Account (WABA)

1. No Business Manager, va em **Configuracoes > Contas > Contas do WhatsApp**
2. Clique em **"Adicionar"** > **"Criar nova conta do WhatsApp Business"**
3. De um nome (ex: "Diamante Veiculos WABA")
4. Selecione um Time zone e moeda

## 4.3 Cadastrar e verificar numero de telefone

1. Dentro da WABA, clique em **"Adicionar numero de telefone"**
2. Digite o numero empresarial (com DDD, ex: 11 99999-9999)
3. Escolha **SMS** ou **chamada** para receber o codigo de verificacao
4. Digite o codigo de 6 digitos quando receber

> **Aviso:** Apos verificar, esse numero **nao podera mais ser usado no WhatsApp comum** ate 6 meses depois (regra do Meta). Use um chip dedicado.

## 4.4 Criar app Meta tipo Business

1. Acesse https://developers.facebook.com/apps
2. Clique em **"Criar app"** > tipo **"Business"** > **"Avancar"**
3. Nome da app: ex `ConnectVeiculos WhatsApp` (so voce ve)
4. E-mail de contato + selecione o Business Manager criado no passo 4.1
5. Apos criar, no painel da app, em **"Adicionar produtos"**, clique em **"Configurar"** dentro do card **WhatsApp**
6. Selecione a WABA do passo 4.2 e o numero do passo 4.3

## 4.5 Gerar System User Token permanente

> O token de teste padrao expira em 24h. Para producao, voce precisa de um **System User Token** que nao expira.

1. No Business Manager > **Configuracoes > Usuarios > Usuarios do sistema**
2. Clique **"Adicionar"** > nome (ex: `ConnectVeiculos System User`) > funcao **Admin**
3. Apos criar, clique no usuario > **"Adicionar ativos"** > selecione a app criada no passo 4.4 e a WABA do passo 4.2
4. Clique em **"Gerar token"**
5. Selecione a app > escopos: marque **`whatsapp_business_messaging`** e **`whatsapp_business_management`**
6. Validade: **Nunca** (System User Token nao expira)
7. Gere e **copie o token agora** — nao da pra ver depois. Guarde em local seguro.

## 4.6 Submeter templates de mensagem

> Templates sao mensagens pre-aprovadas pelo Meta usadas para iniciar conversa fora da janela de 24h.

1. No painel da app > **WhatsApp > Modelos de mensagem** > **"Criar modelo"**
2. Sugestoes minimas:
   - **`lead_recebido`** (categoria UTILITY, idioma `pt_BR`):
     ```
     Ola {{1}}! Recebemos seu interesse no veiculo {{2}}. Em breve um vendedor entrara em contato.
     ```
   - **`testdrive_confirmado`** (categoria UTILITY):
     ```
     Ola {{1}}, seu test drive do {{2}} esta confirmado para {{3}} as {{4}}. Endereco: {{5}}. Ate la!
     ```
3. Submeta para aprovacao. Espera tipica: **24h**. Se reprovar, ajusta texto e reenvia.

## 4.7 Configurar webhook + colar credenciais no admin

1. **No admin do ConnectVeiculos** (logado como Administrador ou Gerente):
   - Va em **Sistema > Integracoes**
   - No card **WhatsApp Business**, clique em **"Configurar"**
   - Na aba **"Como configurar"**, copie a **URL do webhook** que aparece (ex: `https://connectveiculos.dev.br/api/integracoes/whatsapp/webhook`)
   - Volte na aba **"Colar credenciais"** e digite um **Verify Token** (qualquer string secreta, ex: `connectveiculos-verify-2026`). **Anote esse valor.**

2. **No painel Meta** (developers.facebook.com > sua app > WhatsApp > Configuracao):
   - **URL de retorno de chamada:** cole a URL do webhook
   - **Token de verificacao:** cole o mesmo Verify Token que voce digitou no admin
   - Clique em **"Verificar e salvar"** — Meta vai chamar o webhook e validar
   - Em **"Campos do webhook"**, ative **`messages`**

3. **Volte no admin do ConnectVeiculos:**
   - Cole o **Access Token** do passo 4.5
   - Cole o **Phone Number ID** (aparece em WhatsApp > Configuracao da API > "Identificacao do numero de telefone")
   - Cole o **Verify Token** (mesmo do passo 1)
   - Clique em **"Salvar credenciais"**

## 4.8 Testar

1. Pegue outro celular e mande uma mensagem WhatsApp para o numero empresarial cadastrado (ex: "Tenho interesse no Civic 2023")
2. No admin do ConnectVeiculos > **Captacao de Clientes** (Leads), aparece um lead novo com origem **WHATSAPP**, telefone do remetente e a mensagem na observacao
3. **Pronto!** A integracao esta funcionando.

## 4.9 Avisos importantes

- **Janela de 24h:** voce so pode mandar mensagem **de texto livre** (via funcao "Enviar mensagem" no admin) dentro de 24h apos o cliente ter contatado. Fora dessa janela, somente templates aprovados.
- **Templates levam ~24h pra aprovar.** Se rejeitar, ajusta texto e reenvia.
- **Cobranca:** o Meta cobra direto no cartao da SUA conta Business Manager. Voce define limite mensal e recebe alertas.
- **Numero exclusivo:** o numero **nao pode** estar ativo no WhatsApp comum.
- **Quem pode configurar no admin:** apenas Administrador ou Gerente.
- **Trocar credenciais:** clique em "Reconfigurar" no card WhatsApp da pagina de Integracoes.
- **Desconectar:** clique em "Desconectar" — o sistema para de receber/enviar via WhatsApp ate ser reconectado. Mensagens ja recebidas continuam visiveis em Leads.

---

# 5. E-MAIL / SMTP

**Tempo:** ~10 minutos
**Custo:** Gratis com e-mail proprio (Locaweb, UOL, KingHost, Gmail). Pago apenas se usar servico dedicado tipo SendGrid (free tier 100 e-mails/dia).
**Resultado:**
- E-mails automaticos para clientes que favoritaram veiculos quando o preco cair
- Alertas de novo veiculo similar disponivel
- Recuperacao de senha do admin
- Confirmacao de venda

> ## Importante: o e-mail e da loja, nao da plataforma
> Quando um cliente recebe a notificacao "o preco do Civic 2023 baixou", o remetente deve ser **a sua loja** (`contato@diamanteveiculos.com.br`), nao a plataforma `connectveiculos.dev.br`. Razoes:
> 1. O cliente reconhece quem esta mandando — abre, clica, age
> 2. Reputacao do dominio e seu, nao da plataforma
> 3. Conformidade com LGPD (o "From" deve identificar quem se relaciona com o destinatario)

## 5.1 Cenario A — Voce ja tem e-mail empresarial proprio (mais comum)

Quase toda loja tem `contato@suaempresa.com.br` numa hospedagem (Locaweb, UOL Host, Hostgator, KingHost). Pegue as credenciais SMTP no painel da hospedagem.

**Locaweb (exemplo):**
- Servidor: `smtp.locaweb.com.br`
- Porta: 587 (TLS)
- Usuario: `contato@suaempresa.com.br`
- Senha: a mesma do e-mail

**UOL Host:**
- Servidor: `smtps.uhserver.com`
- Porta: 465 (SSL)

**KingHost:**
- Servidor: `smtp.kinghost.net`
- Porta: 587

Custo: **R$ 0** — ja esta pago dentro da hospedagem.

## 5.2 Cenario B — Gmail (pessoal ou Workspace)

Gmail nao aceita mais a senha normal pra SMTP — exige **App Password**:

1. Acesse https://myaccount.google.com/security
2. Em **"Como entrar no Google"**, ative a **"Verificacao em duas etapas"** (obrigatorio)
3. Acesse https://myaccount.google.com/apppasswords
4. Selecione **"Outro (nome personalizado)"** -> digite "ConnectVeiculos" -> **Gerar**
5. Copie a senha de 16 caracteres (`abcd efgh ijkl mnop`) — **so aparece uma vez**

Configuracao:
- Servidor: `smtp.gmail.com`
- Porta: 587 (TLS) ou 465 (SSL)
- Usuario: seu e-mail Gmail completo
- Senha: a App Password de 16 caracteres (sem os espacos)

Custo: **R$ 0** ate 500 destinatarios/dia.

## 5.3 Cenario C — Servico dedicado (alto volume ou marketing separado)

Para >500 e-mails/dia ou separar transacionais (recuperar senha) de marketing (preco caiu):

| Servico | Free tier | Pago a partir de |
|---------|-----------|-----------------|
| **SendGrid** | 100/dia | $19,95/mes (~R$ 100) = 50k |
| **Mailgun** | 100/dia (3 meses) | $35/mes (~R$ 175) = 50k |
| **AWS SES** | 200/dia (saida do EC2) | $0,10 / 1.000 e-mails |
| **Brevo** (ex-Sendinblue) | 300/dia | EUR 25/mes (~R$ 140) = 20k |

Configuracao similar — so muda o servidor SMTP. Para SendGrid:
- Servidor: `smtp.sendgrid.net`
- Porta: 587
- Usuario: literalmente `apikey` (e isso mesmo)
- Senha: a API Key gerada no painel SendGrid

## 5.4 Configurar no admin

1. Acesse **Sistema > Integracoes** (logado como Administrador ou Gerente)
2. No card **"E-mail / SMTP"**, clique em **"Configurar"**
3. Preencha os 7 campos:
   - **Servidor SMTP:** `smtp.locaweb.com.br` (ou `smtp.gmail.com` etc)
   - **Porta:** `587` (TLS) ou `465` (SSL)
   - **E-mail remetente:** `contato@suaempresa.com.br`
   - **Nome remetente:** `Sua Loja` (aparece como "De:" antes do e-mail)
   - **Usuario SMTP:** geralmente igual ao e-mail remetente (no SendGrid e literalmente `apikey`)
   - **Senha SMTP:** senha do e-mail ou App Password
   - **SSL/TLS:** marque (recomendado)
4. **Antes de salvar**, use a caixa **"Enviar e-mail de teste para:"**, coloque seu proprio e-mail e clique **"Testar"**. Se chegar (verifique tambem spam), credenciais estao corretas.
5. Clique em **Salvar**

## 5.5 Testar fluxo completo

1. Apos salvar, va em **Veiculos** no admin
2. Edite um veiculo qualquer e baixe o preco em mais de R$ 100 (ou >= 1%)
3. Veiculos que tem favoritos cadastrados (visitantes do catalogo publico) recebem o e-mail automatico

## 5.6 Avisos importantes

- **Quem paga:** voce, na sua hospedagem ou servico SMTP. A plataforma nao cobra nada por e-mail.
- **Quem pode configurar no admin:** apenas Administrador ou Gerente.
- **Senha em branco ao reconfigurar:** se ja tem credenciais salvas e voce so quer mudar o nome remetente, deixe a senha em branco — o sistema preserva a senha atual.
- **E-mail caindo em spam:** configure SPF, DKIM e DMARC no DNS do dominio (a hospedagem geralmente tem assistente). E-mail enviado por servidor desconhecido cai em spam.
- **Remetente diferente do dominio:** alguns provedores (Gmail principalmente) podem rejeitar e-mails onde o "From" e diferente do dominio do servidor SMTP. Use sempre um remetente do mesmo dominio que voce autenticou.
