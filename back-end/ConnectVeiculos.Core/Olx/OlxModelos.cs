namespace ConnectVeiculos.Core.Olx
{
    /// <summary>Erro que a tela mostra como esta (422).</summary>
    public class OlxException : Exception
    {
        public OlxException(string mensagem) : base(mensagem) { }
    }

    /// <summary>Item do catalogo da OLX (marca, modelo, versao) ou de uma lista fixa (cor, combustivel...).</summary>
    public class OlxItem
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
    }

    public class OlxStatus
    {
        /// <summary>O ConnectVeiculos ja tem client_id/secret da OLX (cadastro de integrador).</summary>
        public bool Configurado { get; set; }
        public bool Conectado { get; set; }
        public DateTime? ConectadoEm { get; set; }

        /// <summary>Nome da conta devolvido pelo saldo da OLX.</summary>
        public string? Conta { get; set; }
        public int? AnunciosUsados { get; set; }
        public int? AnunciosDisponiveis { get; set; }
        public int? AnunciosTotal { get; set; }
        public DateTime? ProximaRenovacao { get; set; }

        /// <summary>Endereco onde a conexao tem de comecar (o da redirect_uri), quando a loja esta em outro.</summary>
        public string? SiteConexao { get; set; }
        public string? Erro { get; set; }
    }

    /// <summary>Codigos fixos da OLX para carros (categoria 2020).</summary>
    public class OlxAtributos
    {
        public List<OlxItem> Combustiveis { get; set; } = new();
        public List<OlxItem> Cambios { get; set; } = new();
        public List<OlxItem> Cores { get; set; } = new();
        public List<OlxItem> Portas { get; set; } = new();
        public List<OlxItem> Tipos { get; set; } = new();
        public List<OlxItem> Direcoes { get; set; } = new();
        public List<OlxItem> Potencias { get; set; } = new();
        public List<OlxItem> Opcionais { get; set; } = new();
    }

    /// <summary>
    /// O que a OLX exige alem do cadastro do veiculo: marca/modelo/versao do
    /// catalogo dela e os codigos das listas. Preco, km, placa, descricao e
    /// fotos vem do cadastro; telefone e CEP, da loja.
    /// </summary>
    public class OlxAnuncioDados
    {
        public string? MarcaId { get; set; }
        public string? ModeloId { get; set; }
        public string? VersaoId { get; set; }
        /// <summary>Nomes escolhidos, para o titulo do anuncio.</summary>
        public string? MarcaNome { get; set; }
        public string? ModeloNome { get; set; }
        public string? VersaoNome { get; set; }
        /// <summary>Ano do modelo (regdate).</summary>
        public int? Ano { get; set; }
        public string? Combustivel { get; set; }
        public string? Cambio { get; set; }
        public string? Cor { get; set; }
        public string? Portas { get; set; }
        public string? Tipo { get; set; }
        public string? Direcao { get; set; }
        public string? Potencia { get; set; }
        public List<string> Opcionais { get; set; } = new();
        public bool? AceitaTroca { get; set; }
        public bool? UnicoDono { get; set; }
    }

    public class OlxVeiculoInfo
    {
        /// <summary>A loja quer o anuncio no ar (enviado a OLX e nao tirado).</summary>
        public bool Publicado { get; set; }
        public string Status { get; set; } = "";
        /// <summary>Situacao do ultimo envio na OLX: pending, queued, accepted, refused, error.</summary>
        public string? Situacao { get; set; }
        public string? Url { get; set; }
        public string? Erro { get; set; }
        public DateTime? PublicadoEm { get; set; }
        public DateTime? AtualizadoEm { get; set; }
        public int Fotos { get; set; }

        public OlxAnuncioDados Dados { get; set; } = new();
        /// <summary>True quando Dados ainda e' so palpite (nada salvo).</summary>
        public bool Sugestao { get; set; }

        /// <summary>O que falta no cadastro (telefone/CEP da loja, foto, placa) antes de anunciar.</summary>
        public List<string> Pendencias { get; set; } = new();
    }
}
