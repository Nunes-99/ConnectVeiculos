namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IFinanciamentoService
    {
        SimulacaoFinanciamento CalcularPrice(decimal valorVeiculo, decimal entrada, int numeroParcelas, decimal taxaJurosAnual);
        SimulacaoFinanciamento CalcularSAC(decimal valorVeiculo, decimal entrada, int numeroParcelas, decimal taxaJurosAnual);
    }

    public class SimulacaoFinanciamento
    {
        public string Tipo { get; set; } // "PRICE" ou "SAC"
        public decimal ValorVeiculo { get; set; }
        public decimal ValorEntrada { get; set; }
        public decimal ValorFinanciado { get; set; }
        public int NumeroParcelas { get; set; }
        public decimal TaxaJurosAnual { get; set; }
        public decimal TaxaJurosMensal { get; set; }
        public decimal ValorTotalPago { get; set; }
        public decimal TotalJuros { get; set; }
        public decimal CETAnual { get; set; } // Custo Efetivo Total
        public List<ParcelaFinanciamento> Parcelas { get; set; } = new List<ParcelaFinanciamento>();
    }

    public class ParcelaFinanciamento
    {
        public int Numero { get; set; }
        public decimal ValorParcela { get; set; }
        public decimal ValorAmortizacao { get; set; }
        public decimal ValorJuros { get; set; }
        public decimal SaldoDevedor { get; set; }
    }
}
