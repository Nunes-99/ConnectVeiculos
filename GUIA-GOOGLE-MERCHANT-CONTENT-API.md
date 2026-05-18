# Guia passo-a-passo — Conectar Google Merchant (Content API)

Walkthrough da configuração da Content API for Shopping no ConnectVeiculos, baseado na nova interface **Google Auth Platform** (substituiu a antiga "Tela de consentimento OAuth").

**Tempo total:** ~30 min de configuração técnica + setup do Google Ads (cliente)
**Pré-requisito:** já ter o Google Merchant Center criado e o domínio reivindicado (`GUIA-INTEGRACOES.md` seção 3.1 e 3.2).
**Resultado:** veículos novos / editados / vendidos refletem no Google em segundos via push API.

> ## ⚠️ IMPORTANTE — Veículos exigem Vehicle Ads, não Shopping comum
>
> O Google Shopping **rejeita listagem de veículos motorizados** por política de conteúdo
> ("Não é permitido mostrar veículos motorizados destinados ao transporte de pessoas em vias públicas").
>
> Para anunciar carros, motos, caminhões, o caminho oficial é **Google Vehicle Ads** —
> que é parte do **Google Ads** (não do Shopping orgânico).
>
> O `GoogleMerchantService` deste sistema já envia o produto no formato Vehicle Listing:
> exclui Shopping_ads/Free_listings e popula os `customAttributes` `vehicle_*` necessários.
> Mas para o anúncio aparecer efetivamente, o cliente precisa concluir os passos da seção **9. Vehicle Ads** mais abaixo.

---

## 1. Criar projeto no Google Cloud

1. Acesse https://console.cloud.google.com
2. Ignore o banner "Teste grátis com US$ 300" — a Content API não exige billing
3. Clique em **"Selecione um projeto"** (topo) → **NOVO PROJETO**
4. Nome: `connectveiculos-merchant` (ou qualquer outro)
5. Criar
6. Selecione o projeto recém-criado

## 2. Ativar a Content API for Shopping

1. Menu ☰ → **APIs e serviços → Biblioteca**
2. Buscar: **`Content API for Shopping`** (cuidado para não digitar "shipping")
   - Se não achar, busque **`Merchant API`** (o Google está renomeando — escopo OAuth é o mesmo)
3. Clicar no resultado → **ATIVAR**
4. Aguardar a página recarregar mostrando o painel da API

## 3. Configurar o Google Auth Platform (consentimento OAuth)

1. Menu ☰ → **APIs e serviços → Tela de consentimento OAuth**
   - URL direta: `https://console.cloud.google.com/auth/overview`
2. Clicar em **"Vamos começar"** se for o primeiro acesso
3. Wizard inicial:
   - **App information:** Nome `ConnectVeiculos Merchant`, e-mail de suporte da própria conta
   - **Audiência:** marcar **Externo**
   - **Informações de contato:** mesmo e-mail
   - Concordar com a política → Criar

### 3.1 Adicionar o escopo da Content API

1. Menu lateral → **Acesso a dados**
2. **Adicionar ou remover escopos**
3. Filtrar por `content` → marcar `https://www.googleapis.com/auth/content`
4. **Atualizar** → **Salvar**

### 3.2 Adicionar usuário de teste

1. Menu lateral → **Público-alvo**
2. Seção **Usuários de teste** → **+ Adicionar usuários**
3. Inserir o e-mail da conta Google (a mesma que tem acesso ao Merchant Center) → **Salvar**

> O app fica em modo "Testing" — só usuários listados conseguem autorizar. Para produção, no final é possível publicar o app via botão **Publicar app** na Visão geral.

## 4. Criar o OAuth Client ID

1. Menu lateral → **Clientes**
2. **+ Criar cliente**
3. Preencher:
   - **Tipo de aplicativo:** **Aplicativo da Web**
   - **Nome:** `ConnectVeiculos OAuth Client`
   - **URIs de redirecionamento autorizados:** clicar em "+ Adicionar URI" e colar
     ```
     https://developers.google.com/oauthplayground
     ```
     (sem barra `/` no final)
   - **Origens JavaScript autorizadas:** **deixar vazio**
     > ⚠️ Gotcha: se colar o URL do playground neste campo, dá erro "Origem inválida: não é permitido que URIs de origem contenham um caminho". Esse campo só aceita origem (scheme + host). O URL do playground vai no campo **URIs de redirecionamento**, mais abaixo.
4. **Criar**
5. Copiar e guardar:
   - **Client ID** (`...apps.googleusercontent.com`)
   - **Client Secret** (`GOCSPX-...`)

## 5. Gerar Refresh Token no OAuth Playground

1. Abrir https://developers.google.com/oauthplayground em nova aba
2. Login com a mesma conta Google
3. Clicar na **engrenagem ⚙️** (canto superior direito)
4. Marcar **"Use your own OAuth credentials"**
5. Colar Client ID e Client Secret do passo 4
6. Fechar painel
7. No **Step 1** (lado esquerdo), no campo **"Input your own scopes"**, colar:
   ```
   https://www.googleapis.com/auth/content
   ```
8. **Authorize APIs**
9. Escolher a conta Google
10. Tela amarela **"O Google não verificou este app"**:
    - Clicar no link cinza **Continuar** (à esquerda do botão azul)
    - **NÃO** clicar em "Voltar à segurança" (botão azul) — esse cancela
    - Se em vez do botão Continuar aparecer apenas "Avançado", clicar e depois em **"Acessar ConnectVeiculos Merchant (não seguro)"**
11. Marcar todos os checkboxes de permissão → **Continuar**
12. Volta ao OAuth Playground com o **Authorization code** no Step 2
13. Clicar em **"Exchange authorization code for tokens"**
14. Copiar o **Refresh Token** (começa com `1//`) do campo abaixo do botão

> ⚠️ Se "Use your own OAuth credentials" não estiver marcado, o refresh token expira em 24h. Para produção precisa estar marcado.

## 6. Obter o Merchant ID

No Google Merchant Center, canto superior direito, abaixo do nome da empresa — é um número como `5788016203`. Copiar.

## 7. Colar no admin do ConnectVeiculos

1. Login no admin com perfil Administrador
2. **Integrações** (menu lateral)
3. Card **Google Merchant / Vehicle Ads** → **Configurar Content API**
4. Aba **"Colar credenciais"**
5. Preencher os 4 campos:

   | Campo | Origem |
   |---|---|
   | Merchant ID | passo 6 |
   | Client ID | passo 4 |
   | Client Secret | passo 4 |
   | Refresh Token | passo 5 |

6. **Testar agora** — deve retornar `Conta '<nome>' acessada com sucesso.`
7. **Salvar credenciais**

Badge do card deve mudar de "Não configurado" para "Configurado".

## 8. Testar push

1. Ir em **Veículos**
2. Cadastrar/editar um veículo com status **Disponível**, foto, marca, modelo, ano, KM, cor e preço
3. Aguardar ~10 segundos
4. No Merchant Center → **Produtos → Todos os produtos** — o veículo deve aparecer
   > Pode levar minutos extras para o Google processar imagens.

**Estados esperados após push:**
- **"Aguardando produtos do Google Ads"** ou status similar → push OK, mas Vehicle Ads ainda não está consumindo (veja seção 9)
- **"Em análise" / "Avaliação inicial pendente"** → revisão automática (até 3 dias úteis na primeira vez)
- **"Reprovado por política de Shopping"** → o build em produção ainda não tem a correção que exclui Shopping_ads. Deploy do backend.

## 9. Vehicle Ads — habilitar para o anúncio aparecer

O push do sistema deixa o produto **pronto** no Merchant Center, mas para realmente aparecer como anúncio, o cliente precisa:

### 9.1 Habilitar Vehicle Ads no Merchant Center

1. https://merchants.google.com com a conta do passo 6
2. No menu lateral, procurar **Vehicle ads** ou **Veículos** (pode estar em **Marketing** ou **Complementos**)
3. Se o programa não aparecer, talvez a conta precise solicitar acesso (Brasil tem disponibilidade limitada)
4. Aceitar políticas + termos

### 9.2 Vincular Google Ads

1. Merchant Center → **Configurações → Contas vinculadas → Google Ads**
2. Vincular com a mesma conta Google ou Google Ads pré-existente

### 9.3 Criar conta no Google Ads (se não tiver)

1. https://ads.google.com com a mesma conta Google do Merchant
2. Configurar billing (cartão de crédito) — Vehicle Ads é pago

### 9.4 Criar campanha Performance Max for Vehicle Ads

1. No Google Ads, **Nova campanha**
2. Tipo: **Performance Max**
3. Selecionar destino **Vehicle Ads** (aparece se o feed de veículos foi detectado no Merchant)
4. Vincular ao Merchant Center
5. Configurar:
   - Orçamento diário (mín ~R$5)
   - Localização (Brasil ou cidades específicas)
   - Idioma (Português)
   - Recursos da campanha (logos, textos, etc.)
6. Iniciar campanha

### 9.5 Aguardar aprovação

- Aprovação de produtos individuais: até 24h
- Aprovação da campanha: até 3 dias úteis na primeira vez

Após aprovação, os veículos do tenant aparecem em pesquisas do Google tipo "civic 2016 usado em São Paulo", "carros usados na concessionária X", etc.

---

## Notas e gotchas

- **Erros do SignalR no console do navegador** (`/hubs/notificacoes` 401) durante o teste do Merchant são de outra feature (notificações em tempo real) e não afetam o Google. O que conta é o toast verde da chamada do teste.
- **Devtools mostrando "No data found for resource with given identifier"**: o painel "Resposta" só captura o body se DevTools estava aberto no momento do request. Abrir DevTools antes de clicar em "Testar agora" se quiser inspecionar.
- **Segurança:** Client Secret e Refresh Token concedem acesso total ao Merchant Center. Tratar como senha — não commitar, não compartilhar em chat público. Para revogar: Google Cloud → Clientes → resetar Secret; ou https://myaccount.google.com/permissions → revogar acesso do app.
- **Precedência de configuração no backend:** env var > banco (UI) > appsettings.json. Se você salvou via UI mas exporta a env var, a env var ganha.

## Solução de problemas

| Sintoma | Causa provável | Solução |
|---|---|---|
| "Testar agora" retorna 401/invalid_grant | Refresh token revogado ou de conta diferente do Merchant ID | Refazer passo 5 com a conta certa |
| "Testar agora" retorna 403 | Conta Google não tem permissão no Merchant Center | Adicionar a conta como usuário no Merchant Center (Configurações → Acesso e serviços) |
| Veículo não aparece no Merchant após ~1 min | Falta campo obrigatório (imagem, categoria, preço) | Ver erros do produto no Merchant → Produtos → Diagnóstico |
| Refresh token expira em 24h | "Use your own OAuth credentials" não estava marcado no Playground | Refazer passo 5 marcando a opção |
| Produto reprovado por "venda de veículos não permitida" | Build em produção ainda usa o payload Shopping antigo, sem `excludedDestinations` | Deploy do backend com o `GoogleMerchantService` atual |
| Produto OK no Merchant mas não aparece em pesquisas do Google | Falta campanha Performance Max for Vehicle Ads no Google Ads vinculado | Seção 9 — criar campanha e configurar orçamento |
| Mileage/year não está sendo lido | `customAttributes` com nomes errados na payload | Verificar `GoogleMerchantService.PublicarVeiculoAsync` — usa `vehicle_make`, `vehicle_model`, `vehicle_model_year`, `vehicle_condition`, `vehicle_color`, `mileage_value`, `mileage_unit`, `vehicle_id` |
