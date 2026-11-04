namespace ConnectVeiculos.Core.Entities.Financiamentos
{
    /// <summary>
    /// Banco em que a loja e credenciada para financiar. Guarda a tabela de
    /// taxas que o proprio banco manda para a loja (por prazo, ano do veiculo
    /// e entrada minima): com ela o sistema estima as parcelas de todos os
    /// bancos de uma vez, sem consultar ninguem. A aprovacao continua no banco.
    /// </summary>
    public class BancoFinanciamentoLoja
    {
        public int BflId { get; private set; }
        public string BflNome { get; private set; } = string.Empty;

        /// <summary>Endereco do portal do lojista, para o vendedor abrir com um clique.</summary>
        public string? BflPortalUrl { get; private set; }
        public bool BflAtivo { get; private set; } = true;

        /// <summary>Tarifa de cadastro (TC) cobrada pelo banco.</summary>
        public decimal BflTarifaCadastro { get; private set; }

        /// <summary>Registro do contrato no Detran (gravame).</summary>
        public decimal BflRegistroContrato { get; private set; }

        /// <summary>Se as tarifas entram no valor financiado (o mais comum) ou sao pagas a vista.</summary>
        public bool BflTarifasFinanciadas { get; private set; } = true;

        /// <summary>Faixas de taxa (JSON de FaixaTaxaBanco).</summary>
        public string BflFaixasJson { get; private set; } = "[]";
        public string? BflObservacao { get; private set; }
        public DateTime BflAtualizadoEm { get; private set; }

        public BancoFinanciamentoLoja() { }

        public void Atualizar(string nome, string? portalUrl, bool ativo, decimal tarifaCadastro, decimal registroContrato,
            bool tarifasFinanciadas, string faixasJson, string? observacao)
        {
            BflNome = nome;
            BflPortalUrl = portalUrl;
            BflAtivo = ativo;
            BflTarifaCadastro = tarifaCadastro;
            BflRegistroContrato = registroContrato;
            BflTarifasFinanciadas = tarifasFinanciadas;
            BflFaixasJson = faixasJson;
            BflObservacao = observacao;
            BflAtualizadoEm = DateTime.UtcNow;
        }
    }
}
