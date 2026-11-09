namespace ConnectVeiculos.Core.NotasFiscais
{
    public class NfeException : Exception
    {
        public NfeException(string mensagem) : base(mensagem) { }
    }

    /// <summary>
    /// Regras fiscais de uma operacao (compra ou venda de veiculo). Os padroes
    /// sao so ponto de partida: quem define e' o contador da loja.
    /// </summary>
    public class NfeRegraOperacao
    {
        public string NaturezaOperacao { get; set; } = string.Empty;
        public string CfopInterno { get; set; } = string.Empty;
        public string CfopInterestadual { get; set; } = string.Empty;

        /// <summary>CSOSN (Simples) ou CST (regime normal) do ICMS.</summary>
        public string CstIcms { get; set; } = string.Empty;
        public decimal AliquotaIcms { get; set; }
        /// <summary>Reducao da base de calculo do ICMS, em % (ex.: 95 = base de 5% do valor).</summary>
        public decimal ReducaoBaseIcms { get; set; }

        public string CstPis { get; set; } = "49";
        public string CstCofins { get; set; } = "49";

        /// <summary>Aliquota do PIS em % (so para CST 01 e 02, tributado).</summary>
        public decimal AliquotaPis { get; set; }
        /// <summary>Aliquota da COFINS em % (so para CST 01 e 02, tributado).</summary>
        public decimal AliquotaCofins { get; set; }

        /// <summary>CST do IBS/CBS (3 digitos): 000 tributacao integral, 200 com reducao, 410 imunidade/nao incidencia.</summary>
        public string CstIbsCbs { get; set; } = "000";
        /// <summary>Classificacao tributaria do IBS/CBS (cClassTrib, 6 digitos).</summary>
        public string ClassTribIbsCbs { get; set; } = "000001";
        /// <summary>Reducao da aliquota do IBS/CBS em % (CST 200).</summary>
        public decimal ReducaoIbsCbs { get; set; }
    }

    /// <summary>Emitente (a loja) e padroes da NF-e. Nada secreto.</summary>
    public class NfeConfigInfo
    {
        public string Ambiente { get; set; } = "homologacao";

        public string? Cnpj { get; set; }
        public string? InscricaoEstadual { get; set; }
        public string? RazaoSocial { get; set; }
        public string? NomeFantasia { get; set; }
        /// <summary>1 Simples Nacional, 2 Simples excesso de sublimite, 3 Regime normal, 4 MEI.</summary>
        public int Crt { get; set; } = 1;

        public string? Logradouro { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cep { get; set; }
        /// <summary>Codigo IBGE do municipio (7 digitos).</summary>
        public int? CodigoMunicipio { get; set; }
        public string? Municipio { get; set; }
        public string? Uf { get; set; }
        public string? Telefone { get; set; }

        public int Serie { get; set; } = 1;
        public int ProximoNumero { get; set; } = 1;

        /// <summary>NCM padrao de automovel; varia pela cilindrada, confirme com o contador.</summary>
        public string NcmPadrao { get; set; } = "87032310";

        /// <summary>
        /// CEST padrao do veiculo (7 digitos). A Sefaz exige o CEST quando o ICMS
        /// e' por substituicao tributaria (CST 60 / CSOSN 500), o caso comum de
        /// carro zero km. Fora disso nao vai na nota.
        /// </summary>
        public string? CestPadrao { get; set; }

        public NfeRegraOperacao Compra { get; set; } = new();
        public NfeRegraOperacao Venda { get; set; } = new();

        /// <summary>
        /// Devolucao de venda (NF-e de entrada, finalidade 4, referenciando a
        /// nota de venda). Usada para desfazer a venda depois das 24 horas do
        /// cancelamento.
        /// </summary>
        public NfeRegraOperacao? Devolucao { get; set; }

        /// <summary>
        /// Reforma tributaria (LC 214/2025): informa o IBS e a CBS na nota
        /// (grupo IBSCBS). Obrigatorio para o regime normal; as aliquotas mudam
        /// a cada ano da transicao — confirme com o contador.
        /// </summary>
        public bool IbsCbsAtivo { get; set; }
        /// <summary>Aliquota da CBS em % (2026: 0,9).</summary>
        public decimal AliquotaCbs { get; set; } = 0.9m;
        /// <summary>Aliquota do IBS estadual em % (2026: 0,1).</summary>
        public decimal AliquotaIbsUf { get; set; } = 0.1m;
        /// <summary>Aliquota do IBS municipal em % (2026: 0).</summary>
        public decimal AliquotaIbsMun { get; set; }

        /// <summary>
        /// Manda XML e DANFE para o e-mail do cliente assim que a nota e'
        /// autorizada (so em producao; a loja precisa do e-mail configurado em
        /// Integracoes -> E-mail).
        /// </summary>
        public bool EnviarEmailAoAutorizar { get; set; } = true;

        /// <summary>Texto fixo nas informacoes complementares (ex.: optante pelo Simples).</summary>
        public string? InformacoesComplementares { get; set; }

        // Responsavel tecnico (grupo infRespTec), exigido por algumas Sefaz.
        public string? RespTecCnpj { get; set; }
        public string? RespTecContato { get; set; }
        public string? RespTecEmail { get; set; }
        public string? RespTecTelefone { get; set; }

        public bool CertificadoValido { get; set; }
        public string? CertificadoCnpj { get; set; }

        /// <summary>Emitente completo, certificado valido e do mesmo CNPJ.</summary>
        public bool Pronto { get; set; }
        public List<string> Pendencias { get; set; } = new();
    }

    public class NfeEndereco
    {
        public string? Logradouro { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cep { get; set; }
        public int CodigoMunicipio { get; set; }
        public string? Municipio { get; set; }
        public string? Uf { get; set; }
    }

    /// <summary>
    /// Dados ja conhecidos para preencher uma nota (ou a saida do Renave):
    /// vem da nota anterior do veiculo, da venda registrada ou do Renave.
    /// </summary>
    public class NfeSugestao
    {
        public NfeEmissaoInput Dados { get; set; } = new();

        /// <summary>De onde vieram os dados ("nota", "venda", "renave"), ou null se nada foi achado.</summary>
        public string? Origem { get; set; }
    }

    /// <summary>Dados de uma nota de veiculo: a outra parte (vendedor na compra, comprador na venda) e os valores.</summary>
    public class NfeEmissaoInput
    {
        /// <summary>ENTRADA (compra), SAIDA (venda) ou DEVOLUCAO (devolucao de venda).</summary>
        public string Tipo { get; set; } = "SAIDA";

        /// <summary>Devolucao: chave da NF-e de venda que esta sendo devolvida.</summary>
        public string? ChaveReferenciada { get; set; }

        public string Documento { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string? InscricaoEstadual { get; set; }

        /// <summary>
        /// Situacao do cliente no ICMS: "contribuinte" (tem IE), "isento"
        /// (contribuinte isento de inscricao) ou "nao_contribuinte". Vazio: decide
        /// pela inscricao estadual (com IE = contribuinte, sem = nao contribuinte).
        /// </summary>
        public string? IndicadorIe { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public NfeEndereco Endereco { get; set; } = new();

        public decimal Valor { get; set; }
        public string? Ncm { get; set; }

        /// <summary>Codigo do meio de pagamento da NF-e (01 dinheiro, 03 cartao, 17 Pix, 90 sem pagamento, 99 outros...).</summary>
        public string MeioPagamento { get; set; } = "99";
        public string? InformacoesComplementares { get; set; }

        /// <summary>CEST deste item; vazio usa o padrao da configuracao.</summary>
        public string? Cest { get; set; }

        /// <summary>
        /// Venda interestadual a consumidor final nao contribuinte por loja do
        /// regime normal: aliquota interna do ICMS na UF do cliente (%), para o
        /// diferencial de aliquota (DIFAL, EC 87/2015). Sem ela a Sefaz rejeita.
        /// </summary>
        public decimal? AliquotaInternaDestino { get; set; }

        /// <summary>Fundo de Combate a Pobreza da UF do cliente (%), quando houver.</summary>
        public decimal? PercentualFcpDestino { get; set; }

        /// <summary>
        /// Veiculo novo (zero km): a Sefaz exige o detalhamento especifico do
        /// veiculo (grupo veicProd). Nulo para usado.
        /// </summary>
        public NfeVeiculoNovo? VeiculoNovo { get; set; }
    }

    /// <summary>
    /// Detalhamento de veiculo novo (grupo veicProd da NF-e). Os codigos vem da
    /// nota da montadora; os da Denatran seguem as tabelas do Renavam.
    /// </summary>
    public class NfeVeiculoNovo
    {
        /// <summary>1 venda concessionaria, 2 faturamento direto a consumidor final, 3 venda direta a grandes consumidores, 0 outros.</summary>
        public int TipoOperacao { get; set; } = 1;
        public string Chassi { get; set; } = string.Empty;
        /// <summary>Codigo da cor na montadora (ate 4 caracteres).</summary>
        public string CodigoCor { get; set; } = string.Empty;
        public string DescricaoCor { get; set; } = string.Empty;
        /// <summary>Potencia do motor em CV.</summary>
        public string Potencia { get; set; } = string.Empty;
        /// <summary>Cilindradas (cm3).</summary>
        public string Cilindradas { get; set; } = string.Empty;
        /// <summary>Peso liquido em toneladas (4 casas).</summary>
        public string PesoLiquido { get; set; } = string.Empty;
        /// <summary>Peso bruto em toneladas (4 casas).</summary>
        public string PesoBruto { get; set; } = string.Empty;
        public string NumeroSerie { get; set; } = string.Empty;
        /// <summary>Codigo do combustivel na tabela do Renavam (ex.: 16 alcool/gasolina).</summary>
        public string TipoCombustivel { get; set; } = string.Empty;
        public string NumeroMotor { get; set; } = string.Empty;
        /// <summary>Capacidade maxima de tracao em toneladas (4 casas).</summary>
        public string CapacidadeTracao { get; set; } = string.Empty;
        /// <summary>Distancia entre eixos (mm).</summary>
        public string DistanciaEixos { get; set; } = string.Empty;
        public int AnoModelo { get; set; }
        public int AnoFabricacao { get; set; }
        /// <summary>Tipo de pintura (ex.: S solida, M metalica, P perolizada).</summary>
        public string TipoPintura { get; set; } = string.Empty;
        /// <summary>Tipo de veiculo na tabela do Renavam (06 automovel, 13 caminhonete...).</summary>
        public string TipoVeiculo { get; set; } = "06";
        /// <summary>Especie na tabela do Renavam (1 passageiro, 2 carga, 3 misto...).</summary>
        public string EspecieVeiculo { get; set; } = "1";
        /// <summary>VIN remarcado (false = normal).</summary>
        public bool VinRemarcado { get; set; }
        /// <summary>1 acabado, 2 inacabado, 3 semiacabado.</summary>
        public int Condicao { get; set; } = 1;
        /// <summary>Codigo marca/modelo da Denatran (6 digitos).</summary>
        public int CodigoMarcaModelo { get; set; }
        /// <summary>Cor na tabela da Denatran (01 amarelo ... 16 fantasia).</summary>
        public string CorDenatran { get; set; } = string.Empty;
        /// <summary>Lotacao maxima de passageiros, com o motorista.</summary>
        public int Lotacao { get; set; } = 5;
        /// <summary>0 nao ha, 1 alienacao fiduciaria, 2 arrendamento, 3 reserva de dominio, 4 penhor, 9 outras.</summary>
        public int Restricao { get; set; }
    }

    public class NfeResumo
    {
        public int Id { get; set; }
        public int VeiculoId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Ambiente { get; set; } = string.Empty;
        public int Serie { get; set; }
        public int Numero { get; set; }
        public string? Chave { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? CStat { get; set; }
        public string? Motivo { get; set; }
        public string? Protocolo { get; set; }
        public DateTime DataEmissao { get; set; }
        public DateTime? DataAutorizacao { get; set; }
        public decimal Valor { get; set; }
        public string? DestinatarioNome { get; set; }
        public DateTime? DataCancelamento { get; set; }
        public bool PodeReenviar { get; set; }
        public bool PodeCancelar { get; set; }

        /// <summary>Carta de correcao em vigor (a ultima registrada), se houver.</summary>
        public int CartaCorrecaoSequencia { get; set; }
        public string? CartaCorrecaoTexto { get; set; }
        public DateTime? CartaCorrecaoData { get; set; }
        public bool PodeCorrigir { get; set; }

        /// <summary>Devolucao: chave da nota de venda devolvida.</summary>
        public string? ChaveReferenciada { get; set; }
        /// <summary>Venda autorizada ainda nao devolvida.</summary>
        public bool PodeDevolver { get; set; }
        /// <summary>Ultimo envio de XML e DANFE ao cliente.</summary>
        public string? EmailEnviadoPara { get; set; }
        public DateTime? EmailEnviadoEm { get; set; }
        /// <summary>E-mail informado na emissao, para sugerir o envio.</summary>
        public string? EmailCliente { get; set; }

        /// <summary>So na lista geral (tela Notas fiscais): qual veiculo.</summary>
        public string? Veiculo { get; set; }
        public string? Placa { get; set; }
    }

    /// <summary>Filtro da tela Notas fiscais. As datas sao da emissao, dia inteiro.</summary>
    public class NfeFiltro
    {
        public DateTime? De { get; set; }
        public DateTime? Ate { get; set; }
        public string? Tipo { get; set; }
        public string? Status { get; set; }
        /// <summary>Numero da nota, destinatario, placa, veiculo ou chave.</summary>
        public string? Busca { get; set; }
    }

    public class NfeInutilizacaoInput
    {
        public int Serie { get; set; }
        public int NumeroInicial { get; set; }
        public int NumeroFinal { get; set; }
        public string Justificativa { get; set; } = string.Empty;
    }

    public class NfeInutilizacaoInfo
    {
        public int Id { get; set; }
        public string Ambiente { get; set; } = string.Empty;
        public int Serie { get; set; }
        public int NumeroInicial { get; set; }
        public int NumeroFinal { get; set; }
        public string Justificativa { get; set; } = string.Empty;
        public string? Protocolo { get; set; }
        public DateTime Data { get; set; }
    }

    public class NfeMunicipio
    {
        public int Codigo { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Uf { get; set; } = string.Empty;
    }

    public class NfeStatusSefaz
    {
        public int CStat { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string Ambiente { get; set; } = string.Empty;
    }
}
