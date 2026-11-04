using ConnectVeiculos.Core.Contratos;

namespace ConnectVeiculos.Infrastructure.Services.Contratos
{
    /// <summary>
    /// Modelos que vem prontos para a loja comecar. Texto proprio do
    /// ConnectVeiculos, para o advogado ou o contador da loja revisar; a loja
    /// edita a vontade em Contratos -> Modelos.
    /// </summary>
    public static class ModelosPadrao
    {
        public record Modelo(string Nome, string Descricao, string Tipo, string Conteudo);

        private const string Revisar = "Modelo inicial do sistema: revise com o advogado ou o contador da loja.";

        public static readonly IReadOnlyList<Modelo> Todos = new List<Modelo>
        {
            new("Contrato de compra e venda", "A loja vende o veículo ao cliente. " + Revisar, TiposContrato.Venda, @"# CONTRATO DE COMPRA E VENDA DE VEÍCULO

**VENDEDORA:** {{loja.nome}}, CNPJ {{loja.cnpj}}, com sede em {{loja.endereco}}.

**COMPRADOR(A):** {{cliente.nome}}, CPF/CNPJ {{cliente.documento}}, residente em {{cliente.endereco}}, telefone {{cliente.telefone}}, e-mail {{cliente.email}}.

As partes acima ajustam a compra e venda do veículo abaixo, nas condições seguintes.

## 1. Do veículo

{{veiculo.marca}} {{veiculo.modelo}}, ano {{veiculo.ano}}, cor {{veiculo.cor}}, placa {{veiculo.placa}}, chassi {{veiculo.chassi}}, Renavam {{veiculo.renavam}}, com {{veiculo.km}} km no hodômetro na data da entrega.

## 2. Do preço e do pagamento

O preço é de **{{contrato.valor}}** ({{contrato.valor_extenso}}), pago da seguinte forma: {{contrato.forma_pagamento}}. Havendo financiamento, a venda só se conclui com a liberação do crédito pela instituição financeira.

## 3. Da entrega e da documentação

O veículo é entregue ao COMPRADOR na data de assinatura deste contrato. A VENDEDORA registra a saída do veículo no Renave e entrega a autorização de transferência eletrônica (ATPV-e). Cabe ao COMPRADOR fazer a transferência de propriedade no órgão de trânsito no prazo legal de 30 (trinta) dias.

## 4. Do estado do veículo

O COMPRADOR declara que examinou o veículo, que fez ou teve a oportunidade de fazer o test drive e a vistoria que quisesse, e que o recebe no estado em que se encontra, sabendo tratar-se de veículo usado, com o desgaste natural do tempo e do uso. Ficam assegurados os direitos de garantia previstos no Código de Defesa do Consumidor.

## 5. Das multas, impostos e débitos

Multas, impostos, taxas e débitos do veículo com fato gerador até a data da entrega são de responsabilidade da VENDEDORA; os posteriores à entrega, do COMPRADOR, que responde também por infrações de trânsito cometidas a partir de então.

## 6. Do foro

Fica eleito o foro da comarca de {{loja.cidade}}/{{loja.uf}} para resolver qualquer questão deste contrato.

E, por estarem de acordo, as partes assinam este contrato eletronicamente.

{{loja.cidade}}, {{data.extenso}}."),

            new("Contrato de compra pela loja", "A loja compra o veículo de pessoa física ou jurídica. " + Revisar, TiposContrato.Compra, @"# CONTRATO DE COMPRA DE VEÍCULO

**VENDEDOR(A):** {{cliente.nome}}, CPF/CNPJ {{cliente.documento}}, residente em {{cliente.endereco}}, telefone {{cliente.telefone}}, e-mail {{cliente.email}}.

**COMPRADORA:** {{loja.nome}}, CNPJ {{loja.cnpj}}, com sede em {{loja.endereco}}.

As partes acima ajustam a compra, pela COMPRADORA, do veículo abaixo, nas condições seguintes.

## 1. Do veículo

{{veiculo.marca}} {{veiculo.modelo}}, ano {{veiculo.ano}}, cor {{veiculo.cor}}, placa {{veiculo.placa}}, chassi {{veiculo.chassi}}, Renavam {{veiculo.renavam}}, com {{veiculo.km}} km no hodômetro.

## 2. Do preço e do pagamento

O preço é de **{{contrato.valor}}** ({{contrato.valor_extenso}}), pago da seguinte forma: {{contrato.forma_pagamento}}.

## 3. Das declarações do vendedor

O VENDEDOR declara, sob as penas da lei, que é o legítimo proprietário do veículo; que ele está livre de penhora, alienação fiduciária, reserva de domínio, busca e apreensão ou qualquer outro ônus não informado à COMPRADORA; que sua origem é lícita; e que não omitiu defeito oculto, sinistro, adulteração ou histórico de leilão de que tenha conhecimento.

## 4. Da documentação e da entrega

O VENDEDOR entrega o veículo, as chaves e os documentos nesta data e assina a autorização de transferência (ATPV-e ou CRV) em favor da COMPRADORA, que registra a entrada do veículo no Renave.

## 5. Das multas, impostos e débitos

Multas, impostos, taxas e débitos com fato gerador até a data da entrega são de responsabilidade do VENDEDOR, que autoriza a COMPRADORA a descontá-los do preço ou a cobrá-los, caso apareçam depois. Os posteriores à entrega são da COMPRADORA.

## 6. Do foro

Fica eleito o foro da comarca de {{loja.cidade}}/{{loja.uf}} para resolver qualquer questão deste contrato.

E, por estarem de acordo, as partes assinam este contrato eletronicamente.

{{loja.cidade}}, {{data.extenso}}."),

            new("Contrato de consignação", "O dono deixa o veículo para a loja vender. " + Revisar, TiposContrato.Consignacao, @"# CONTRATO DE CONSIGNAÇÃO DE VEÍCULO PARA VENDA

**CONSIGNANTE:** {{cliente.nome}}, CPF/CNPJ {{cliente.documento}}, residente em {{cliente.endereco}}, telefone {{cliente.telefone}}, e-mail {{cliente.email}}.

**CONSIGNATÁRIA:** {{loja.nome}}, CNPJ {{loja.cnpj}}, com sede em {{loja.endereco}}.

## 1. Do objeto

O CONSIGNANTE entrega à CONSIGNATÁRIA, para exposição e venda, o veículo {{veiculo.marca}} {{veiculo.modelo}}, ano {{veiculo.ano}}, cor {{veiculo.cor}}, placa {{veiculo.placa}}, chassi {{veiculo.chassi}}, Renavam {{veiculo.renavam}}, com {{veiculo.km}} km no hodômetro. O veículo continua sendo de propriedade do CONSIGNANTE até a venda.

## 2. Do preço

O CONSIGNANTE autoriza a venda por valor não inferior a **{{contrato.valor}}** ({{contrato.valor_extenso}}), líquido para ele. Venda por valor menor depende de autorização por escrito do CONSIGNANTE.

## 3. Da remuneração da consignatária

Pela intermediação, a CONSIGNATÁRIA recebe {{extra.comissao}}, ou o que exceder o valor líquido do item 2, conforme combinado entre as partes.

## 4. Do prazo

A consignação vale por {{extra.prazo_dias}} dias a partir desta data, renovando-se por igual período se nenhuma das partes se manifestar. O CONSIGNANTE pode retirar o veículo a qualquer tempo, avisando com 48 (quarenta e oito) horas de antecedência e pagando as despesas que tenha autorizado.

## 5. Do repasse

Vendido o veículo, a CONSIGNATÁRIA repassa o valor ao CONSIGNANTE em até {{extra.prazo_repasse_dias}} dias após receber o pagamento do comprador.

## 6. Da guarda e das despesas

A CONSIGNATÁRIA guarda e conserva o veículo enquanto estiver sob sua posse e responde por danos causados por culpa sua. Preparação, reparos e despesas com o veículo dependem de aprovação prévia do CONSIGNANTE.

## 7. Das declarações e débitos

O CONSIGNANTE declara que é o proprietário do veículo, que ele está livre de ônus não informados e que sua origem é lícita. Multas, impostos e débitos do veículo até a data da venda são de responsabilidade do CONSIGNANTE.

## 8. Do foro

Fica eleito o foro da comarca de {{loja.cidade}}/{{loja.uf}} para resolver qualquer questão deste contrato.

E, por estarem de acordo, as partes assinam este contrato eletronicamente.

{{loja.cidade}}, {{data.extenso}}."),

            new("Certificado de garantia", "Garantia de motor e câmbio na venda. " + Revisar, TiposContrato.Garantia, @"# CERTIFICADO DE GARANTIA

{{loja.nome}}, CNPJ {{loja.cnpj}}, concede ao cliente {{cliente.nome}}, CPF/CNPJ {{cliente.documento}}, garantia para o veículo {{veiculo.marca}} {{veiculo.modelo}}, ano {{veiculo.ano}}, placa {{veiculo.placa}}, chassi {{veiculo.chassi}}, nas condições abaixo.

## 1. Prazo e cobertura

A garantia é de **{{extra.garantia_dias}} dias** a partir da entrega do veículo e cobre defeitos de funcionamento do **motor e do câmbio** (partes internas lubrificadas). Esse prazo inclui a garantia legal do Código de Defesa do Consumidor.

## 2. O que não está coberto

Itens de desgaste natural (embreagem, freios, pneus, bateria, suspensão, correias, velas, filtros, lâmpadas, palhetas), lataria, pintura, estofamento, vidros, som e acessórios, além de defeitos causados por mau uso, acidente, falta de manutenção, uso de combustível ou lubrificante inadequado, alteração das características originais ou conserto feito fora da oficina indicada pela loja.

## 3. Como usar

Ao perceber o defeito, o cliente deve avisar a loja antes de qualquer conserto e levar o veículo ao local indicado por ela. O cliente deve manter as revisões e trocas de óleo em dia durante o prazo da garantia.

## 4. Perda da garantia

A garantia é cancelada se o veículo for vendido ou transferido, se o hodômetro for adulterado ou se as condições acima não forem cumpridas.

{{loja.cidade}}, {{data.extenso}}."),

            new("Recibo", "Recibo de pagamento recebido pela loja. " + Revisar, TiposContrato.Recibo, @"# RECIBO

**Valor: {{contrato.valor}}**

{{loja.nome}}, CNPJ {{loja.cnpj}}, declara que recebeu de {{cliente.nome}}, CPF/CNPJ {{cliente.documento}}, a importância de **{{contrato.valor}}** ({{contrato.valor_extenso}}), paga por {{contrato.forma_pagamento}}, referente a {{extra.referente_a}} do veículo {{veiculo.marca}} {{veiculo.modelo}}, ano {{veiculo.ano}}, placa {{veiculo.placa}}.

Para clareza, firma o presente recibo.

{{loja.cidade}}, {{data.extenso}}."),

            new("Declaração de financiamento fora da loja", "O cliente financiou por conta própria, sem intermediação da loja. " + Revisar, TiposContrato.Outro, @"# DECLARAÇÃO DE FINANCIAMENTO CONTRATADO FORA DA LOJA

Eu, {{cliente.nome}}, CPF/CNPJ {{cliente.documento}}, residente em {{cliente.endereco}}, declaro, para os devidos fins, que o pagamento do veículo {{veiculo.marca}} {{veiculo.modelo}}, ano {{veiculo.ano}}, placa {{veiculo.placa}}, chassi {{veiculo.chassi}}, comprado de {{loja.nome}}, CNPJ {{loja.cnpj}}, será feito, no todo ou em parte, com financiamento que **contratei diretamente** com {{extra.instituicao_financeira}}, sem intermediação da loja.

Declaro que escolhi livremente a instituição, as taxas, o prazo e as parcelas; que as condições do financiamento são de minha exclusiva responsabilidade; e que a venda só se conclui com a liberação do crédito à loja. Caso o crédito não seja liberado, a loja pode desfazer a venda, devolvendo-se o que tiver sido pago.

{{loja.cidade}}, {{data.extenso}}.")
        };
    }
}
