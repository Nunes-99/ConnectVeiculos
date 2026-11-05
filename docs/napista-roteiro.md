# NaPista: do client_id à produção

Integração de estoque e leads com o NaPista (portal do banco BV). Sem custo
para o integrador. A loja conecta a **conta dela no NaPista** (mesmo usuário e
senha de lá) por OAuth; o ConnectVeiculos é o integrador, com `client_id`
próprio e **sem segredo** (cliente público).

Documentação (com senha, no e-mail do Renan Queiroz, 04/11/2026):
https://developers.napista.com.br/reference/boas-vindas — contato:
integrador@napista.com.br / parceiros@napista.com.br.

## Como funciona

- **Integrações → NaPista**: *Conectar conta do NaPista* → login no NaPista →
  volta para `/integracoes/napista` → escolhe a loja (se o usuário tiver uma
  só, já vem escolhida). *Buscar leads do NaPista* traz os últimos 30 dias para
  **Leads** (origem "NaPista", veículo ligado pela placa; rodar de novo não
  duplica).
- **Veículos → menu ⋮ → Anunciar no NaPista**: o sistema sugere marca, modelo,
  versão, câmbio, combustível, cor e portas a partir do cadastro (FIPE); a
  pessoa confere e anuncia. Preço, km, placa, observação e até 20 fotos vêm do
  cadastro.
- Depois é automático: editar atualiza o anúncio (fotos só quando mudam);
  vender marca **vendido** (SOLD); inativar/reservar tira do ar (UNPUBLISHED);
  estornar/voltar a disponível republica. *Tirar do ar* pela janela pausa: só
  volta clicando em Anunciar.
- Trocar a versão ou os anos de um anúncio cria outro (a API não deixa mudar
  esses campos) e o antigo é removido.

## Testado

- `NapistaServiceTests` (15 testes, NaPista simulado no formato da documentação):
  conexão, renovação do token, anúncio, fotos, vendido/estorno/pausa, troca de
  versão, recusa, sugestão, leads.
- Telas, com um NaPista falso local (04/11/2026): conectar, escolher loja,
  buscar leads (aparece em Leads), sugestão completa no Golf GTI, anunciar
  (corpo, foto por URL) e tirar do ar.
- **Ainda não** contra o NaPista de verdade: falta o `client_id`.

## Para ligar (desenvolvimento do NaPista)

1. O NaPista cadastra o `client_id` com as redirect URIs:
   - `https://connectveiculos.dev.br/integracoes/napista`
   - `http://localhost:4200/integracoes/napista`
2. Gravar no servidor (Git Bash): `bash scripts/trocar-chaves.sh` → cole só
   `NAPISTA_CLIENT_ID` (Enter nas outras). Ver `docs/trocar-chaves.md`.
3. Juntar a branch `feature/napista` no master e publicar.
4. Entrar em https://connectveiculos.dev.br com a loja de teste →
   Integrações → NaPista → Conectar, com o usuário de teste que o NaPista der.
5. Anunciar um carro de teste e buscar leads. Mandar prints/erros ao Claude.

## Para produção

No `.env` do servidor (pelo `trocar-chaves.sh`):

- `NAPISTA_CLIENT_ID` de produção (se for outro);
- `NAPISTA_AUTH_URL=https://auth.napista.com.br/realms/marketplace-dealer/protocol/openid-connect`
- `NAPISTA_API_URL=` o endereço de produção da API (a documentação só mostra o
  de desenvolvimento: `https://api.development.napista.com.br/seller-inventory-api`).

## Perguntas em aberto para o NaPista

1. Endereço de produção da API de estoque (`seller-inventory-api`).
2. Existe processo de homologação antes de liberar produção? Quem aprova?
3. O refresh token vence em quanto tempo? Dá para liberar `offline_access` no
   nosso client? Sem isso a loja precisa conectar de novo quando vence (a tela
   mostra "vale até").
4. Limite de fotos por anúncio e de chamadas por minuto.
5. A resposta de criar anúncio vem com `status`? (Se não vier, o sistema chama o
   endpoint de status para publicar.)
6. Há webhook/aviso de lead novo, ou só a consulta?

## Onde está o código

- Backend: `Infrastructure/Services/Napista/` (`NapistaService`,
  `NapistaSugestao`, `NapistaSettings`), `API/Controllers/NapistaController.cs`
  (`/api/integracoes/napista`), tabela `VeiculoNapista`, `Lead.LeaExternoId`.
- Gatilhos: `PublicacaoAutomaticaService` (venda, inativação, estorno) e
  `AtualizarVeiculoUseCase` (edição), junto com a Webmotors.
- Telas: `shared/components/napista/` (cartão e janela do veículo),
  `pages/integracoes/napista-retorno.component.ts` (volta do login).
