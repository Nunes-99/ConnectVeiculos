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

A conta cai a cada 6 horas: o aplicativo `7837357995078436` não recebe
`refresh_token` e o DevCenter não expõe `offline_access`. Testado com e sem
`scope` na URL de autorização e com corpo em JSON e form-urlencoded — o
comportamento não muda. **A correção depende de chamado no suporte deles**; o
resto abaixo é conviver com o problema.

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
- [ ] Aviso de expiração com o visual novo (o que chegou usava o texto antigo)
- [!] Renovação automática do token — impossível sem `offline_access`

## 2. WhatsApp Business

Implementado por inteiro e **nunca configurado**: configuração, verificação do
webhook, recebimento criando Lead com anti-duplicata, envio de mensagem, envio
de template e lembrete de test drive.

Pré-requisitos que não são de código: conta **WhatsApp Business API** (não é o
aplicativo comum), número dedicado a ela, e templates aprovados pela Meta.

- [ ] Configurar Access Token, Phone ID e Verify Token
- [ ] Webhook de verificação — cadastrar a URL no painel da Meta e ver o
      `hub.challenge` ser aceito
- [ ] **Mensagem recebida vira Lead** no admin
- [ ] Anti-duplicata — duas mensagens seguidas do mesmo número geram um Lead só
- [ ] Notificação em tempo real do Lead no painel, sem recarregar
- [ ] Enviar mensagem pelo sistema
- [ ] Enviar template aprovado
- [ ] Lembrete de test drive (job do Hangfire)

## 3. Meta — Facebook e Instagram

- [x] Publicar no Facebook Page com foto
- [x] Publicar no Instagram (carrossel de até 10 fotos)
- [x] Carimbo `✅ VENDIDO` na legenda do post do Facebook
- [x] Publicação manual pelo ícone da rede na lista de Veículos
- [x] Ícone colorido só quando já publicado
- [x] **Estorno de venda tirando o carimbo** — validado em 2026-09-14 (veículo 8)
- [x] **Mudança de preço reescrevendo a legenda** — validado em 2026-09-14
- [ ] **Excluir veículo carimbando `⛔ INDISPONÍVEL`**
- [ ] Comportamento quando o token da Page falhar (não expira, mas nunca vimos
      falhar de verdade)
- [!] Marcar vendido no Instagram — a Graph API não permite editar legenda de
      mídia publicada; só permite excluir

## 4. E-mail (SMTP)

> Achado durante a validação: a API devolvia o token de redefinição no corpo da
> resposta e a tela o exibia. Qualquer pessoa podia pedir recuperação com o
> e-mail de outra e trocar a senha dela sem acesso à caixa de entrada. Corrigido.

Remetente atual: `mecanto.app@gmail.com` via `smtp.gmail.com:587`. É provisório —
um cliente da Diamante recebendo e-mail desse endereço estranha.

- [x] Teste de configuração da tela de Integrações
- [x] Aviso de expiração do Mercado Livre
- [x] **Recuperação de senha** — validado em 2026-09-14, ponta a ponta (pedido,
      e-mail com link, tela de nova senha, login com a senha nova)
- [ ] Venda confirmada
- [ ] Venda estornada
- [ ] Queda de preço para quem favoritou um veículo
- [ ] Novo veículo similar para quem favoritou
- [ ] Novo usuário (senha temporária)
- [x] Acentuação e visual padronizados em todos os templates

## 5. Google

- [x] Domínio verificado no Search Console
- [x] `sitemap.xml` aceito — 17 URLs descobertas
- [x] Título e JSON-LD próprios na página de veículo disponível
- [x] Veículo vendido com `noindex` e aviso na tela
- [ ] **Alguma página realmente indexada** — "descoberta" não é "indexada";
      conferir em Inspeção de URL e solicitar indexação
- [ ] Filtrar o sitemap para lojas públicas (hoje expõe `default`,
      `empresa-teste` e `teste`)
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
- [ ] Importar planilha — hoje não publica em lugar nenhum; decisão pendente se
      deve publicar em ML e catálogos (Instagram não, pelo limite de 25
      posts/24h)

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

- Chamado no suporte do Mercado Livre pedindo `offline_access` na aplicação
  `7837357995078436`
- Rotacionar o app secret da Meta
- Estoque real da Diamante no lugar dos carros de teste (placas `TST...`)
- Logotipo real da loja
- Remetente de e-mail no domínio da loja
