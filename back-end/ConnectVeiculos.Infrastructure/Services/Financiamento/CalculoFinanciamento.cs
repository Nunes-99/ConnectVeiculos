using ConnectVeiculos.Core.Financiamentos;

namespace ConnectVeiculos.Infrastructure.Services.Financiamento
{
    /// <summary>
    /// Conta do financiamento de veiculo para pessoa fisica, como os bancos
    /// fazem: tabela Price, IOF financiado (0,38% + 0,0082% ao dia sobre cada
    /// amortizacao, ate 365 dias) e tarifas. Da uma estimativa proxima da do
    /// banco; o valor final sai na aprovacao.
    /// </summary>
    public static class CalculoFinanciamento
    {
        public const decimal IofAdicional = 0.0038m;
        public const decimal IofDiarioPf = 0.000082m;

        /// <summary>Parcela fixa (Price) de um valor a uma taxa mensal decimal.</summary>
        public static decimal Parcela(decimal valor, decimal taxaMensal, int meses)
        {
            if (meses <= 0) throw new ArgumentOutOfRangeException(nameof(meses));
            if (taxaMensal == 0) return valor / meses;
            var fator = (decimal)Math.Pow((double)(1 + taxaMensal), meses);
            return valor * taxaMensal * fator / (fator - 1);
        }

        /// <summary>IOF de um contrato Price de "valor" (parcelas mensais, 30 dias cada).</summary>
        public static decimal Iof(decimal valor, decimal taxaMensal, int meses)
        {
            var parcela = Parcela(valor, taxaMensal, meses);
            var saldo = valor;
            var diario = 0m;
            for (var k = 1; k <= meses; k++)
            {
                var amortizacao = parcela - saldo * taxaMensal;
                saldo -= amortizacao;
                diario += amortizacao * IofDiarioPf * Math.Min(30 * k, 365);
            }
            return valor * IofAdicional + diario;
        }

        /// <param name="valorLiquido">O que o banco paga pelo carro (valor - entrada).</param>
        /// <param name="taxaMensalPct">Taxa da tabela, em % ao mes (ex.: 1,99).</param>
        public static EstimativaPrazo Estimar(decimal valorLiquido, decimal taxaMensalPct, int meses,
            decimal tarifas, bool tarifasFinanciadas)
        {
            var i = taxaMensalPct / 100m;
            var semIof = valorLiquido + (tarifasFinanciadas ? tarifas : 0);

            // O IOF entra no valor financiado e tambem paga IOF: converge em poucas voltas.
            var iof = 0m;
            for (var volta = 0; volta < 20; volta++)
            {
                var novo = Iof(semIof + iof, i, meses);
                var fim = Math.Abs(novo - iof) < 0.001m;
                iof = novo;
                if (fim) break;
            }

            var financiado = semIof + iof;
            // O cliente paga a parcela em centavos: o total parte dela arredondada.
            var parcela = Math.Round(Parcela(financiado, i, meses), 2);
            var aVista = tarifasFinanciadas ? 0 : tarifas;
            var cetMensal = TaxaInterna(valorLiquido - aVista, parcela, meses);

            return new EstimativaPrazo
            {
                Meses = meses,
                TaxaMensal = taxaMensalPct,
                ValorParcela = Math.Round(parcela, 2),
                ValorFinanciado = Math.Round(financiado, 2),
                Iof = Math.Round(iof, 2),
                Tarifas = Math.Round(tarifas, 2),
                TotalPago = Math.Round(parcela * meses + aVista, 2),
                CetMensal = Math.Round(cetMensal * 100, 2),
                CetAnual = Math.Round(((decimal)Math.Pow((double)(1 + cetMensal), 12) - 1) * 100, 2)
            };
        }

        /// <summary>Taxa mensal que iguala o valor recebido as parcelas (base do CET).</summary>
        public static decimal TaxaInterna(decimal recebido, decimal parcela, int meses)
        {
            if (recebido <= 0 || parcela * meses <= recebido) return 0;
            decimal baixo = 0, alto = 1;
            for (var n = 0; n < 100; n++)
            {
                var meio = (baixo + alto) / 2;
                if (Parcela(recebido, meio, meses) > parcela) alto = meio;
                else baixo = meio;
            }
            return (baixo + alto) / 2;
        }

        /// <summary>
        /// Escolhe a faixa da tabela para o ano e a entrada. Sem faixa, devolve
        /// o motivo para a tela (ano fora da tabela ou entrada abaixo do minimo).
        /// </summary>
        public static (FaixaTaxaBanco? Faixa, string? Motivo) EscolherFaixa(IEnumerable<FaixaTaxaBanco> faixas,
            short ano, decimal valorVeiculo, decimal entrada)
        {
            var doAno = faixas
                .Where(f => (f.AnoDe == null || ano >= f.AnoDe) && (f.AnoAte == null || ano <= f.AnoAte))
                .Where(f => f.Taxas.Count > 0)
                .ToList();
            if (doAno.Count == 0) return (null, $"Sem taxa na tabela para veículo {ano}.");

            var pct = valorVeiculo > 0 ? entrada / valorVeiculo * 100 : 0;
            var faixa = doAno
                .Where(f => pct + 0.0001m >= f.EntradaMinimaPct)
                .OrderByDescending(f => f.EntradaMinimaPct)
                .FirstOrDefault();
            if (faixa != null) return (faixa, null);

            var minimo = doAno.Min(f => f.EntradaMinimaPct);
            var br = new System.Globalization.CultureInfo("pt-BR");
            return (null, string.Format(br, "Entrada mínima de {0:0.##}% ({1:C}).", minimo, valorVeiculo * minimo / 100));
        }
    }
}
