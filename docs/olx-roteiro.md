# OLX: do cadastro de integrador à produção

Anúncios de **carros** na OLX pela API de importação ("autoupload"). A loja
conecta a **conta dela na OLX** por OAuth; o ConnectVeiculos é o integrador,
com `client_id` e `client_secret` próprios (os mesmos para todas as lojas).
A OLX não tem ambiente de testes: tudo é produção.

Documentação: https://developers.olx.com.br/anuncio/api/home.html

## Situação (26/11/2026)

- Código pronto (esta branch), testado com a OLX simulada no formato da
  documentação (`OlxServiceTests`). **Falta o cadastro de integrador**: sem
  `OLX_CLIENT_ID`/`OLX_CLIENT_SECRET` o cartão mostra "em liberação".
- A **loja** precisa de plano **profissional** da OLX (Essencial, Plus ou
  Premium Empresa). Plano de autônomo volta "sem permissão" (statusCode -6).

## 1. Cadastro de integrador (Vitor)

E-mail para **suporteintegrador@olxbr.com** (ou WhatsApp (21) 3199-8540,
opção "Integrações") com: nome do cliente, nome da aplicação
(ConnectVeiculos), descrição, site (https://connectveiculos.dev.br),
telefone, e-mail e a URL de redirecionamento:

    https://connectveiculos.dev.br/integracoes/olx

Pedir o escopo **autoupload** (e basic_user_info). A OLX devolve
`client_id` e `client_secret`.

## 2. Chaves no servidor

`bash scripts/trocar-chaves.sh` (ver `docs/trocar-chaves.md`) e preencher
`OLX_CLIENT_ID` e `OLX_CLIENT_SECRET`. O cartão da OLX em Integrações passa a
mostrar **Conectar conta da OLX**.

## Como funciona

- **Integrações → OLX**: *Conectar conta da OLX* → login na OLX → volta para
  `/integracoes/olx`. O cartão mostra o saldo de anúncios do plano
  (`GET /autoupload/balance`). O token da OLX não tem renovação: se a OLX
  recusar (401), a loja conecta de novo.
- **Veículos → menu ⋮ → Anunciar na OLX**: o sistema sugere marca, modelo e
  versão do catálogo da OLX (`car_info`) e os códigos (combustível, câmbio,
  cor, portas, motor, opcionais) a partir do cadastro; a pessoa confere e
  anuncia. Preço, km, placa, observação e até **20 fotos** vêm do veículo;
  **telefone e CEP vêm do cadastro da loja** (sem eles o botão fica travado e a
  janela diz o que falta).
- A OLX processa o envio **em segundo plano**: o envio devolve um token e o
  resultado (no ar, recusado e o motivo, link do anúncio) aparece ao abrir a
  janela de novo (`POST /autoupload/import/{token}`).
- Depois é automático: editar (preço, km, fotos...) reenvia só se o anúncio
  mudou; vender/inativar/reservar tira do ar (a OLX não tem "vendido");
  voltar a disponível reanuncia. *Tirar do ar* pela janela só volta pelo
  botão Anunciar.
- Selo roxo **OLX** na lista de Veículos (abre a janela).
- Id do anúncio na OLX = `cv{id do veículo}`; "insert" com o mesmo id altera.

## Limites e cuidados

- Cada anúncio consome o saldo do plano da loja na OLX (-7/-8 = sem saldo).
- Só carros (categoria 2020). Motos e caminhões usam outro catálogo
  (`moto_info`) e ficam para depois.
- A OLX confere a placa de carro usado (INVALID_PLATE recusa).
- Campo opcional vazio ou 0 faz a OLX recusar: o sistema só manda o que foi
  preenchido.
- Leads da OLX (escopo `autoservice`, webhook) e importar anúncios que a loja
  já tem na OLX ficam para uma próxima etapa.

## Para testar com a primeira loja

1. Loja com plano profissional conecta em Integrações → OLX e confere o saldo.
2. Anunciar um carro com fotos; abrir a janela depois de alguns minutos:
   tem de aparecer "No ar na OLX" e o link.
3. Editar o preço e conferir na OLX; vender e conferir que saiu; estornar e
   conferir que voltou.

## Onde está o código

- Backend: `Infrastructure/Services/Olx/` (`OlxService`, `OlxCodigos`,
  `OlxSettings`), `API/Controllers/OlxController.cs` (`/api/integracoes/olx`),
  tabela `VeiculoOlx`.
- Gatilhos: `PublicacaoAutomaticaService` (venda, inativação, estorno),
  `AtualizarVeiculoUseCase` (edição), `ImagensController` (fotos).
- Telas: `shared/components/olx/` (cartão e janela do veículo),
  `pages/integracoes/olx-retorno.component.ts` (volta do login).
