namespace ConnectVeiculos.Application.UseCases.Estoque
{
    /// <summary>Regras da loja para o alerta e a sugestao (Configuracoes → Estoque parado).</summary>
    public class EstoqueParadoConfig
    {
        public const int DiasAtencaoPadrao = 45;
        public const int DiasCriticoPadrao = 90;
        public const decimal MargemMinimaPadrao = 5m;

        /// <summary>A partir de quantos dias o carro aparece como "atencao".</summary>
        public int DiasAtencao { get; set; } = DiasAtencaoPadrao;
        /// <summary>A partir de quantos dias o carro aparece como "critico".</summary>
        public int DiasCritico { get; set; } = DiasCriticoPadrao;
        /// <summary>Margem minima sobre o custo (compra + despesas), em %: a sugestao nunca fica abaixo.</summary>
        public decimal MargemMinima { get; set; } = MargemMinimaPadrao;
    }

    public enum FaixaEstoque { Normal, Atencao, Critico }

    public class SugestaoPrecoResultado
    {
        public FaixaEstoque Faixa { get; set; }
        /// <summary>Preco sugerido; igual ao atual quando nao ha o que mudar.</summary>
        public decimal PrecoSugerido { get; set; }
        public bool Mudar { get; set; }
        /// <summary>Menor preco que respeita a margem minima (0 sem custo cadastrado).</summary>
        public decimal PrecoMinimo { get; set; }
        /// <summary>Margem atual sobre o preco, em %; nula sem custo.</summary>
        public decimal? MargemAtual { get; set; }
        /// <summary>Preco atual comparado com a FIPE, em % (positivo = acima); nulo sem FIPE.</summary>
        public decimal? DiferencaFipe { get; set; }
        public string Motivo { get; set; } = "";
    }

    /// <summary>
    /// Sugestao de preco pela FIPE, pela margem e pelo tempo de estoque.
    /// - Ate o alerta: so corrige preco fora da curva (mais de 10% acima da
    ///   FIPE ou abaixo da margem minima).
    /// - Atencao: baixa 3%, sem passar da FIPE.
    /// - Critico: baixa 6%, sem passar de 3% abaixo da FIPE.
    /// Nunca abaixo do custo + margem minima. Arredonda em centenas.
    /// </summary>
    public static class SugestaoPreco
    {
        public const decimal LimiteAcimaFipe = 0.10m;

        public static FaixaEstoque Faixa(int dias, EstoqueParadoConfig c) =>
            dias >= c.DiasCritico ? FaixaEstoque.Critico : dias >= c.DiasAtencao ? FaixaEstoque.Atencao : FaixaEstoque.Normal;

        public static SugestaoPrecoResultado Calcular(decimal preco, decimal custo, decimal? fipe, int dias, EstoqueParadoConfig c)
        {
            var faixa = Faixa(dias, c);
            var minimo = custo > 0 ? Centena(custo * (1 + c.MargemMinima / 100m), paraCima: true) : 0m;
            var r = new SugestaoPrecoResultado
            {
                Faixa = faixa,
                PrecoSugerido = preco,
                PrecoMinimo = minimo,
                MargemAtual = custo > 0 && preco > 0 ? Math.Round((preco - custo) / preco * 100m, 1) : null,
                DiferencaFipe = fipe is > 0 && preco > 0 ? Math.Round((preco - fipe.Value) / fipe.Value * 100m, 1) : null
            };
            if (preco <= 0)
            {
                r.Motivo = "Cadastre o preço de venda.";
                return r;
            }

            decimal alvo;
            if (faixa == FaixaEstoque.Normal)
            {
                if (minimo > 0 && preco < minimo)
                    return Sugerir(r, preco, minimo, $"O preço está abaixo do custo + margem mínima de {c.MargemMinima:0.#}%.");
                if (fipe is > 0 && preco > fipe.Value * (1 + LimiteAcimaFipe))
                {
                    alvo = Math.Max(Centena(fipe.Value * 1.05m), minimo);
                    return Sugerir(r, preco, alvo, $"O preço está {r.DiferencaFipe:0.#}% acima da FIPE: deve demorar a vender.");
                }
                r.Motivo = $"Há {dias} dia(s) em estoque. Preço dentro do esperado.";
                return r;
            }

            var critico = faixa == FaixaEstoque.Critico;
            alvo = preco * (critico ? 0.94m : 0.97m);
            if (fipe is > 0) alvo = Math.Min(alvo, fipe.Value * (critico ? 0.97m : 1m));
            alvo = Centena(alvo);
            if (minimo > 0 && alvo < minimo) alvo = minimo;

            var motivo = $"Há {dias} dias em estoque ({(critico ? "crítico" : "atenção")}).";
            if (alvo >= preco)
            {
                r.Motivo = motivo + (minimo > 0 && minimo >= preco
                    ? $" O preço já está no limite da margem mínima de {c.MargemMinima:0.#}%: melhore fotos e anúncios ou reveja a margem."
                    : " O preço já está abaixo da referência: melhore fotos e anúncios.");
                return r;
            }
            return Sugerir(r, preco, alvo, motivo + (critico
                ? " Baixar o preço libera o dinheiro parado."
                : " Uma pequena redução ajuda a girar."));
        }

        private static SugestaoPrecoResultado Sugerir(SugestaoPrecoResultado r, decimal preco, decimal alvo, string motivo)
        {
            r.PrecoSugerido = alvo;
            r.Mudar = alvo != preco;
            r.Motivo = motivo;
            return r;
        }

        private static decimal Centena(decimal v, bool paraCima = false) =>
            (paraCima ? Math.Ceiling(v / 100m) : Math.Floor(v / 100m)) * 100m;
    }
}
