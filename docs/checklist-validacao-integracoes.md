# Checklist de validação das integrações

Levantado em 2026-09-14, depois de uma rodada de correções em Mercado Livre,
Meta, e-mail e SEO.

Cada item é um comportamento que **precisa ser visto funcionando em produção**.
Compilar e passar nos testes não conta como validado aqui — vários dos bugs
desta rodada passavam nos dois.

Legenda: `[x]` visto funcionando · `[ ]` nunca exercitado · `[!]` sabidamente
quebrado ou impossível

---

## 1. Mercado Livre

> **Resolvido em 18/09/2026** — ver a seção 4.1. O texto abaixo é o histórico
> da investigação, de quando a conta caía a cada 6 horas.

A conta cai a cada 6 horas: a aplicação `7837357995078436` não recebe
`refresh_token`. **Chamado aberto em 16/09/2026 — protocolo 482724079**,
escalado para a equipe de Integrações do Mercado Livre.

O que ficou provado em 16/09, depois de três hipóteses minhas descartadas
(parâmetro `scope` ausente, separador `+` em vez de `%20`, grant antigo
reaproveitado):

- A URL de autorização envia `scope=offline_access%20read%20write`, codificada.
- O `/oauth/token` responde com `access_token`, `token_type`, `expires_in`
  (21600), `scope` e `user_id` — **sem `refresh_token` e sem erro
  `invalid_scope`**. O escopo é descartado em silêncio.
- O `scope` concedido traz `read`, `write` e cinco escopos `urn:...`, nunca
  `offline_access`.
- `GET /applications/7837357995078436/grants` confirma o mesmo, e o grant mais
  antigo, de 25/05/2026, **já nascia sem `offline_access`** — nunca funcionou.
- Revogar o grant (`DELETE /users/{id}/applications/{app}`, HTTP 200
  "Autorización eliminada") e autorizar do zero não muda nada.

Achado de produto no caminho: o "Desconectar" era só local e nunca revogava a
autorização no ML. Corrigido — agora revoga de verdade, e isso invalidava
qualquer teste de escopo feito antes.

- [x] Conectar pelo OAuth
- [x] Publicar anúncio de um veículo
- [x] Encerrar todos os anúncios (botão "remover todos")
- [x] Webhook chegando no tenant certo, agora respondendo antes de processar
      (antes o ML cortava a conexão e as notificações se perdiam)
- [x] Aviso de expiração por e-mail (disparou sozinho em 2026-09-14 05:44)
- [x] **Republicação automática ao reconectar** — validado em 2026-09-14: cinco
      veículos publicados sozinhos após autorizar, sem clicar em "sincronizar"
      (todos em AGUARDANDO_PAGAMENTO, que é o ML cobrando a taxa da categoria)
- [x] Anúncio criado na publicação automática (MLB5234328545)
- [!] Atualizar preço / encerrar anúncio no ML — **impossível enquanto o anúncio
      estiver em **. O ML recusa com "price is not modifiable",
      "status is not modifiable". Só volta a ser possível pagando a taxa.
      Verificado em 2026-09-14 nas duas operações
- [x] Aviso de expiração com o visual novo — recebido em 2026-09-15
- [x] **Renovação automática do token** — validada em 23/09/2026 pelo log de
      produção: de 18/09 a 23/09 o token foi renovado sozinho a cada 6h
      (`Token do Mercado Livre renovado com sucesso (expira em 21600s)`), sem
      nenhuma reconexão manual
- [x] **E-mail de expiração falso a cada 6h** — achado em 23/09 no mesmo log.
      O refresh só acontecia 60s antes de vencer, mas o aviso sai 60 min antes:
      a loja recebia "Conexão com o Mercado Livre expira em breve" 4 vezes por
      dia, e uma hora depois o token se renovava sozinho. Corrigido: o refresh
      passou a acontecer 75 min antes (`RefreshSkew`), o worker renova antes de
      decidir, e só avisa se a renovação falhar. Antes o worker também calava
      justamente nesse caso — checava `IsConnectedAsync` primeiro, e ela devolve
      false quando o refresh falha. **Validado em 23/09 após o deploy:** o token
      foi renovado às 12:38 (Brasília), antes da janela de aviso, e nenhum e-mail
      saiu

## 2. WhatsApp Business

Implementado por inteiro e **nunca configurado**: configuração, verificação do
webhook, recebimento criando Lead com anti-duplicata, envio de mensagem, envio
de template e lembrete de test drive.

Pré-requisitos que não são de código: conta **WhatsApp Business API** (não é o
aplicativo comum), número dedicado a ela, e templates aprovados pela Meta.

- [x] **Configurar Access Token, Phone ID e Verify Token** — 2026-09-16, com o
      número de teste gratuito da Meta (app `1801407787871634`, Phone ID
      `1306797175857602`)
- [x] **Webhook de verificação** — a Meta chamou `GET .../whatsapp/webhook`
      com `hub.challenge` e recebeu 200
- [x] **Mensagem recebida vira Lead** — `Lead WhatsApp criado #1 de
      +16315551181`, com nome, telefone, origem WHATSAPP e o texto na observação
- [x] **Anti-duplicata** — segundo envio do mesmo payload registrou
      `Lead WhatsApp ja existe (+16315551181), ignorando duplicata`
- [x] Notificação em tempo real do Lead no painel, sem recarregar — **validado em 17/09/2026**.
      Webhook disparado com o número real (11) 95317-9948 → lead apareceu sozinho no topo
      da lista e os cartões passaram de 3 para 4, com a tela parada. A tela nunca escutava
      o `LEAD_WHATSAPP` que o backend já emitia; corrigido no commit 72a4c0c.
- [x] **Enviar mensagem pelo sistema** — não havia tela: `enviarWhatsApp` existia
      no serviço e nenhuma página o chamava; o botão do lead só abria o `wa.me`.
      Criada a resposta pelo lead e o envio de teste no card de Integrações
- [x] **Token permanente do WhatsApp** — configurado em 23/09/2026. Usuário do
      sistema `connectveiculos-api` no portfólio "ConnectVeiculos Teste",
      `whatsapp_business_management` + `whatsapp_business_messaging`. O
      depurador de token da Meta confirma: tipo System User, expira "Nunca",
      escopos valendo para todas as contas do WhatsApp do portfólio
- [x] **Templates `testdrive_confirmado` / `_lembrete` / `_cancelado`** —
      enviados para análise em 23/09/2026 (Utilidade, pt_BR, variáveis por
      número). A Meta recusa variável no fim do corpo — o `_{{6}}_` do
      `WHATSAPP_TEMPLATES.md` não passa; a última linha virou
      `Te esperamos na {{6}}. Até breve!`. **Falta aprovação**
- [x] ~~O token do WhatsApp expira em 24h~~ — descoberto em 18/09/2026, resolvido
      pelo token permanente acima.
      Consultando o número na Graph API, a resposta foi:

      ```
      Error validating access token: Session has expired on
      Thursday, 17-Sep-26 12:00:00 PDT
      ```

      O token gerado no painel "API Setup" da Meta é **temporário, de 24 horas**.
      Serve para testar e nada mais: depois de um dia o envio para de funcionar
      sozinho, sem ninguém mexer em nada. É uma explicação a se considerar para
      qualquer teste de envio que "parou de funcionar do nada" de um dia para o
      outro.

      Não afeta o recebimento: o webhook continua criando leads normalmente,
      porque quem chama somos nós é que somos chamados.

      A saída definitiva é um **token de Usuário do Sistema** (System User) no
      Business Manager, com as permissões `whatsapp_business_messaging` e
      `whatsapp_business_management`, marcado como permanente. Enquanto o
      Embedded Signup não existir, é esse token que tem de estar na configuração

- [!] **Causa do "accepted" que nunca chega — encontrada em 23/09/2026.** Com
      o token permanente e o número já na lista de destinatários, o
      `hello_world` saiu com `accepted` e não chegou. O evento de status, visto
      em "Verifique webhooks de teste" no painel do app, trouxe o motivo:

      ```
      "status": "failed", "code": 130497,
      "title": "Business account is restricted from messaging users in this country."
      ```

      O **número de teste da Meta (+1 555 149-2530) não pode enviar para o
      Brasil**. Não é token, template, lista de destinatários nem código. O
      caminho é registrar um **número brasileiro real** na conta
      `2600526637086768` (Etapa 2 · Configuração da produção), com forma de
      pagamento. Receber mensagens (lead) continua funcionando com o número de
      teste
- [!] **Enviar template aprovado — bloqueado fora do nosso código.** O envio sai
      correto: a Meta responde 200 com `wa_id` válido e `message_status:
      accepted`. A mensagem não é entregue, e o teste que isola isso é
      definitivo — **o envio disparado pelo painel da própria Meta, sem passar
      pelo sistema, também não chega**. Falta verificar o número destinatário
      na lista de teste
- [~] Lembrete de test drive (job do Hangfire) — **o job está validado; o
      que falta é o template na Meta.** Log de produção em 21/09, 09:00:

      ```
      Lembrete TestDrive tenant empresa-teste: 1 agendados pra amanha —
      0 enviados, 1 falharam, 0 sem WA configurado
      WhatsApp falhou (NotFound): (#132001) template name (testdrive_lembrete)
      does not exist in pt_BR
      ```

      O job achou o test drive #1 (remarcado para 22/09), montou o envio e a
      Meta recusou por template inexistente — não por token. Depois de criar e
      aprovar os templates, basta agendar um test drive para o dia seguinte.

      Histórico dos bloqueios:
      1. *(resolvido em 18/09)* O horário é opcional no cadastro e ia vazio para
         a variável `{{3}}` do template. A Meta recusa o template inteiro quando
         uma variável vem em branco, e a recusa não diz qual foi — o erro chega
         parecendo template inexistente. O test drive cadastrado para o teste
         estava salvo justamente sem horário. Corrigido com 4 testes de regressão
      2. Os templates `testdrive_confirmado` e `testdrive_lembrete` ainda não
         existem na WABA — precisam ser criados e aprovados (6 parâmetros: nome,
         data, horário, veículo, endereço, nome da loja)

      O job em si já degrada direito: conta enviados/falhas/sem-config, não
      estoura exceção, e o log já sugere conferir se o template está aprovado.
      Nada a mudar ali.

      **Preparado em 18/09:** test drive #2 agendado para 19/09 as 14:30,
      status Confirmado, telefone (11) 95317-9948. O job das 09:00 de 19/09 vai
      encontra-lo. O #1 (18/09, sem horario) ficou como esta — serve de registro
      do bug que motivou a correcao.

      A confirmacao disparou a notificacao e o log mostrou o estado real:

      ```
      [13:05:06 ERR] WhatsApp falhou (Unauthorized): "Authentication Error", code 190
      [13:05:06 WRN] TestDrive notificacao falhou: template=testdrive_confirmado
      ```

      13:05 UTC = 10:05 em Brasilia, e o token expirava as 10:00 — a prova em
      producao de que o token de 24h nao serve nem para um dia de trabalho

**Achados de código nesta rodada**, todos corrigidos:

- Template sem parâmetro não pode levar `components`. Mandávamos sempre um
  `body` com lista vazia; a Meta aceita e não entrega. O `hello_world` cai
  exatamente nesse caso
- O envio logava só "enviado com sucesso" e descartava o corpo da resposta —
  que traz o id e o `message_status`. Dois testes se perderam por isso
- `EnviarTemplateAsync` não tinha endpoint nem tela, mesmo padrão do
  `enviarWhatsApp` e do e-mail de novo usuário
- O telefone `+1 631 555-1181` aparecia como `(16) 31555-1181`: o pipe aplicava
  máscara brasileira em qualquer número de 11 dígitos

**Roteamento por número — feito e validado em 2026-09-17.** O webhook
identifica a loja pelo `phone_number_id` do payload, não mais pela URL. Isso
permite um app único da Meta atender todas as lojas com o mesmo endereço.

Validado chamando o webhook **sem** `?tenant=`: o lead caiu em `empresa-teste`
(`Lead WhatsApp criado #2 ... no tenant empresa-teste`) e o `default` continuou
com zero leads — ou seja, não houve queda no tenant errado. O mapa
número → loja vive no master, no padrão do `UserEmailMap`. O `?tenant=`
continua aceito como alternativa.

**Falta a segunda metade: o Embedded Signup**, que troca os três campos de
configuração por um botão. Depende de a Meta aprovar a conta como Provedor de
Tecnologia, o que exige verificação da empresa. Não foi escrito de propósito —
seria código indo para produção sem nunca ter rodado.

**Pendências para ter clientes reais** (nenhuma é de código):

1. **Verificação da empresa** na Meta — é o primeiro dominó: aparece como
   pré-requisito do WhatsApp em produção e de Provedor de Tecnologia
2. **App Review do Facebook/Instagram** — enquanto o app estiver em modo
   desenvolvimento, só admin, desenvolvedor ou testador do app consegue
   autorizar. Uma revenda de fora **não conectaria hoje**, e isso nunca foi
   testado porque todos os testes foram com a conta do próprio desenvolvedor
3. **Provedor de Tecnologia + Embedded Signup** para o WhatsApp

Atenção ao banner "Modo Teste" da tela de Integrações: ele é calculado como
`AppId preenchido && AppSecret vazio`, o que mede se falta configurar o secret,
**não** o modo do app na Meta. Pode estar informando algo que não corresponde

**Nota sobre webhooks:** app não publicado **só recebe webhook de teste
disparado do painel**. Os status reais de entrega aparecem no painel da Meta e
nunca chegam ao servidor — confirmado em 2026-09-17, três eventos `messages`
listados lá e zero POSTs no nginx

## 3. Meta — Facebook e Instagram

- [x] Publicar no Facebook Page com foto
- [x] Publicar no Instagram (carrossel de até 10 fotos)
- [x] Carimbo `✅ VENDIDO` na legenda do post do Facebook
- [x] Publicação manual pelo ícone da rede na lista de Veículos
- [x] Ícone colorido só quando já publicado
- [x] **Estorno de venda tirando o carimbo** — validado em 2026-09-14 (veículo 8)
- [x] **Mudança de preço reescrevendo a legenda** — validado em 2026-09-14
- [x] **Excluir veículo carimbando `⛔ INDISPONÍVEL`** — validado em 2026-09-15
      (veículo 8; log registrou "atualizada (status I)")
- [x] **Conexão automática da Page** — validado em 2026-09-15. Desconectar e
      reconectar deixou a Page ativa sem nenhum passo manual; o log registrou
      "Page ConnectVeiculos selecionada automaticamente (única da conta)" e os
      dois "Testar conexão" responderam OK com o Page Token novo
- [x] **Causa do "Nenhuma Page encontrada"** — era a falta de
      `business_management`. Sem esse escopo o `/me/accounts` devolve 200 com
      `data` vazio quando a Page pertence a um Business, mesmo com
      `pages_show_list` concedido
- [ ] Comportamento quando o token da Page falhar (não expira, mas nunca vimos
      falhar de verdade)
- [!] Marcar vendido no Instagram — a Graph API não permite editar legenda de
      mídia publicada
- [x] **Carimbo `🔒 RESERVADO`** — validado em 2026-09-15 (veículo 1)
- [x] **Tirar o carimbo ao voltar para Disponível** — validado em 2026-09-15,
      *depois de corrigir*. O ramo "voltou a ficar disponível" publicava no
      catálogo do Facebook e no Google Merchant, mas não reescrevia a legenda do
      post orgânico: o post ficava dizendo RESERVADO com o carro de volta à
      venda. Só o estorno de venda tratava disso. Corrigido em
      `AtualizarVeiculoUseCase`, com teste de regressão
- [!] **Apagar o post do Instagram — impossível, e o interruptor foi retirado**.
      Exige `instagram_manage_contents`, que a Meta não concede a este tipo de
      aplicativo: pedi-lo no OAuth derruba a autorização inteira com "Invalid
      Scopes". Confirmado em 2026-09-15 na tela de Integrações comerciais do
      Facebook, que lista as seis permissões concedidas (`business_management`,
      `pages_show_list`, `pages_read_engagement`, `pages_manage_posts`,
      `instagram_basic`, `instagram_content_publish`) — nenhuma de gerenciar
      conteúdo do Instagram. O backend continua pronto caso o App Review libere.
      **Confirmado também pela API** em 2026-09-15, marcando o veículo 1 como
      reservado com a opção ligada:
      `DELETE /v18.0/18162197113470689 -> 400` com
      `(#10) Insufficient permissions to access this data`. O registro da
      publicação continuou ATIVO, como deve: o ícone na lista não pode dizer que
      o post sumiu enquanto ele está no ar

## 4. E-mail (SMTP)

> Achado durante a validação: a API devolvia o token de redefinição no corpo da
> resposta e a tela o exibia. Qualquer pessoa podia pedir recuperação com o
> e-mail de outra e trocar a senha dela sem acesso à caixa de entrada. Corrigido.

Remetente atual: `mecanto.app@gmail.com` via `smtp.gmail.com:587`. É provisório —
um cliente da Diamante recebendo e-mail desse endereço estranha.

> Achado em 2026-09-15: todo valor em dinheiro saía como `¤119,000.00`. A
> aplicação nunca define cultura e o container não tem `LANG`, então `:C`
> formatava com a cultura invariante. Corrigido com pt-BR explícito. A mesma
> causa deixava a porcentagem de queda de preço como `5.9%`; corrigida depois,
> em 2026-09-15.

- [x] Teste de configuração da tela de Integrações
- [x] Aviso de expiração do Mercado Livre
- [x] **Recuperação de senha** — validado em 2026-09-14, ponta a ponta (pedido,
      e-mail com link, tela de nova senha, login com a senha nova)
- [x] Venda confirmada — 2026-09-15
- [x] Venda estornada — 2026-09-15
- [x] Queda de preço para quem favoritou um veículo — 2026-09-15
- [x] Novo veículo similar para quem favoritou — validado em 2026-09-15 (Jeep
      Renegade a R$ 120.000 disparou para quem favoritou o Compass a R$ 112.000;
      regra é mesma marca, mesma categoria e preço dentro de ±20%)
- [x] **Novo usuário (senha temporária)** — validado ponta a ponta em
      2026-09-16: cadastro sem campo de senha, e-mail recebido com a temporária
      `PVjGJPXHdq7T` (12 caracteres, sem I/l/1/O/0), modal obrigatório abrindo
      sozinho no primeiro login, troca concluída e `UsuTrocarSenha` de volta a 0.
      Dois bugs meus apareceram só neste teste: eu havia preenchido o campo no
      `LoginUseCase`, que a API não usa (quem autentica é o `AuthController`), e
      abria o modal de dentro de um `effect()`, o que o Angular proíbe (NG0600)
      — a exceção era engolida e a tela nunca aparecia. Os dois desenhos que se contradiziam foram
      resolvidos a favor da senha gerada pelo sistema: o formulário não pede
      mais senha, `CadastrarUsuarioUseCase` gera uma temporária de 12
      caracteres, grava `UsuTrocarSenha` e enfileira o e-mail depois do commit.
      No primeiro login o modal de troca abre sozinho e não fecha até a pessoa
      escolher a própria senha. Qualquer troca de senha (tela ou recuperação)
      zera a exigência no mesmo UPDATE
- [x] Acentuação e visual padronizados em todos os templates

## 4.1 Mercado Livre — offline_access resolvido em 18/09/2026

- [x] **A conexao para de cair a cada 6 horas.** O suporte respondeu ao chamado
      WCS-50843 confirmando o diagnostico: a aplicacao estava com o fluxo e o
      redirect URI corretos, **faltava o offline_access habilitado na propria
      aplicacao**.

      No DevCenter isso nao aparece com esse nome: e a caixa **"Refresh Token"**
      em *Fluxos OAuth*, que estava desmarcada ao lado de Authorization Code e
      Client Credentials. Os seletores de *Permissoes* nao tem opcao "off-line"
      nenhuma — procurar por ali era beco sem saida.

      Ordem que funcionou: marcar Refresh Token e salvar → Desconectar (revoga o
      grant antigo, que o suporte exigia) → Conectar. Resultado no log:

      ```
      ML /oauth/token respondeu com os campos
      [access_token, token_type, expires_in, scope, user_id, refresh_token]
      e scope [offline_access read ... write]
      Mercado Livre conectado. UserId: 188075347
      ```

      No banco: IntRefreshTokenCifrado PRESENTE, IntFalhasConsecutivasSync 0.

      Tres hipoteses foram descartadas antes desta (ausencia do parametro scope,
      separador + vs %20, grant velho). A quarta tentativa achou um bug real de
      caminho — o DesconectarAsync nunca revogava o grant — que virou justamente
      o passo que o suporte pediu.

## 5. Google

- [x] Domínio verificado no Search Console
- [x] `sitemap.xml` aceito — 17 URLs descobertas
- [x] Título e JSON-LD próprios na página de veículo disponível
- [x] Veículo vendido com `noindex` e aviso na tela
- [x] **Páginas realmente indexadas** — confirmado em 18/09/2026 por
      `site:connectveiculos.dev.br` no Google. Aparecem a landing, o catálogo da
      loja (`/catalogo/empresa-teste`), a página de exclusão de dados e **uma
      página de veículo** (`/empresa-teste/veiculo/...`). A página do veículo sai
      com preço no resultado — "R$ 145.000,00" — ou seja o JSON-LD está sendo
      lido, não só o texto. Nenhum veículo vendido apareceu, o que bate com o
      `noindex`
- [x] **Filtrar o sitemap para lojas públicas** — feito e validado em
      2026-09-15. Expunha quatro tenants: `default` (6 URLs), `empresa-teste`
      (5), `teste` (1) e `viorica7078` (1) — este último um autocadastro com
      e-mail temporário, ou seja qualquer visitante entrava no sitemap sozinho e
      era oferecido ao Google como loja real. Agora entram só lojas ativas, fora
      do plano gratuito, diferentes de `default` (loja-modelo) e com ao menos um
      veículo no catálogo. O plano gratuito é reconhecido pelo nome, não pelo
      preço: o Enterprise custa zero por ser valor sob consulta, e filtrar por
      preço tirava uma loja real do ar. Resultado: 14 URLs → 7, só
      `empresa-teste`
- [!] Google Merchant — refresh token revogado (`invalid_grant`)
- [!] Google Vehicle Ads — não existe no Brasil
- [ ] Google Ads — sem integração no sistema; é link externo, nada a testar

## 6. Fluxos que cruzam integrações

- [x] **Cadastrar veículo com fotos e ver publicar sozinho** — validado em
      2026-09-14 com o Jeep Compass (veículo 8, 3 fotos). O log registrou
      "Publicando veiculo 8 nas plataformas externas (3 foto(s))", ou seja o
      serviço esperou o upload terminar antes de disparar. Resultado: anúncio no
      Mercado Livre, post no Facebook com foto e carrossel no Instagram
- [x] Trava "Salvando…" — vista funcionando no cadastro de veículo (formulário
      acinzentado e botão com spinner durante o upload das fotos)
- [x] **Importar planilha e publicar** — resolvido em 2026-09-16 descobrindo
      que a premissa estava errada. O checklist dizia "hoje não publica em
      lugar nenhum"; na verdade a importação cria os veículos pelo mesmo
      endpoint do cadastro manual, que **já enfileira a publicação
      automática** — a mesma rotina que espera as fotos chegarem.

      Chegou a ser implementada uma pergunta no fim da importação ("publicar os
      importados?") com rotina própria de lote. Revertida: era redundante, e um
      veículo recém-importado nunca tem foto no momento da pergunta, porque
      planilha não carrega imagem — a resposta seria sempre "0 publicados".
      O log de produção mostrou as duas rotinas rodando em sequência para os
      mesmos veículos, cada uma registrando a mesma ausência de foto.

      Como funciona hoje: o veículo importado entra na publicação automática
      como qualquer outro. Sem foto na janela de espera, não publica e registra
      o motivo. Quem subir as fotos depois publica pelos ícones de Instagram e
      Facebook na lista de Veículos.

## 6.1 Lacunas de produto encontradas

- [x] **Remarcar test drive** — implementado e validado em producao em
      18/09/2026. O registro #1, que estava em 18/09 sem horario (o mesmo que
      revelou o bug do parametro vazio), foi remarcado pela tela para 22/09 as
      10:30, mantendo o status Confirmado. O #2, preparado para o job, ficou
      intacto. O aviso ao cliente foi tentado e caiu com code 190, como
      esperado enquanto o token do WhatsApp nao for permanente.

      **Descoberta de bastidor:** as telas que "nao carregavam" durante os
      testes (Veiculos, Test Drives, Leads) nao tinham problema nenhum. O
      console entregou a explicacao: `The page is being frozen` — o Chrome
      suspende abas em segundo plano. Com a aba em foco, tudo responde na hora.
      O servidor sempre entregou em ~0,12s, o que ja era pista disso.

      Era assim antes:

- [~] **Nao existia remarcar test drive.** A API tem `POST` (agendar, publico),
      `GET` (listar) e `PUT /{id}/status` — e so. Se o cliente ligar pedindo
      para mudar a data ou o horario, a loja precisa cancelar e criar outro,
      perdendo o registro do agendamento original. O horario tambem nao pode ser
      preenchido depois: o test drive #1 esta sem horario e nao ha como corrigir
      pela tela. Encontrado em 18/09/2026 ao procurar o botao de editar

## 6.2 Feed do catálogo (Facebook / Google)

- [x] `/api/feed/facebook` (TSV) e `/api/feed/google` (XML) respondendo 200 —
      verificado em 23/09/2026
- [x] Links do feed abrindo a página certa do veículo (`/catalogo/{slug}/veiculo/{id}`)
- [x] **Feed servindo a loja errada — corrigido e validado em 23/09 (b806aac):** `empresa-teste` agora traz os carros da Diamante e `default` os da loja-modelo. Achado em
      23/09: `?tenant=empresa-teste` devolvia os carros da loja-modelo
      `default`. O `[ResponseCache(Duration = 60)]` sem `VaryByQueryKeys` faz o
      `UseResponseCaching` usar só o path como chave — o primeiro feed pedido
      era servido por 60s para todas as lojas (a prova: `Age: 46` numa URL com
      parâmetro aleatório). Mesmo bug que já tinha acontecido em
      `/api/imagens/file`. Provavelmente é o "não encontra o carro do
      catálogo": um catálogo da Meta apontado para o feed da loja recebia os
      carros de outra. **Depois do deploy:** comparar
      `/api/feed/google?tenant=empresa-teste` com `?tenant=default` — os títulos
      têm de ser diferentes

## 7. Infraestrutura

- [x] 504 durante deploy — resolvido com teto de memória no build
- [ ] Site continua lento durante deploy (1 a 6s por ~13 min). A saída seria
      construir a imagem fora da VM (GitHub Actions); migrar para máquina maior
      não é possível na conta atual

---

## Ordem sugerida

1. **Recuperação de senha** — se falhar, ninguém recupera acesso ao sistema
2. **Reconectar o Mercado Livre** — resolve a conexão caída e valida a
   republicação automática de uma vez
3. **Cadastrar um veículo com fotos** — valida o fluxo automático inteiro
4. **WhatsApp** — o maior bloco intocado, mas depende de conta e número

## Pendências que não são de código

- ~~Chamado no suporte do Mercado Livre pedindo `offline_access`~~ — resolvido em 18/09
- Criar e aprovar na Meta os templates `testdrive_confirmado` e `testdrive_lembrete`
- ~~Token permanente do WhatsApp (System User)~~ — feito em 23/09
- **Número brasileiro para o WhatsApp — PENDENTE, decisão do cliente.** O número
  de teste da Meta não envia para o Brasil (erro 130497). Opções: chip novo só
  para o sistema, número atual da loja (deixa de usar o app nele) ou apresentar
  sem envio automático. Precisa de nome de exibição aprovado e cartão na conta
  `2600526637086768`. Até lá, confirmação/lembrete/cancelamento de test drive
  ficam sem validar
- Rotacionar o app secret da Meta
- Estoque real da Diamante no lugar dos carros de teste (placas `TST...`)
- Logotipo real da loja
- Remetente de e-mail no domínio da loja
