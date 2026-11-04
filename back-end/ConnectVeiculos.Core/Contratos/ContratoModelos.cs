namespace ConnectVeiculos.Core.Contratos
{
    /// <summary>Erro de regra mostrado para a pessoa (vira 422 na API).</summary>
    public class ContratoException : Exception
    {
        public ContratoException(string message) : base(message) { }
    }

    public static class TiposContrato
    {
        public const string Venda = "VENDA";
        public const string Compra = "COMPRA";
        public const string Consignacao = "CONSIGNACAO";
        public const string Garantia = "GARANTIA";
        public const string Recibo = "RECIBO";
        public const string Outro = "OUTRO";

        public static readonly string[] Todos = { Venda, Compra, Consignacao, Garantia, Recibo, Outro };

        /// <summary>Como o cliente aparece no contrato.</summary>
        public static string PapelCliente(string tipo) => tipo switch
        {
            Venda => "Comprador",
            Compra => "Vendedor",
            Consignacao => "Consignante",
            _ => "Cliente"
        };

        /// <summary>O cliente e quem estava com o carro antes (vendedor, consignante).</summary>
        public static bool ClienteEhDono(string tipo) => tipo is Compra or Consignacao;
    }

    public class ModeloContratoInfo
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public string Tipo { get; set; } = TiposContrato.Outro;
        public string Conteudo { get; set; } = string.Empty;
        public bool Ativo { get; set; }
        public bool Padrao { get; set; }
        public DateTime AtualizadoEm { get; set; }
    }

    public class ModeloContratoInput
    {
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public string Tipo { get; set; } = TiposContrato.Outro;
        public string Conteudo { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
    }

    public class ContratoVariavel
    {
        public string Chave { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Grupo { get; set; } = string.Empty;
    }

    /// <summary>O cliente do contrato (comprador, vendedor ou consignante).</summary>
    public class ContratoParte
    {
        public string Nome { get; set; } = string.Empty;
        public string? Documento { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public string? Endereco { get; set; }
    }

    public class ContratoGerarInput
    {
        public int ModeloId { get; set; }
        public int VeiculoId { get; set; }
        public ContratoParte Parte { get; set; } = new();
        public decimal? Valor { get; set; }
        public string? FormaPagamento { get; set; }
        /// <summary>Campos livres do modelo ({{extra.garantia_dias}} etc.).</summary>
        public Dictionary<string, string>? Extras { get; set; }
    }

    /// <summary>Preenche o formulario de geracao com o que o sistema ja sabe.</summary>
    public class ContratoSugestao
    {
        public string Tipo { get; set; } = TiposContrato.Outro;
        public string PapelCliente { get; set; } = string.Empty;
        public ContratoParte Parte { get; set; } = new();
        public decimal? Valor { get; set; }
        public string? FormaPagamento { get; set; }
        /// <summary>De onde veio o cliente: "venda", "veiculo" ou nulo.</summary>
        public string? Origem { get; set; }
        public List<ContratoExtra> Extras { get; set; } = new();
    }

    public class ContratoExtra
    {
        public string Chave { get; set; } = string.Empty;
        public string Rotulo { get; set; } = string.Empty;
    }

    public class ContratoTrecho
    {
        public string Texto { get; set; } = string.Empty;
        public bool Negrito { get; set; }
        /// <summary>Variavel sem valor (aparece como linha para preencher).</summary>
        public bool Faltando { get; set; }
    }

    public class ContratoBloco
    {
        /// <summary>"titulo", "secao" ou "paragrafo".</summary>
        public string Tipo { get; set; } = "paragrafo";
        public List<ContratoTrecho> Trechos { get; set; } = new();
    }

    public class ContratoPreview
    {
        public string Titulo { get; set; } = string.Empty;
        public List<ContratoBloco> Blocos { get; set; } = new();
        /// <summary>Variaveis usadas que ficaram sem valor.</summary>
        public List<string> Faltando { get; set; } = new();
    }

    public class ModeloPreviewInput
    {
        public string Conteudo { get; set; } = string.Empty;
        public string Tipo { get; set; } = TiposContrato.Outro;
        public int? VeiculoId { get; set; }
    }

    public class ContratoSignatarioInfo
    {
        public int Id { get; set; }
        public string Papel { get; set; } = string.Empty;
        public string PapelRotulo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string? Documento { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Metodo { get; set; }
        public DateTime? VisualizadoEm { get; set; }
        public DateTime? AssinadoEm { get; set; }
        /// <summary>Token do link publico (so para o cliente, so para a loja).</summary>
        public string? Token { get; set; }
    }

    public class ContratoInfo
    {
        public int Id { get; set; }
        public int VeiculoId { get; set; }
        public string? Veiculo { get; set; }
        public string? Placa { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public DateTime CriadoEm { get; set; }
        public DateTime? ConcluidoEm { get; set; }
        public List<ContratoSignatarioInfo> Signatarios { get; set; } = new();
    }

    public class ContratoFiltro
    {
        public int? VeiculoId { get; set; }
        public string? Status { get; set; }
        public string? Busca { get; set; }
    }

    /// <summary>Rubrica desenhada (PNG em data URL) e quem assina.</summary>
    public class AssinaturaInput
    {
        public string Nome { get; set; } = string.Empty;
        public string? Documento { get; set; }
        public string Imagem { get; set; } = string.Empty;
        /// <summary>So no link publico: o codigo que chegou por e-mail.</summary>
        public string? Codigo { get; set; }
        public bool Aceite { get; set; }
    }

    /// <summary>O que o cliente ve ao abrir o link.</summary>
    public class AssinaturaPublicaInfo
    {
        public string Loja { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public List<ContratoBloco> Blocos { get; set; } = new();
        public string Hash { get; set; } = string.Empty;
        public string Papel { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string? EmailMascarado { get; set; }
        public bool PrecisaCodigo { get; set; }
        public bool JaAssinou { get; set; }
        public DateTime? AssinadoEm { get; set; }
        public string StatusContrato { get; set; } = string.Empty;
    }

    /// <summary>Dados de quem esta assinando pelo link (vindos da requisicao).</summary>
    public class AssinaturaOrigem
    {
        public string? Ip { get; set; }
        public string? Navegador { get; set; }
    }
}
