namespace ConnectVeiculos.Core.Financiamentos
{
    /// <summary>Recusa da Credere (ou da nossa validacao), com texto para a tela.</summary>
    public class CredereException : Exception
    {
        public CredereException(string mensagem) : base(mensagem) { }
    }

    /// <summary>
    /// Dados extras que alguns bancos pedem na Credere, guardados na ficha
    /// (alem de nome, CPF, telefone e e-mail), e a ultima simulacao feita.
    /// </summary>
    public class CredereFichaDados
    {
        public DateTime? Nascimento { get; set; }
        public bool? PossuiCnh { get; set; }

        /// <summary>"F" ou "M" (dominio gender da Credere).</summary>
        public string? Genero { get; set; }

        /// <summary>Renda mensal em reais.</summary>
        public decimal? RendaMensal { get; set; }
        public string? Cep { get; set; }

        /// <summary>credere_identifier do dominio occupation (ex.: "1" empregado, "11" autonomo).</summary>
        public string? Ocupacao { get; set; }

        public int? ModeloId { get; set; }
        public string? ModeloNome { get; set; }
        public int? AnoFabricacao { get; set; }
        public string? UfLicenciamento { get; set; }
        public string? CidadeLicenciamento { get; set; }
        public List<int> Prazos { get; set; } = new();

        public string? UltimaSimulacao { get; set; }
        public DateTime? UltimaSimulacaoEm { get; set; }
    }

    public class CredereLoja
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Cnpj { get; set; }
        public string? Cidade { get; set; }
        public string? Uf { get; set; }
    }

    public class CredereBancoLoja
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public bool Ativo { get; set; }

        /// <summary>okay, unauthorized ou not_analyzed.</summary>
        public string? Status { get; set; }
    }

    public class CredereStatus
    {
        /// <summary>O ConnectVeiculos tem as chaves de parceiro da Credere (client_id/secret).</summary>
        public bool Configurado { get; set; }
        public bool Conectado { get; set; }
        public string? Conta { get; set; }
        public string? Usuario { get; set; }
        public DateTime? ConectadoEm { get; set; }
        public DateTime? ExpiraEm { get; set; }
        public int? LojaId { get; set; }
        public string? LojaNome { get; set; }
        public List<CredereLoja> Lojas { get; set; } = new();
        public List<CredereBancoLoja> Bancos { get; set; } = new();

        /// <summary>Falha ao falar com a Credere agora (a conexao continua salva).</summary>
        public string? Erro { get; set; }
    }

    public class CredereModeloVeiculo
    {
        public int Id { get; set; }
        public string Marca { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string? Versao { get; set; }
        public int? AnoInicio { get; set; }
        public int? AnoFim { get; set; }
        public string? Fipe { get; set; }
    }

    /// <summary>Campo do cliente que algum banco da loja exige, e quais bancos.</summary>
    public class CredereCampoExigido
    {
        public string Campo { get; set; } = string.Empty;
        public List<string> Bancos { get; set; } = new();
    }

    public class CredereSimulacaoInput
    {
        public DateTime? Nascimento { get; set; }
        public bool? PossuiCnh { get; set; }
        public string? Genero { get; set; }
        public decimal? RendaMensal { get; set; }
        public string? Cep { get; set; }
        public string? Ocupacao { get; set; }

        public int ModeloId { get; set; }
        public string? ModeloNome { get; set; }
        public int AnoFabricacao { get; set; }
        public int AnoModelo { get; set; }
        public string UfLicenciamento { get; set; } = string.Empty;
        public string CidadeLicenciamento { get; set; } = string.Empty;
        public List<int> Prazos { get; set; } = new();
    }

    /// <summary>Uma condicao devolvida por um banco (prazo + entrada).</summary>
    public class CredereCondicao
    {
        public long Id { get; set; }
        public string Banco { get; set; } = string.Empty;
        public string CodigoBanco { get; set; } = string.Empty;
        public int Prazo { get; set; }
        public decimal Entrada { get; set; }
        public decimal? Parcela { get; set; }
        public decimal? UltimaParcela { get; set; }
        public decimal? Financiado { get; set; }
        public decimal? TotalPago { get; set; }
        public decimal? TaxaMensal { get; set; }
        public decimal? CetMensal { get; set; }

        /// <summary>Ainda esperando o banco responder.</summary>
        public bool Processando { get; set; }

        /// <summary>O banco ofereceu esta condicao (success && available).</summary>
        public bool Disponivel { get; set; }
        public bool Aprovado { get; set; }

        /// <summary>3 pre-aprovada, 2 neutra, 1 CPF com restricao, null sem pre-analise.</summary>
        public int? PreAprovacao { get; set; }
        public string? Motivo { get; set; }
        public string? MotivoCodigo { get; set; }

        /// <summary>Condicao extra com a entrada minima/sugerida pelo banco (null = a pedida).</summary>
        public string? Sugestao { get; set; }
        public decimal? EntradaMinima { get; set; }
        public decimal? EntradaSugerida { get; set; }

        /// <summary>Subsidio da fabrica (hoje so Itau gera).</summary>
        public bool SubsidioFabrica { get; set; }
    }

    public class CredereSimulacao
    {
        public string Uuid { get; set; } = string.Empty;
        public bool Concluida { get; set; }
        public DateTime? CriadaEm { get; set; }
        public List<CredereCondicao> Condicoes { get; set; } = new();
    }
}
