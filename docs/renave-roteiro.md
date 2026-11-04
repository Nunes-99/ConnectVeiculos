# Renave: da homologação à produção

Estoque eletrônico de veículos da Senatran (Renave-WS, do Serpro). A loja opera
com o **certificado A1 dela**: é o mesmo modelo do WCheck (conferido em
04/11/2026). O ConnectVeiculos é só o sistema; quem contrata o Serpro e paga as
operações é a loja.

Documentação da API (swagger, aberto): https://hom.renave.estaleiro.serpro.gov.br/renave-ws/swagger-ui.html
(grupo "Estabelecimento (Concessionária ou Revenda)").

## O que o sistema faz

Em **Integrações → Renave** (com operador e e-mail salvos):

- **Importar estoque do Renave**: para a loja que já lançava no Renave por outro
  sistema. Liga cada estoque ao veículo com a mesma placa/chassi; o que não
  existir é cadastrado com o mínimo (placa, chassi, Renavam, km) como
  **Reservado**, fora do catálogo e das redes, com o modelo "Importado do
  Renave". Não faz nova entrada. Rodar de novo não duplica.
- **Zero km pendentes**: o que a montadora mandou para a loja. Dar entrada
  (pede a chave da NF-e da montadora) ou rejeitar (desistência / erro da
  montadora). A entrada cadastra o carro como Reservado se ele ainda não existir.
- **Entregas**: zero km vendido por outra concessionária em que a montadora
  indicou esta loja para entregar. Registrar a entrega ou cancelar.
- **Consultar veículo**: por placa, chassi ou Renavam — restrições e os
  estoques do veículo na loja.
- **Testes** (só em homologação): cria um zero km fictício.

Na janela do veículo (Veículos → Renave):

- Usado: aptidão → entrada → assinatura do vendedor → NF-e de compra →
  Detran → saída → ATPVe → NF-e de venda. **Termo de entrada** (depois da
  assinatura) e **Termo de saída** para baixar.
- Zero km: entrada → saída (sem CRV) → NF-e de venda. Sem ATPVe e sem termos.

## O que a documentação não diz (homologação, 04/11/2026)

- Entrada de zero km **exige** `chaveNotaFiscal`, embora o swagger marque como
  opcional ("Chave da Nota fiscal é obrigatório").
- Saída de zero km também **exige** a chave da NF-e de venda, junto na mesma
  chamada (no usado ela é informada depois). A tela pede o campo no zero km.
- Na saída, o Renave exige **todos** os dados do comprador — nome, e-mail,
  logradouro, número e bairro — mas reclama de um por vez. O sistema confere
  tudo antes e lista o que falta numa mensagem só.
- Consultar entregas exige ao menos um filtro ("Deve ser informado pelo menos
  um parâmetro de pesquisa"): "Todas" consulta cada situação e junta.
- Termo de entrada/saída só existe para **usado** (e estoque de ITE); para zero
  km o Renave recusa.
- Termo de entrada só depois de enviada a **assinatura do ATPV** do vendedor.
- A loja de teste da homologação (CNPJ 85106037000585) é compartilhada por
  todos os integradores: tem centenas de zero km pendentes e um estoque enorme.
  Listar o estoque com período longo estourou o tempo e o serviço ficou fora
  (503) por vários minutos. Por isso o teste real da importação só roda com
  `RENAVE_TESTE_IMPORTAR=1`. Para uma loja de verdade a lista é pequena.
- Sem filtro de data, `GET /api/estoques` só devolve entradas dos últimos 3
  meses. A importação pede desde 2020; se o Renave recusar o período, usa o
  padrão e a tela avisa.

## Testar (homologação, sem certificado)

    cd back-end
    RENAVE_HOMOLOGACAO=1 dotnet test ConnectVeiculos.Tests --filter RenaveHomologacaoTests --logger "console;verbosity=detailed"

Faz o fluxo do usado (com termo de entrada) e o do zero km (pendente → entrada →
consulta → saída → entregas → rejeição de outro zero km).

## Ir para produção (depende da loja)

1. A loja se credencia no **Credencia** (Senatran) com o e-CNPJ, serviço
   "RENAVE concessionário/lojista".
2. A loja assina o **contrato com o Serpro** (Loja Serpro ou Área do Cliente)
   e indica o responsável financeiro. Cada operação é cobrada.
3. A loja habilita o certificado A1 (parte pública, .cer) no ambiente
   **Produção** do Renave.
4. No ConnectVeiculos: certificado em **Notas fiscais → Configuração**;
   em **Integrações → Renave**, ambiente Produção, operador e e-mail; **Testar
   conexão** tem que mostrar o CNPJ da loja.
5. Se a loja já usava o Renave por outro sistema: **Importar do Renave**.
6. Primeira entrada e primeira saída reais acompanhadas.

## Onde está o código

- Backend: `Infrastructure/Services/Renave/RenaveService.cs` (usado, config,
  HTTP) e `RenaveService.Estoque.cs` (importar, zero km, entregas, consulta,
  termos); `API/Controllers/RenaveController.cs` (`/api/renave`).
- Telas: `shared/components/renave/` — `renave-config` (cartão),
  `renave-operacoes` (abas) e `renave-veiculo` (janela do veículo).
- Testes: `RenaveServiceTests` e `RenaveEstoqueTests` (Renave simulado),
  `RenaveHomologacaoTests` (real, opcional).
