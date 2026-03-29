using ConnectVeiculos.Core.Interfaces.Services;

namespace ConnectVeiculos.Infrastructure.Services.Financiamento
{
    public class FinanciamentoService : IFinanciamentoService
    {
        /// <summary>
        /// Calcula financiamento pela Tabela Price (parcelas fixas)
        /// </summary>
        public SimulacaoFinanciamento CalcularPrice(decimal valorVeiculo, decimal entrada, int numeroParcelas, decimal taxaJurosAnual)
        {
            var valorFinanciado = valorVeiculo - entrada;
            var taxaMensal = taxaJurosAnual / 12 / 100;

            // Formula Price: PMT = PV * [i(1+i)^n] / [(1+i)^n - 1]
            var potencia = (decimal)Math.Pow((double)(1 + taxaMensal), numeroParcelas);
            var valorParcela = valorFinanciado * (taxaMensal * potencia) / (potencia - 1);
            valorParcela = Math.Round(valorParcela, 2);

            var simulacao = new SimulacaoFinanciamento
            {
                Tipo = "PRICE",
                ValorVeiculo = valorVeiculo,
                ValorEntrada = entrada,
                ValorFinanciado = valorFinanciado,
                NumeroParcelas = numeroParcelas,
                TaxaJurosAnual = taxaJurosAnual,
                TaxaJurosMensal = Math.Round(taxaMensal * 100, 4)
            };

            var saldoDevedor = valorFinanciado;
            var totalPago = entrada;
            var totalJuros = 0m;

            for (int i = 1; i <= numeroParcelas; i++)
            {
                var juros = saldoDevedor * taxaMensal;
                var amortizacao = valorParcela - juros;
                saldoDevedor -= amortizacao;

                // Ajuste para ultima parcela
                if (i == numeroParcelas && Math.Abs(saldoDevedor) < 0.01m)
                    saldoDevedor = 0;

                simulacao.Parcelas.Add(new ParcelaFinanciamento
                {
                    Numero = i,
                    ValorParcela = valorParcela,
                    ValorAmortizacao = Math.Round(amortizacao, 2),
                    ValorJuros = Math.Round(juros, 2),
                    SaldoDevedor = Math.Round(saldoDevedor, 2)
                });

                totalPago += valorParcela;
                totalJuros += juros;
            }

            simulacao.ValorTotalPago = Math.Round(totalPago, 2);
            simulacao.TotalJuros = Math.Round(totalJuros, 2);
            simulacao.CETAnual = CalcularCET(valorFinanciado, valorParcela, numeroParcelas);

            return simulacao;
        }

        /// <summary>
        /// Calcula financiamento pelo Sistema SAC (amortizacao constante)
        /// </summary>
        public SimulacaoFinanciamento CalcularSAC(decimal valorVeiculo, decimal entrada, int numeroParcelas, decimal taxaJurosAnual)
        {
            var valorFinanciado = valorVeiculo - entrada;
            var taxaMensal = taxaJurosAnual / 12 / 100;
            var amortizacaoFixa = valorFinanciado / numeroParcelas;

            var simulacao = new SimulacaoFinanciamento
            {
                Tipo = "SAC",
                ValorVeiculo = valorVeiculo,
                ValorEntrada = entrada,
                ValorFinanciado = valorFinanciado,
                NumeroParcelas = numeroParcelas,
                TaxaJurosAnual = taxaJurosAnual,
                TaxaJurosMensal = Math.Round(taxaMensal * 100, 4)
            };

            var saldoDevedor = valorFinanciado;
            var totalPago = entrada;
            var totalJuros = 0m;

            for (int i = 1; i <= numeroParcelas; i++)
            {
                var juros = saldoDevedor * taxaMensal;
                var valorParcela = amortizacaoFixa + juros;
                saldoDevedor -= amortizacaoFixa;

                // Ajuste para ultima parcela
                if (i == numeroParcelas && Math.Abs(saldoDevedor) < 0.01m)
                    saldoDevedor = 0;

                simulacao.Parcelas.Add(new ParcelaFinanciamento
                {
                    Numero = i,
                    ValorParcela = Math.Round(valorParcela, 2),
                    ValorAmortizacao = Math.Round(amortizacaoFixa, 2),
                    ValorJuros = Math.Round(juros, 2),
                    SaldoDevedor = Math.Round(saldoDevedor, 2)
                });

                totalPago += valorParcela;
                totalJuros += juros;
            }

            simulacao.ValorTotalPago = Math.Round(totalPago, 2);
            simulacao.TotalJuros = Math.Round(totalJuros, 2);

            // Para SAC, usar a primeira parcela para calculo do CET
            var primeiraParcela = simulacao.Parcelas.First().ValorParcela;
            simulacao.CETAnual = CalcularCET(valorFinanciado, primeiraParcela, numeroParcelas);

            return simulacao;
        }

        /// <summary>
        /// Calcula o Custo Efetivo Total aproximado
        /// </summary>
        private decimal CalcularCET(decimal valorFinanciado, decimal parcelaTipica, int numeroParcelas)
        {
            // Calculo simplificado do CET usando a formula de TIR aproximada
            // CET real deveria incluir seguros, tarifas, IOF, etc.
            var totalPago = parcelaTipica * numeroParcelas;
            var jurosTotal = totalPago - valorFinanciado;
            var taxaEfetivaPeriodo = jurosTotal / valorFinanciado;
            var taxaAnual = taxaEfetivaPeriodo / (numeroParcelas / 12m) * 100;

            return Math.Round(taxaAnual, 2);
        }
    }
}
