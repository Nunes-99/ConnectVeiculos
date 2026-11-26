namespace ConnectVeiculos.Application.UseCases.Comissoes
{
    /// <summary>Como a comissao e' calculada.</summary>
    public static class TipoComissao
    {
        /// <summary>% sobre o valor da venda.</summary>
        public const string PercentualVenda = "PERCENTUAL_VENDA";
        /// <summary>% sobre o lucro (venda - compra - despesas do carro).</summary>
        public const string PercentualLucro = "PERCENTUAL_LUCRO";
        /// <summary>Valor fixo por carro vendido.</summary>
        public const string ValorFixo = "VALOR_FIXO";

        public static readonly string[] Todos = { PercentualVenda, PercentualLucro, ValorFixo };
    }

    public class RegraComissao
    {
        public string Tipo { get; set; } = TipoComissao.PercentualVenda;
        /// <summary>% (tipos percentuais) ou R$ (valor fixo).</summary>
        public decimal Valor { get; set; }
    }

    /// <summary>Regra padrao da loja e excecoes por vendedor (id do usuario).</summary>
    public class RegrasComissao
    {
        public RegraComissao Padrao { get; set; } = new();
        public Dictionary<int, RegraComissao> Vendedores { get; set; } = new();

        public RegraComissao Para(int vendedorId) =>
            Vendedores.TryGetValue(vendedorId, out var r) ? r : Padrao;
    }

    public class ComissaoSugerida
    {
        public decimal Percentual { get; set; }
        public decimal Valor { get; set; }
        public string Tipo { get; set; } = TipoComissao.PercentualVenda;
        /// <summary>"3% da venda", "10% do lucro (R$ 5.000)", "R$ 500 por carro".</summary>
        public string Regra { get; set; } = "";
        /// <summary>A regra e' do vendedor (true) ou a padrao da loja.</summary>
        public bool RegraDoVendedor { get; set; }
    }

    public static class ComissaoCalculo
    {
        /// <summary>
        /// Comissao de uma venda pela regra. Lucro negativo nao gera comissao
        /// negativa. O percentual devolvido e' sempre sobre o valor da venda
        /// (e' o que a venda guarda).
        /// </summary>
        public static ComissaoSugerida Calcular(RegraComissao regra, decimal valorVenda, decimal custo)
        {
            var valor = regra.Tipo switch
            {
                TipoComissao.ValorFixo => regra.Valor,
                TipoComissao.PercentualLucro => Math.Max(0, valorVenda - custo) * regra.Valor / 100m,
                _ => valorVenda * regra.Valor / 100m
            };
            valor = Math.Round(Math.Max(0, valor), 2, MidpointRounding.AwayFromZero);
            if (valorVenda > 0 && valor > valorVenda) valor = valorVenda;
            var pt = new System.Globalization.CultureInfo("pt-BR");
            return new ComissaoSugerida
            {
                Tipo = regra.Tipo,
                Valor = valor,
                Percentual = valorVenda > 0 ? Math.Round(valor / valorVenda * 100m, 2) : 0,
                Regra = regra.Tipo switch
                {
                    TipoComissao.ValorFixo => $"{regra.Valor.ToString("C0", pt)} por carro",
                    TipoComissao.PercentualLucro => $"{regra.Valor:0.##}% do lucro ({Math.Max(0, valorVenda - custo).ToString("C0", pt)})",
                    _ => $"{regra.Valor:0.##}% da venda"
                }
            };
        }

        public static void Validar(RegraComissao r, string quem)
        {
            if (!TipoComissao.Todos.Contains(r.Tipo))
                throw new Exceptions.InputModelException($"Tipo de comissão inválido ({quem}).");
            if (r.Valor < 0) throw new Exceptions.InputModelException($"A comissão não pode ser negativa ({quem}).");
            if (r.Tipo != TipoComissao.ValorFixo && r.Valor > 100)
                throw new Exceptions.InputModelException($"A comissão vai de 0% a 100% ({quem}).");
        }
    }
}
