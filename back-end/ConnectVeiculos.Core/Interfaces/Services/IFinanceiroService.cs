namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Financeiro simples da loja: contas a pagar e a receber e o fluxo de
    /// caixa do mes, que junta os lancamentos com o que ja esta no sistema
    /// (vendas, compras de carros e despesas de carros).
    /// </summary>
    public interface IFinanceiroService
    {
        Task<FinanceiroMes> MesAsync(int ano, int mes, int? lojaId);
        Task<LancamentoInfo> CriarAsync(LancamentoInput input, int usuarioId);
        Task<LancamentoInfo> AlterarAsync(int id, LancamentoInput input);
        Task ExcluirAsync(int id);

        /// <summary>Marca como pago/recebido (nulo desfaz). Recorrente pago cria o do mes seguinte.</summary>
        Task<LancamentoInfo> PagarAsync(int id, DateTime? pagoEm);
    }

    public class FinanceiroException : Exception
    {
        public FinanceiroException(string mensagem) : base(mensagem) { }
    }

    public class LancamentoInput
    {
        /// <summary>"P" = a pagar, "R" = a receber.</summary>
        public string Tipo { get; set; } = "P";
        public string Descricao { get; set; } = "";
        public string? Categoria { get; set; }
        public decimal Valor { get; set; }
        public DateTime Vencimento { get; set; }
        public int? LojaId { get; set; }
        public string? Observacao { get; set; }
        public bool Recorrente { get; set; }
        /// <summary>Ja pago/recebido nesta data.</summary>
        public DateTime? PagoEm { get; set; }
    }

    public class LancamentoInfo
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = "P";
        public string Descricao { get; set; } = "";
        public string Categoria { get; set; } = "";
        public decimal Valor { get; set; }
        public DateTime Vencimento { get; set; }
        public DateTime? PagoEm { get; set; }
        public int? LojaId { get; set; }
        public string? Observacao { get; set; }
        public bool Recorrente { get; set; }
        /// <summary>"pago", "atrasado", "vence_hoje", "aberto".</summary>
        public string Situacao { get; set; } = "aberto";
    }

    /// <summary>Movimento do fluxo de caixa (lancamento ou registro automatico).</summary>
    public class MovimentoCaixa
    {
        public DateTime Data { get; set; }
        /// <summary>"entrada" ou "saida".</summary>
        public string Sentido { get; set; } = "saida";
        public string Descricao { get; set; } = "";
        public string Categoria { get; set; } = "";
        public decimal Valor { get; set; }
        /// <summary>"lancamento", "venda", "compra", "despesa".</summary>
        public string Origem { get; set; } = "lancamento";
        /// <summary>Realizado (pago/recebido) ou previsto (em aberto).</summary>
        public bool Realizado { get; set; }
        public int? LancamentoId { get; set; }
        public int? VeiculoId { get; set; }
    }

    public class FinanceiroMes
    {
        public int Ano { get; set; }
        public int Mes { get; set; }
        public decimal EntradasRealizadas { get; set; }
        public decimal SaidasRealizadas { get; set; }
        public decimal EntradasPrevistas { get; set; }
        public decimal SaidasPrevistas { get; set; }
        /// <summary>Realizado: entradas - saidas ja feitas.</summary>
        public decimal SaldoRealizado { get; set; }
        /// <summary>Com o que ainda falta pagar/receber no mes.</summary>
        public decimal SaldoPrevisto { get; set; }

        /// <summary>Contas em aberto vencidas (de qualquer mes).</summary>
        public int Atrasadas { get; set; }
        public decimal ValorAtrasado { get; set; }
        /// <summary>Contas a pagar que vencem nos proximos 7 dias.</summary>
        public int VencemEmBreve { get; set; }

        /// <summary>Lancamentos com vencimento no mes, mais os atrasados de antes.</summary>
        public List<LancamentoInfo> Lancamentos { get; set; } = new();
        public List<MovimentoCaixa> Movimentos { get; set; } = new();
        /// <summary>Saidas realizadas por categoria (para o grafico).</summary>
        public Dictionary<string, decimal> SaidasPorCategoria { get; set; } = new();
    }
}
