using ConnectVeiculos.Core.Exceptions;

namespace ConnectVeiculos.Core.Entities.Financeiro
{
    /// <summary>
    /// Conta a pagar ou a receber da loja (aluguel, salarios, impostos,
    /// parcela a receber...). Vendas, compras de carros e despesas de carros
    /// ja estao no sistema e entram no fluxo de caixa sem lancamento.
    /// </summary>
    public class LancamentoFinanceiro
    {
        public const string Pagar = "P";
        public const string Receber = "R";

        public int LfiId { get; private set; }
        /// <summary>"P" = a pagar (saida), "R" = a receber (entrada).</summary>
        public string LfiTipo { get; private set; }
        public string LfiDescricao { get; private set; }
        public string LfiCategoria { get; private set; }
        public decimal LfiValor { get; private set; }
        public DateTime LfiVencimento { get; private set; }
        /// <summary>Quando foi pago/recebido; nulo = em aberto.</summary>
        public DateTime? LfiPagoEm { get; private set; }
        public int? R_LojId { get; private set; }
        public string LfiObservacao { get; private set; }
        /// <summary>Repete todo mes: ao pagar, o do mes seguinte e' criado.</summary>
        public bool LfiRecorrente { get; private set; }
        public int R_UsuId { get; private set; }
        public DateTime LfiCriadoEm { get; private set; }

        public LancamentoFinanceiro() { }

        public LancamentoFinanceiro(string tipo, string descricao, string categoria, decimal valor, DateTime vencimento,
            int? lojaId, string observacao, bool recorrente, int usuarioId)
        {
            LfiCriadoEm = DateTime.Now;
            R_UsuId = usuarioId;
            Alterar(tipo, descricao, categoria, valor, vencimento, lojaId, observacao, recorrente);
        }

        public bool EmAberto => LfiPagoEm == null;

        public void Alterar(string tipo, string descricao, string categoria, decimal valor, DateTime vencimento,
            int? lojaId, string observacao, bool recorrente)
        {
            if (tipo != Pagar && tipo != Receber) throw new DomainException("Escolha se é conta a pagar ou a receber.");
            if (string.IsNullOrWhiteSpace(descricao)) throw new DomainException("Informe a descrição.");
            if (valor <= 0) throw new DomainException("O valor precisa ser maior que zero.");
            if (vencimento.Year < 2000 || vencimento.Year > 2100) throw new DomainException("Informe o vencimento.");
            LfiTipo = tipo;
            LfiDescricao = descricao.Trim().Length > 200 ? descricao.Trim()[..200] : descricao.Trim();
            LfiCategoria = string.IsNullOrWhiteSpace(categoria) ? "Outros" : categoria.Trim();
            LfiValor = Math.Round(valor, 2);
            LfiVencimento = vencimento.Date;
            R_LojId = lojaId is > 0 ? lojaId : null;
            LfiObservacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
            LfiRecorrente = recorrente;
        }

        public void MarcarPago(DateTime? pagoEm) => LfiPagoEm = pagoEm?.Date;

        /// <summary>O do mes seguinte (mesmo dia; fim de mes ajusta), em aberto.</summary>
        public LancamentoFinanceiro ProximoDaRecorrencia() =>
            new(LfiTipo, LfiDescricao, LfiCategoria, LfiValor, LfiVencimento.AddMonths(1), R_LojId, LfiObservacao, true, R_UsuId);
    }
}
