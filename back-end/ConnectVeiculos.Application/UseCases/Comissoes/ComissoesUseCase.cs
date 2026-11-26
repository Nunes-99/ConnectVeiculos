using System.Text.Json;
using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Despesas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;

namespace ConnectVeiculos.Application.UseCases.Comissoes
{
    public interface IComissoesUseCase
    {
        Task<RegrasComissao> RegrasAsync();
        Task<RegrasComissao> SalvarRegrasAsync(RegrasComissao regras);

        /// <summary>Comissao pela regra do vendedor para uma venda que esta sendo registrada; nulo sem regras.</summary>
        Task<ComissaoSugerida?> SugestaoAsync(int vendedorId, int veiculoId, decimal valorVenda);

        /// <summary>Vendas ativas do periodo com a comissao de cada uma; vendedorId filtra.</summary>
        Task<ComissaoExtrato> ExtratoAsync(DateTime inicio, DateTime fim, int? vendedorId);

        /// <summary>Marca as comissoes como pagas na data (ou volta para "a pagar" com nulo).</summary>
        Task<int> MarcarPagasAsync(IReadOnlyCollection<int> vendaIds, DateTime? pagaEm);
    }

    public class ComissaoExtrato
    {
        public DateTime Inicio { get; set; }
        public DateTime Fim { get; set; }
        public decimal TotalVendido { get; set; }
        public decimal TotalComissao { get; set; }
        public decimal TotalPago { get; set; }
        public decimal TotalAPagar { get; set; }
        public List<ComissaoVendedor> Vendedores { get; set; } = new();
        public List<ComissaoItem> Vendas { get; set; } = new();
    }

    public class ComissaoVendedor
    {
        public int VendedorId { get; set; }
        public string Nome { get; set; } = "";
        public int Vendas { get; set; }
        public decimal Vendido { get; set; }
        public decimal Comissao { get; set; }
        public decimal Pago { get; set; }
        public decimal APagar { get; set; }
    }

    public class ComissaoItem
    {
        public int VenId { get; set; }
        public DateTime Data { get; set; }
        public int VendedorId { get; set; }
        public string Vendedor { get; set; } = "";
        public string Veiculo { get; set; } = "";
        public string? Comprador { get; set; }
        public decimal Valor { get; set; }
        public decimal Percentual { get; set; }
        public decimal Comissao { get; set; }
        public DateTime? PagaEm { get; set; }
    }

    /// <inheritdoc cref="IComissoesUseCase"/>
    public class ComissoesUseCase : IComissoesUseCase
    {
        public const string ChaveRegras = "COMISSAO_REGRAS";
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly IVendaRepository _vendas;
        private readonly IVeiculoRepository _veiculos;
        private readonly IVeiculoDespesaRepository _despesas;
        private readonly IUsuarioRepository _usuarios;
        private readonly IConfiguracaoSistemaRepository _config;

        public ComissoesUseCase(IVendaRepository vendas, IVeiculoRepository veiculos, IVeiculoDespesaRepository despesas,
            IUsuarioRepository usuarios, IConfiguracaoSistemaRepository config)
        {
            _vendas = vendas;
            _veiculos = veiculos;
            _despesas = despesas;
            _usuarios = usuarios;
            _config = config;
        }

        public async Task<RegrasComissao> RegrasAsync()
        {
            var json = await _config.GetValorAsync(ChaveRegras);
            if (string.IsNullOrWhiteSpace(json)) return new RegrasComissao();
            try { return JsonSerializer.Deserialize<RegrasComissao>(json, Json) ?? new RegrasComissao(); }
            catch (JsonException) { return new RegrasComissao(); }
        }

        public async Task<RegrasComissao> SalvarRegrasAsync(RegrasComissao regras)
        {
            regras.Padrao ??= new RegraComissao();
            regras.Vendedores ??= new();
            ComissaoCalculo.Validar(regras.Padrao, "padrão da loja");
            var usuarios = (await _usuarios.GetAllAsync()).ToDictionary(u => u.UsuId, u => u.UsuNome);
            foreach (var (id, r) in regras.Vendedores)
            {
                if (!usuarios.TryGetValue(id, out var nome)) throw new InputModelException($"Usuário {id} não existe.");
                ComissaoCalculo.Validar(r, nome);
            }
            await _config.SetValorAsync(ChaveRegras, JsonSerializer.Serialize(regras, Json));
            return await RegrasAsync();
        }

        public async Task<ComissaoSugerida?> SugestaoAsync(int vendedorId, int veiculoId, decimal valorVenda)
        {
            // Loja que nunca cadastrou regra: a tela de venda fica como era (% digitado).
            if (string.IsNullOrWhiteSpace(await _config.GetValorAsync(ChaveRegras))) return null;
            var regras = await RegrasAsync();
            var regra = regras.Para(vendedorId);
            var custo = 0m;
            if (regra.Tipo == TipoComissao.PercentualLucro)
            {
                var veiculo = await _veiculos.GetByIdAsync(veiculoId);
                custo = (veiculo?.VeiPrecoCompra ?? 0) + (await _despesas.GetByVeiculoIdAsync(veiculoId)).Sum(d => d.DesValor);
            }
            var s = ComissaoCalculo.Calcular(regra, valorVenda, custo);
            s.RegraDoVendedor = regras.Vendedores.ContainsKey(vendedorId);
            return s;
        }

        public async Task<ComissaoExtrato> ExtratoAsync(DateTime inicio, DateTime fim, int? vendedorId)
        {
            inicio = inicio.Date;
            var ate = fim.Date.AddDays(1);
            var usuarios = (await _usuarios.GetAllAsync()).ToDictionary(u => u.UsuId, u => u.UsuNome);
            var vendas = (await _vendas.GetAllAsync())
                .Where(v => v.VenStatus != "E" && v.VenDtVenda >= inicio && v.VenDtVenda < ate)
                .Where(v => vendedorId == null || v.R_UsuId == vendedorId)
                .OrderByDescending(v => v.VenDtVenda)
                .ToList();

            var itens = vendas.Select(v => new ComissaoItem
            {
                VenId = v.VenId,
                Data = v.VenDtVenda,
                VendedorId = v.R_UsuId,
                Vendedor = usuarios.GetValueOrDefault(v.R_UsuId) ?? "Usuário removido",
                Veiculo = $"{(v.VenMarca ?? "").Split(" - ").Last()} {v.VenModelo} {v.VenAno}".Trim(),
                Comprador = v.VenCompradorNome,
                Valor = v.VenValor,
                Percentual = v.VenComissaoPorc,
                Comissao = v.VenComissaoValor,
                PagaEm = v.VenComissaoPagaEm
            }).ToList();

            return new ComissaoExtrato
            {
                Inicio = inicio,
                Fim = fim.Date,
                TotalVendido = itens.Sum(i => i.Valor),
                TotalComissao = itens.Sum(i => i.Comissao),
                TotalPago = itens.Where(i => i.PagaEm != null).Sum(i => i.Comissao),
                TotalAPagar = itens.Where(i => i.PagaEm == null).Sum(i => i.Comissao),
                Vendas = itens,
                Vendedores = itens.GroupBy(i => i.VendedorId)
                    .Select(g => new ComissaoVendedor
                    {
                        VendedorId = g.Key,
                        Nome = g.First().Vendedor,
                        Vendas = g.Count(),
                        Vendido = g.Sum(i => i.Valor),
                        Comissao = g.Sum(i => i.Comissao),
                        Pago = g.Where(i => i.PagaEm != null).Sum(i => i.Comissao),
                        APagar = g.Where(i => i.PagaEm == null).Sum(i => i.Comissao)
                    })
                    .OrderByDescending(v => v.Comissao)
                    .ToList()
            };
        }

        public async Task<int> MarcarPagasAsync(IReadOnlyCollection<int> vendaIds, DateTime? pagaEm)
        {
            if (vendaIds.Count == 0) throw new InputModelException("Escolha ao menos uma venda.");
            var n = 0;
            foreach (var id in vendaIds.Distinct())
            {
                var venda = await _vendas.GetByIdAsync(id);
                if (venda == null || venda.VenStatus == "E") continue;
                venda.MarcarComissaoPaga(pagaEm?.Date);
                await _vendas.UpdateAsync(venda);
                n++;
            }
            return n;
        }
    }
}
