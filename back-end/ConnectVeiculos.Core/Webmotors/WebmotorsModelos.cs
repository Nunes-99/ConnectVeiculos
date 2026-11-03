namespace ConnectVeiculos.Core.Webmotors
{
    /// <summary>Erro que a tela mostra como esta (422).</summary>
    public class WebmotorsException : Exception
    {
        public WebmotorsException(string mensagem) : base(mensagem) { }
    }

    /// <summary>Item de tabela da Webmotors (marca, modelo, cor, cambio...).</summary>
    public class WebmotorsItem
    {
        public long Codigo { get; set; }
        public string Nome { get; set; } = "";
    }

    public class WebmotorsVersao : WebmotorsItem
    {
        public List<int> Anos { get; set; } = new();
    }

    public class WebmotorsModalidade : WebmotorsItem
    {
        public string TipoAnuncio { get; set; } = "";
        public int Limite { get; set; }
        public int EmUso { get; set; }
    }

    public class WebmotorsConectarInput
    {
        public string Cnpj { get; set; } = "";
        public string Email { get; set; } = "";
        public string Senha { get; set; } = "";
    }

    public class WebmotorsStatus
    {
        public bool Conectado { get; set; }
        public bool Homologacao { get; set; }
        public string? Cnpj { get; set; }
        public string? Email { get; set; }
        public DateTime? ConectadoEm { get; set; }
        public long? ModalidadeId { get; set; }
        public List<WebmotorsModalidade> Modalidades { get; set; } = new();
        public string? Erro { get; set; }
    }

    /// <summary>
    /// O que a Webmotors exige alem do cadastro do veiculo: os codigos das
    /// tabelas dela e as caracteristicas S/N do anuncio.
    /// </summary>
    public class WebmotorsAnuncioDados
    {
        public long? MarcaId { get; set; }
        public long? ModeloId { get; set; }
        public long? VersaoId { get; set; }
        public int? AnoModelo { get; set; }
        public int? AnoFabricacao { get; set; }
        public long? CambioId { get; set; }
        public long? CombustivelId { get; set; }
        public long? CorId { get; set; }
        public int? Portas { get; set; }
        public List<long> Opcionais { get; set; } = new();

        public bool UnicoDono { get; set; }
        public bool IpvaPago { get; set; }
        public bool Licenciado { get; set; }
        public bool Blindado { get; set; }
        public bool Alienado { get; set; }
        public bool GarantiaDeFabrica { get; set; }
        public bool RevisoesEmConcessionaria { get; set; }
        public bool AdaptadoPcd { get; set; }
        public bool NaoAceitaTroca { get; set; }
    }

    public class WebmotorsVeiculoInfo
    {
        public bool Publicado { get; set; }
        public long? CodigoAnuncio { get; set; }
        public string Status { get; set; } = "";
        public string? Erro { get; set; }
        public DateTime? PublicadoEm { get; set; }
        public DateTime? AtualizadoEm { get; set; }
        public int Fotos { get; set; }

        /// <summary>Dados salvos, ou a sugestao montada a partir do cadastro.</summary>
        public WebmotorsAnuncioDados Dados { get; set; } = new();

        /// <summary>True quando Dados ainda e' so palpite (nada salvo).</summary>
        public bool Sugestao { get; set; }
    }
}
