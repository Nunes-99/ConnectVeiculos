namespace ConnectVeiculos.Core.Entities.Contratos
{
    /// <summary>
    /// Modelo de contrato da loja (compra e venda, consignacao, garantia...).
    /// O conteudo e texto com marcacao simples: "# " titulo, "## " secao,
    /// linha em branco separa paragrafos, **negrito** e variaveis {{veiculo.placa}}.
    /// </summary>
    public class ModeloContrato
    {
        public int MdcId { get; private set; }
        public string MdcNome { get; private set; } = string.Empty;
        public string? MdcDescricao { get; private set; }
        public string MdcTipo { get; private set; } = "OUTRO";
        public string MdcConteudo { get; private set; } = string.Empty;
        public bool MdcAtivo { get; private set; } = true;

        /// <summary>Veio pronto com o sistema (a loja pode editar).</summary>
        public bool MdcPadrao { get; private set; }

        public DateTime MdcDtCriacao { get; private set; }
        public DateTime MdcDtAtualizacao { get; private set; }

        public ModeloContrato() { }

        public ModeloContrato(string nome, string? descricao, string tipo, string conteudo, bool padrao = false)
        {
            MdcPadrao = padrao;
            MdcDtCriacao = DateTime.UtcNow;
            Atualizar(nome, descricao, tipo, conteudo, true);
        }

        public void Atualizar(string nome, string? descricao, string tipo, string conteudo, bool ativo)
        {
            MdcNome = nome.Trim();
            MdcDescricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
            MdcTipo = tipo;
            MdcConteudo = conteudo;
            MdcAtivo = ativo;
            MdcDtAtualizacao = DateTime.UtcNow;
        }

        public void Desativar()
        {
            MdcAtivo = false;
            MdcDtAtualizacao = DateTime.UtcNow;
        }
    }
}
