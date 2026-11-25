namespace ConnectVeiculos.Core.Napista
{
    /// <summary>Erro que a tela mostra como esta (422).</summary>
    public class NapistaException : Exception
    {
        public NapistaException(string mensagem) : base(mensagem) { }
    }

    /// <summary>Item do catalogo do NaPista (marca, modelo, versao, cor, combustivel...).</summary>
    public class NapistaItem
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
    }

    /// <summary>Loja do NaPista que o usuario conectado pode operar.</summary>
    public class NapistaLoja
    {
        public string SellerId { get; set; } = "";
        public string Nome { get; set; } = "";
        public string? Cnpj { get; set; }
    }

    public class NapistaStatus
    {
        /// <summary>O ConnectVeiculos ja tem client_id do NaPista neste ambiente.</summary>
        public bool Configurado { get; set; }
        public bool Desenvolvimento { get; set; }
        public bool Conectado { get; set; }
        public DateTime? ConectadoEm { get; set; }

        /// <summary>Ate quando a conexao vale sem a loja entrar de novo (refresh token).</summary>
        public DateTime? ValidaAte { get; set; }

        public string? SellerId { get; set; }
        public string? LojaNome { get; set; }
        public List<NapistaLoja> Lojas { get; set; } = new();

        /// <summary>Endereco onde a conexao tem de comecar (o da redirect_uri), quando a loja esta em outro.</summary>
        public string? SiteConexao { get; set; }
        public string? Erro { get; set; }
    }

    /// <summary>Valores aceitos pelo NaPista nos campos de escolha do anuncio.</summary>
    public class NapistaAtributos
    {
        public List<NapistaItem> Cores { get; set; } = new();
        public List<NapistaItem> Combustiveis { get; set; } = new();
        public List<NapistaItem> Cambios { get; set; } = new();
        public List<NapistaItem> Opcionais { get; set; } = new();
    }

    /// <summary>
    /// O que o NaPista exige alem do cadastro do veiculo: a versao do catalogo
    /// dele e os valores das listas (cor, combustivel, cambio, opcionais).
    /// Preco, km, placa e descricao vem do cadastro.
    /// </summary>
    public class NapistaAnuncioDados
    {
        /// <summary>CAR, MOTORCYCLE ou TRUCK.</summary>
        public string Categoria { get; set; } = "CAR";
        public string? MarcaId { get; set; }
        public string? ModeloId { get; set; }
        public string? VersaoId { get; set; }
        public int? AnoModelo { get; set; }
        public int? AnoFabricacao { get; set; }
        public string? Cor { get; set; }
        public string? Combustivel { get; set; }
        public string? Cambio { get; set; }
        public int? Portas { get; set; }
        public bool Blindado { get; set; }
        public List<string> Opcionais { get; set; } = new();
    }

    public class NapistaVeiculoInfo
    {
        public bool Publicado { get; set; }
        public string? AnuncioId { get; set; }
        public string Status { get; set; } = "";
        public string? Erro { get; set; }
        public DateTime? PublicadoEm { get; set; }
        public DateTime? AtualizadoEm { get; set; }
        public int Fotos { get; set; }

        /// <summary>Dados salvos, ou a sugestao montada a partir do cadastro.</summary>
        public NapistaAnuncioDados Dados { get; set; } = new();

        /// <summary>True quando Dados ainda e' so palpite (nada salvo).</summary>
        public bool Sugestao { get; set; }
    }

    /// <summary>Anuncio que ja esta no NaPista (GET /seller/{id}/offers), para importar.</summary>
    public class NapistaAnuncioExistente
    {
        public string AnuncioId { get; set; } = "";
        /// <summary>"MITSUBISHI PAJERO TR4 2.0 FLEX 16V 4X2 AUT." (marca + modelo + versao).</summary>
        public string Titulo { get; set; } = "";
        public string Status { get; set; } = "";
        public int? AnoModelo { get; set; }
        public int? AnoFabricacao { get; set; }
        public int Km { get; set; }
        public decimal Preco { get; set; }
        /// <summary>Placa como veio. O NaPista pode esconder ("******4").</summary>
        public string? Placa { get; set; }
        public bool PlacaEscondida { get; set; }
        public string? Cor { get; set; }
        public int Fotos { get; set; }
        public string? FotoCapa { get; set; }

        /// <summary>Veiculo do ConnectVeiculos ja ligado a este anuncio.</summary>
        public int? VeiculoVinculadoId { get; set; }
        /// <summary>Veiculo do ConnectVeiculos com a mesma placa: importar so liga os dois.</summary>
        public int? VeiculoMesmaPlacaId { get; set; }
        public string? VeiculoDescricao { get; set; }
    }

    public class NapistaImportarItem
    {
        public string AnuncioId { get; set; } = "";
        /// <summary>Placa completa, quando o NaPista mandou escondida.</summary>
        public string? Placa { get; set; }
    }

    public class NapistaImportarPedido
    {
        public int LojaId { get; set; }
        public int CategoriaId { get; set; }
        public List<NapistaImportarItem> Itens { get; set; } = new();
    }

    public class NapistaImportacaoDetalhe
    {
        public string AnuncioId { get; set; } = "";
        public string Titulo { get; set; } = "";
        public bool Sucesso { get; set; }
        /// <summary>"criado", "vinculado" ou "ignorado".</summary>
        public string Acao { get; set; } = "";
        public int? VeiculoId { get; set; }
        public int Fotos { get; set; }
        public string Mensagem { get; set; } = "";
    }

    public class NapistaImportacaoResultado
    {
        public int Criados { get; set; }
        public int Vinculados { get; set; }
        public int Erros { get; set; }
        public List<NapistaImportacaoDetalhe> Detalhes { get; set; } = new();
    }

    public class NapistaLeadsResultado
    {
        public int Encontrados { get; set; }
        public int Importados { get; set; }
        public int JaExistiam { get; set; }
    }
}
