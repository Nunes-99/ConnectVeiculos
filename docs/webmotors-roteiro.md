# Webmotors: da homologação à produção

Integração de estoque com o WebService "Integração Revendedor" da Webmotors
(SOAP). Cada loja usa o **usuário de integração** que a Webmotors fornece
(CNPJ, e-mail e senha) e paga o plano dela na Webmotors; o ConnectVeiculos não
cobra nada a mais. Chamado de liberação: **PTS-1524** (Jira da Webmotors).

Manual: https://integracao.webmotors.com.br/manualintegracao/index.html

## Como funciona

- **Integrações → Webmotors**: a loja informa o usuário de integração e escolhe
  a modalidade (plano) do anúncio.
- **Veículos → ícone de carro**: o sistema sugere marca, modelo, versão, câmbio,
  combustível, cor, portas e opcionais a partir do cadastro (FIPE). A pessoa
  confere e clica em **Anunciar**. Preço, km, placa, observação e até 20 fotos
  vêm do cadastro.
- Depois disso é automático: editar altera o anúncio (e troca as fotos se
  mudaram); vender exclui ("vendi na loja"); inativar/reservar exclui; estornar
  a venda ou voltar a disponível anuncia de novo. **Tirar do ar** pela janela
  pausa: só volta clicando em Anunciar.

## Detalhes que a documentação não diz (vistos na homologação, 03/11/2026)

- Retornos: `500` ok, `400` login recusado, `401` sessão vencida (o sistema
  refaz o login sozinho). Outros códigos vêm como `NN|N`.
- Foto por URL (`IncluirFotoUrl`) é recusada (`21|10`) até para .jpg público:
  o servidor baixa a foto e manda o arquivo (`IncluirFoto`, base64).
- Logo depois de incluir, o anúncio recusa foto por alguns segundos (`42|4`):
  o sistema espera 3 + 5 + 8 s e tenta de novo.
- Só o IP do servidor (136.248.77.154) está liberado; do PC dá 403.

## Testar (homologação)

Usuário de homologação: no e-mail do chamado PTS-1524 (não colar no chat).
No Git Bash, com o túnel pelo servidor:

    ssh -f -N -D 1080 -i ~/.ssh/connectveiculos-oracle.key ubuntu@136.248.77.154
    cd back-end/ConnectVeiculos.Tests
    HTTPS_PROXY=socks5://127.0.0.1:1080 WEBMOTORS_TESTE_REAL=1 WEBMOTORS_TESTE_ANUNCIAR=1 \
      WEBMOTORS_CNPJ=... WEBMOTORS_EMAIL=... WEBMOTORS_SENHA=... \
      dotnet test --filter WebmotorsRealTests --logger "console;verbosity=detailed"

Inclui um anúncio de teste com foto, altera o preço e exclui. Passou em 03/11/2026.

## Ir para produção

1. Responder o chamado PTS-1524 dizendo que os testes terminaram e pedir a
   liberação de produção (o IP é o mesmo) e se a produção exige o token
   "Authorization: bearer" que a coleção do Postman prevê.
2. Juntar a branch `feature/webmotors` no master e publicar.
3. No `.env` da VM: `WEBMOTORS_BASE_URL=https://integracao.webmotors.com.br`
   (e `WEBMOTORS_TOKEN`, se a Webmotors mandar). Sem isso fica na homologação
   (a tela avisa "ambiente de testes").
4. A loja pede à Webmotors o usuário de integração dela e conecta em Integrações.

## Onde está o código

- Backend: `Infrastructure/Services/Webmotors/` (`WebmotorsService`,
  `WebmotorsSoap` monta/lê o XML, `WebmotorsSugestao` faz o palpite),
  `API/Controllers/WebmotorsController.cs` (`/api/integracoes/webmotors`),
  tabela `VeiculoWebmotors`.
- Gatilhos: `PublicacaoAutomaticaService` (venda, inativação, estorno) e
  `AtualizarVeiculoUseCase` (edição).
- Telas: `shared/components/webmotors/` (cartão em Integrações e janela do veículo).
- Testes: `WebmotorsServiceTests` (Webmotors simulada) e `WebmotorsRealTests` (real, opcional).
