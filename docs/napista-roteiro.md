# NaPista: do client_id à produção

Integração de estoque e leads com o NaPista (portal do banco BV). Sem custo
para o integrador (confirmado pelo NaPista em 25/11/2026). A loja conecta a **conta dela no NaPista**
(mesmo usuário e senha de lá) por OAuth; o ConnectVeiculos é o integrador, com
`client_id` próprio e **sem segredo** (cliente público).

Documentação (com senha, no e-mail do Renan Queiroz, 04/11/2026):
https://developers.napista.com.br/reference/boas-vindas — contato:
renan.freitas@napista.com.br / integrador@napista.com.br.

## Situação (26/11/2026)

- Desenvolvimento do NaPista: client_id **`connect-veiculos`** liberado em
  24/11/2026, redirect_uri só `https://connectveiculos.dev.br/integracoes/napista`
  (localhost **não** é aceito). Testes concluídos em 25/11/2026 (abaixo).
- **Produção liberada em 25/11/2026** (mesmo client_id `connect-veiculos`):
  auth `https://auth.napista.com.br/realms/marketplace-dealer/protocol/openid-connect`,
  API `https://api.napista.com.br/seller-inventory-api`. O refresh token **não
  expira** (a loja conecta uma vez só), até **30 fotos** por anúncio, rate limit
  recomendado de **100 requisições/min**, **sem webhook** (leads só pela
  consulta) e **sem custo** para o integrador. Leads indisponíveis no
  desenvolvimento por enquanto; as fotos (PATCH apagando, 403) o Renan ainda
  vai verificar.

## Como funciona

- **Integrações → NaPista**: *Conectar conta do NaPista* → login no NaPista →
  volta para `/integracoes/napista` → escolhe a loja (se o usuário tiver uma
  só, já vem escolhida). *Buscar leads do NaPista* traz os últimos 30 dias para
  **Leads** (origem "NaPista", veículo ligado pela placa; rodar de novo não
  duplica).
- **Veículos → menu ⋮ → Anunciar no NaPista**: o sistema sugere marca, modelo,
  versão, câmbio, combustível, cor e portas a partir do cadastro (FIPE); a
  pessoa confere e anuncia. Preço, km, placa, observação e até 30 fotos vêm do
  cadastro. Carro novo **não** vai sozinho (a versão do catálogo tem de ser
  confirmada uma vez); depois disso tudo é automático.
- Na lista de **Veículos**, o selo azul **NP** marca os carros no ar no NaPista
  (abre a janela do anúncio; o NaPista não devolve o link público).
- Depois é automático: editar atualiza o anúncio; vender marca **vendido**
  (SOLD); inativar/reservar tira do ar (UNPUBLISHED); estornar/voltar a
  disponível republica. *Tirar do ar* pela janela pausa: só volta clicando em
  Anunciar. Subir, excluir ou trocar a foto principal manda as fotos ~15 s
  depois (junta várias fotos num envio só).
- Trocar a versão ou os anos de um anúncio cria outro (a API não deixa mudar
  esses campos) e o antigo é removido.
- **Loja que já anunciava no NaPista**: Integrações → NaPista → *Ver anúncios
  do NaPista* lista os publicados (`GET /seller/{id}/offers?status=PUBLISHED`).
  Importar cria o veículo (marca e modelo pelo catálogo, a partir do título;
  cor, opcionais, km, preço, descrição e fotos do anúncio) já ligado ao
  anúncio, sem criar outro lá e sem postar nas redes. Placa já cadastrada só
  liga. O NaPista pode esconder a placa (`******4`): a pessoa digita e o final
  tem de bater. Foto que não baixa (endereço relativo ou 403) fica de fora,
  com aviso.

## Comportamentos do NaPista que a documentação não conta

Descobertos no desenvolvimento deles em 25/11/2026; o código já trata:

- **O PATCH do anúncio apaga as fotos** (a doc diz "Photos are ignored"). O
  sistema reenvia as fotos por `/photos/url` depois de toda alteração (#31).
- **`/photos/url` substitui as anteriores, mas dois envios simultâneos somam**
  (ficou com 8 fotos em vez de 4). Um envio por veículo de cada vez (#32).
- **DELETE é exclusão lógica**: um PATCH com `status: PUBLISHED` num anúncio
  apagado o publica de novo (não volta 404).
- **PATCH com versão diferente da do anúncio** volta 400 "Request has field
  cannot be changed!" — por isso a troca de versão apaga e cria outro.
- **Valor fora do enum** (cor, combustível...) volta 400 "Failed to read
  request" sem dizer o campo. O sistema traduz e mantém os dados já aceitos (#29).
- **Fotos devolvidas pela API** (`marketplace.development.napista.com.br/static/photos/...`)
  respondem **403** fora do NaPista no desenvolvimento: a miniatura não abre e
  a importação não baixa as fotos. Perguntado se em produção são públicas.
- O login (Keycloak) de desenvolvimento cai às vezes (503): não desconecta mais
  a loja (#28). A tela de login deles expira se ficar aberta muito tempo
  ("Ops, ocorreu um problema no carregamento"): é só clicar em Conectar de novo.
- Com a conexão desfeita, edições do carro não dão erro nem vão ao NaPista; ao
  reconectar, o anúncio continua ligado e só a próxima edição o atualiza.

## Testado

- `NapistaServiceTests` (NaPista simulado no formato da documentação): conexão,
  renovação e queda do login, anúncio, fotos (reenvio após PATCH, um envio por
  veículo, fotos mudadas pela tela), vendido/estorno/pausa, troca de versão,
  recusa, sugestão, leads, importação (lista, criar, ligar pela placa, placa
  escondida, importação dupla).
- Contra o NaPista de desenvolvimento (25/11/2026), na loja `empresa-teste`, com
  o Ford Ka TST0E05: conectar/reconectar, loja, leads, catálogo (carro, moto,
  caminhão), anunciar com fotos, editar preço/km, vendido por edição e por
  **venda + estorno**, reservado, **inativar e reativar**, tirar do ar e
  anunciar de novo, troca de versão, recusas, edições simultâneas, subir/excluir
  foto e trocar a principal, anúncio apagado no NaPista, importar (ligar pela
  placa e criar do zero) e desconectado (tudo pede "Conecte", editar não dá
  erro).
- **Não deu para testar no desenvolvimento**: leads com dados (a conta de teste
  não tem) e fotos baixadas na importação (403). Ficam para o primeiro uso em
  produção.
- Atenção ao testar status na loja de teste: com o Mercado Livre conectado, cada
  volta a "disponível" cria um anúncio novo no ML ("aguardando pagamento").
  Desconecte o ML antes (só a `empresa-teste` usa).

## Para produção

1. O NaPista libera o client em produção e informa os endereços.
2. No `.env` do servidor (pelo `bash scripts/trocar-chaves.sh`, ver
   `docs/trocar-chaves.md`); o client_id continua `connect-veiculos`:
   - `NAPISTA_AUTH_URL=https://auth.napista.com.br/realms/marketplace-dealer/protocol/openid-connect`
   - `NAPISTA_API_URL=https://api.napista.com.br/seller-inventory-api`
3. **Antes de virar**: na `empresa-teste`, desconectar o NaPista e apagar as
   linhas de `VeiculoNapista` (apontam para anúncios do desenvolvimento, que não
   existem em produção).
4. Com uma loja de verdade: conectar com a conta dela, importar os anúncios que
   ela já tem (conferir que as fotos vêm), anunciar um carro e conferir no
   **napista.com.br**, editar, vender/estornar, buscar leads.

## Perguntas ao NaPista (e-mail de 26/11/2026)

Respondidas em 25/11/2026: endereços de produção, client_id (o mesmo), refresh
token (não expira), limites (30 fotos, 100 req/min), webhook (não tem), custo
(nenhum). **Em aberto**: o PATCH apagar as fotos, se as URLs das fotos são
públicas em produção, e leads no desenvolvimento (indisponível por enquanto).

## Onde está o código

- Backend: `Infrastructure/Services/Napista/` (`NapistaService`,
  `NapistaService.Importacao`, `NapistaSugestao`, `NapistaSettings`),
  `API/Controllers/NapistaController.cs` (`/api/integracoes/napista`), tabela
  `VeiculoNapista`, `Lead.LeaExternoId`.
- Gatilhos: `PublicacaoAutomaticaService` (venda, inativação, estorno),
  `AtualizarVeiculoUseCase` (edição), `ImagensController` (fotos), junto com a
  Webmotors. Selo da lista: `ConsultarVeiculosUseCase.NapistaNoAr`.
- Telas: `shared/components/napista/` (cartão, janela do veículo e
  importação), `pages/integracoes/napista-retorno.component.ts` (volta do
  login).
