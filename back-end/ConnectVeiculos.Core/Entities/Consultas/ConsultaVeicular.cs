namespace ConnectVeiculos.Core.Entities.Consultas
{
    /// <summary>
    /// Consulta feita pela loja na ZapCar (placa, CPF/CNPJ, chassi, NF-e...).
    /// O valor sai dos creditos da loja (no master); se a base falhar, volta
    /// para os creditos.
    /// </summary>
    public class ConsultaVeicular
    {
        public const string Processando = "PROCESSANDO";
        public const string Concluida = "CONCLUIDA";
        public const string Erro = "ERRO";

        public int CvcId { get; private set; }
        /// <summary>Placa consultada ("" quando o servico nao usa placa).</summary>
        public string CvcPlaca { get; private set; } = "";
        /// <summary>O que foi consultado quando nao e' placa (CPF/CNPJ mascarado, chassi, chave da NF-e...).</summary>
        public string? CvcIdentificador { get; private set; }
        /// <summary>Servico da ZapCar (ex.: consulta-completa).</summary>
        public string CvcServico { get; private set; } = "";
        /// <summary>Finalidade declarada (LGPD) nas consultas de pessoa.</summary>
        public string? CvcFinalidade { get; private set; }
        public string CvcStatus { get; private set; } = Processando;
        /// <summary>Id da consulta na ZapCar.</summary>
        public string? CvcExternoId { get; private set; }
        /// <summary>Liga a consulta ao debito nos creditos (e ao estorno).</summary>
        public string CvcReferencia { get; private set; } = "";
        /// <summary>Quanto a loja pagou (creditos).</summary>
        public decimal CvcValor { get; private set; }
        /// <summary>Quanto a ZapCar cobrou da plataforma.</summary>
        public decimal? CvcCusto { get; private set; }
        /// <summary>Resposta da ZapCar quando concluida (JSON).</summary>
        public string? CvcResultadoJson { get; private set; }
        public bool CvcTemDocumento { get; private set; }
        public string? CvcErro { get; private set; }
        /// <summary>Feita com a chave de teste (dados ficticios, nada cobrado).</summary>
        public bool CvcTeste { get; private set; }
        public bool CvcEstornada { get; private set; }
        public int R_UsuId { get; private set; }
        public DateTime CvcCriadoEm { get; private set; }
        public DateTime? CvcConcluidoEm { get; private set; }

        public ConsultaVeicular() { }

        public ConsultaVeicular(string placa, string servico, string referencia, decimal valor, int usuarioId,
            string? identificador = null, string? finalidade = null)
        {
            CvcPlaca = placa ?? "";
            CvcServico = servico;
            CvcReferencia = referencia;
            CvcValor = valor;
            R_UsuId = usuarioId;
            CvcIdentificador = identificador;
            CvcFinalidade = finalidade;
            CvcCriadoEm = DateTime.UtcNow;
        }

        public void Enviada(string externoId, decimal? custo, bool teste)
        {
            CvcExternoId = externoId;
            CvcCusto = custo;
            CvcTeste = teste;
        }

        public void Concluir(string? resultadoJson, bool temDocumento)
        {
            CvcStatus = Concluida;
            CvcResultadoJson = resultadoJson;
            CvcTemDocumento = temDocumento;
            CvcErro = null;
            CvcConcluidoEm = DateTime.UtcNow;
        }

        public void Falhar(string erro, bool estornada)
        {
            CvcStatus = Erro;
            CvcErro = erro;
            CvcEstornada = estornada;
            CvcConcluidoEm = DateTime.UtcNow;
        }
    }
}
