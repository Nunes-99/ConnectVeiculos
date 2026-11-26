using System.Globalization;
using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.Interfaces.Estoque;
using ConnectVeiculos.Application.ViewModels.Estoque;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Despesas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;

namespace ConnectVeiculos.Application.UseCases.Estoque
{
    /// <inheritdoc cref="IEstoqueParadoUseCase"/>
    public class EstoqueParadoUseCase : IEstoqueParadoUseCase
    {
        public const string ChaveDiasAtencao = "ESTOQUE_DIAS_ATENCAO";
        public const string ChaveDiasCritico = "ESTOQUE_DIAS_CRITICO";
        public const string ChaveMargemMinima = "ESTOQUE_MARGEM_MINIMA";

        private readonly IVeiculoRepository _veiculos;
        private readonly IVeiculoDespesaRepository _despesas;
        private readonly ILojaRepository _lojas;
        private readonly IConfiguracaoSistemaRepository _config;

        /// <summary>Para os testes: "hoje" fixo.</summary>
        public Func<DateTime> Hoje { get; set; } = () => DateTime.Today;

        public EstoqueParadoUseCase(IVeiculoRepository veiculos, IVeiculoDespesaRepository despesas,
            ILojaRepository lojas, IConfiguracaoSistemaRepository config)
        {
            _veiculos = veiculos;
            _despesas = despesas;
            _lojas = lojas;
            _config = config;
        }

        public async Task<EstoqueParadoViewModel> ConsultarAsync(int? lojaId = null)
        {
            var config = await ConfigAsync();
            var disponiveis = (await _veiculos.GetAllAsync())
                .Where(v => v.VeiSts == "D" && (lojaId == null || v.R_LojId == lojaId))
                .ToList();
            var despesas = (await _despesas.GetAllAsync())
                .GroupBy(d => d.R_VeiId).ToDictionary(g => g.Key, g => g.Sum(d => d.DesValor));
            var lojas = (await _lojas.GetAllAsync()).ToDictionary(l => l.LojId, l => l.LojNome);

            var itens = disponiveis
                .Select(v => Item(v, despesas.GetValueOrDefault(v.VeiId), lojas.GetValueOrDefault(v.R_LojId), config))
                .ToList();
            var parados = itens.Where(i => i.Faixa != "normal").OrderByDescending(i => i.Dias).ToList();
            return new EstoqueParadoViewModel
            {
                Config = config,
                Disponiveis = itens.Count,
                Atencao = itens.Count(i => i.Faixa == "atencao"),
                Critico = itens.Count(i => i.Faixa == "critico"),
                CustoParado = parados.Sum(i => i.Custo),
                MediaDias = itens.Count > 0 ? (int)Math.Round(itens.Average(i => i.Dias)) : 0,
                Veiculos = parados
            };
        }

        public async Task<EstoqueParadoItemViewModel?> SugestaoAsync(int veiculoId)
        {
            var v = await _veiculos.GetByIdAsync(veiculoId);
            if (v == null) return null;
            var despesas = (await _despesas.GetByVeiculoIdAsync(veiculoId)).Sum(d => d.DesValor);
            var loja = await _lojas.GetByIdAsync(v.R_LojId);
            return Item(v, despesas, loja?.LojNome, await ConfigAsync());
        }

        public async Task<EstoqueParadoConfig> ConfigAsync() => new()
        {
            DiasAtencao = Inteiro(await _config.GetValorAsync(ChaveDiasAtencao)) ?? EstoqueParadoConfig.DiasAtencaoPadrao,
            DiasCritico = Inteiro(await _config.GetValorAsync(ChaveDiasCritico)) ?? EstoqueParadoConfig.DiasCriticoPadrao,
            MargemMinima = Numero(await _config.GetValorAsync(ChaveMargemMinima)) ?? EstoqueParadoConfig.MargemMinimaPadrao
        };

        public async Task<EstoqueParadoConfig> SalvarConfigAsync(EstoqueParadoConfig c)
        {
            if (c.DiasAtencao < 1 || c.DiasAtencao > 365)
                throw new InputModelException("O alerta de atenção vai de 1 a 365 dias.");
            if (c.DiasCritico <= c.DiasAtencao || c.DiasCritico > 730)
                throw new InputModelException("O alerta crítico tem de ser maior que o de atenção (até 730 dias).");
            if (c.MargemMinima < 0 || c.MargemMinima > 100)
                throw new InputModelException("A margem mínima vai de 0% a 100%.");
            await _config.SetValorAsync(ChaveDiasAtencao, c.DiasAtencao.ToString(CultureInfo.InvariantCulture));
            await _config.SetValorAsync(ChaveDiasCritico, c.DiasCritico.ToString(CultureInfo.InvariantCulture));
            await _config.SetValorAsync(ChaveMargemMinima, c.MargemMinima.ToString(CultureInfo.InvariantCulture));
            return await ConfigAsync();
        }

        private EstoqueParadoItemViewModel Item(Veiculo v, decimal despesas, string? loja, EstoqueParadoConfig config)
        {
            // Sem data de entrada chega 0001-01-01: contaria ~740 mil dias. Fica 0 (sem alerta).
            var dias = v.VeiDtEntrada.Year < 1900 ? 0 : Math.Max(0, (Hoje().Date - v.VeiDtEntrada.Date).Days);
            // Sem preco de compra nao ha custo: a sugestao fica so pela FIPE e pelo tempo.
            var custo = v.VeiPrecoCompra > 0 ? v.VeiPrecoCompra + despesas : 0m;
            var s = SugestaoPreco.Calcular(v.VeiPreco, custo, v.VeiPrecoFipe, dias, config);
            return new EstoqueParadoItemViewModel
            {
                VeiId = v.VeiId,
                Descricao = $"{(v.VeiMarca ?? "").Split(" - ").Last()} {v.VeiModelo} {v.VeiAno}".Trim(),
                Placa = v.VeiPlaca,
                Loja = loja,
                Dias = dias,
                Entrada = v.VeiDtEntrada,
                Preco = v.VeiPreco,
                Fipe = v.VeiPrecoFipe,
                Custo = custo,
                Despesas = despesas,
                Faixa = s.Faixa switch { FaixaEstoque.Critico => "critico", FaixaEstoque.Atencao => "atencao", _ => "normal" },
                PrecoSugerido = s.PrecoSugerido,
                Mudar = s.Mudar,
                PrecoMinimo = s.PrecoMinimo,
                MargemAtual = s.MargemAtual,
                DiferencaFipe = s.DiferencaFipe,
                Motivo = s.Motivo
            };
        }

        private static int? Inteiro(string? v) =>
            int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

        private static decimal? Numero(string? v) =>
            decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
}
