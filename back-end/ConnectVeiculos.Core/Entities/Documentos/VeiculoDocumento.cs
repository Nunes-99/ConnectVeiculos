namespace ConnectVeiculos.Core.Entities.Documentos
{
    public class VeiculoDocumento
    {
        public int DocId { get; private set; }
        public int R_VeiId { get; private set; }
        public string DocTipo { get; private set; } // CRLV, LAUDO_CAUTELAR, TRANSFERENCIA, VISTORIA, OUTROS
        public string DocStatus { get; private set; } // PENDENTE, EM_ANDAMENTO, CONCLUIDO
        public string DocArquivo { get; private set; }
        public string DocObservacao { get; private set; }
        public DateTime? DocDtVencimento { get; private set; }
        public DateTime DocDtCriacao { get; private set; }
        public DateTime? DocDtConclusao { get; private set; }

        public VeiculoDocumento() { }

        public VeiculoDocumento(int docId, int rVeiId, string docTipo, string docStatus,
            string docArquivo, string docObservacao, DateTime? docDtVencimento)
        {
            DocId = docId;
            R_VeiId = rVeiId;
            DocTipo = docTipo;
            DocStatus = string.IsNullOrEmpty(docStatus) ? "PENDENTE" : docStatus;
            DocArquivo = docArquivo;
            DocObservacao = docObservacao;
            DocDtVencimento = docDtVencimento;
            DocDtCriacao = DateTime.Now;
            if (DocStatus == "CONCLUIDO") DocDtConclusao = DateTime.Now;
        }

        public void AlterarStatus(string novoStatus)
        {
            DocStatus = novoStatus;
            DocDtConclusao = novoStatus == "CONCLUIDO" ? DateTime.Now : null;
        }

        public void AtualizarDados(string tipo, string arquivo, string observacao, DateTime? vencimento)
        {
            if (!string.IsNullOrEmpty(tipo)) DocTipo = tipo;
            if (arquivo != null) DocArquivo = arquivo;
            if (observacao != null) DocObservacao = observacao;
            DocDtVencimento = vencimento;
        }
    }
}
