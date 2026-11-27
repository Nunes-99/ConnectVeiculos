namespace ConnectVeiculos.Core.Entities.Consultas
{
    /// <summary>
    /// Consulta veicular por placa (ZapCar) feita pela loja. O valor sai dos
    /// creditos da loja (no master); se a base falhar, volta para os creditos.
    /// </summary>
    public class ConsultaVeicular
    {
        public const string Processando = "PROCESSANDO";
        public const string Concluida = "CONCLUIDA";
        public const string Erro = "ERRO";

        public int CvcId { get; private set; }
        public string CvcPlaca { get; private set; } = "";
        /// <summary>Servico da ZapCar (ex.: consulta-completa).</summary>
        public string CvcServico { get; private set; } = "";
        public string CvcStatus { get; private set; } = Processando;
        /// <summary>Id da consulta na ZapCar.</summary>
        public string? CvcExternoId { get; private set; }
        /// <summary>Liga a consulta ao debito nos creditos (e ao estorno).</summary>
        public string CvcReferencia { get; private set; } = "";
        /// <summary>Quanto a loja pagou (creditos).</summary>
        public decimal CvcValor { get; private set; }
        /// <summary>Quanto a ZapCar cobrou da plataforma.</summary>
        public decimal? CvcCusto { get; private set; }
        /// <summary>Bloco "veiculo" normalizado da resposta (JSON).</summary>
        public string? CvcResultadoJson { get; private set; }
        public string? CvcErro { get; private set; }
        /// <summary>Feita com a chave de teste (dados ficticios, nada cobrado).</summary>
        public bool CvcTeste { get; private set; }
        public bool CvcEstornada { get; private set; }
        public int R_UsuId { get; private set; }
        public DateTime CvcCriadoEm { get; private set; }
        public DateTime? CvcConcluidoEm { get; private set; }

        public ConsultaVeicular() { }

        public ConsultaVeicular(string placa, string servico, string referencia, decimal valor, int usuarioId)
        {
            CvcPlaca = placa;
            CvcServico = servico;
            CvcReferencia = referencia;
            CvcValor = valor;
            R_UsuId = usuarioId;
            CvcCriadoEm = DateTime.Now;
        }

        public void Enviada(string externoId, decimal? custo, bool teste)
        {
            CvcExternoId = externoId;
            CvcCusto = custo;
            CvcTeste = teste;
        }

        public void Concluir(string? resultadoJson)
        {
            CvcStatus = Concluida;
            CvcResultadoJson = resultadoJson;
            CvcErro = null;
            CvcConcluidoEm = DateTime.Now;
        }

        public void Falhar(string erro, bool estornada)
        {
            CvcStatus = Erro;
            CvcErro = erro;
            CvcEstornada = estornada;
            CvcConcluidoEm = DateTime.Now;
        }
    }
}
