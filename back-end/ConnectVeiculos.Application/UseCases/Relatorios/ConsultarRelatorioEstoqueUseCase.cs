using ConnectVeiculos.Application.Interfaces.Relatorios;
using ConnectVeiculos.Application.ViewModels.Relatorios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;

namespace ConnectVeiculos.Application.UseCases.Relatorios
{
    public class ConsultarRelatorioEstoqueUseCase : IConsultarRelatorioEstoqueUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly ICategoriaRepository _categoriaRepository;

        public ConsultarRelatorioEstoqueUseCase(
            IVeiculoRepository veiculoRepository,
            ILojaRepository lojaRepository,
            ICategoriaRepository categoriaRepository)
        {
            _veiculoRepository = veiculoRepository;
            _lojaRepository = lojaRepository;
            _categoriaRepository = categoriaRepository;
        }

        public async Task<RelatorioEstoqueViewModel> Execute(int? lojaId = null, int? categoriaId = null)
        {
            var todosVeiculos = await _veiculoRepository.GetAllAsync();
            var veiculos = todosVeiculos.ToList();

            // Aplicar filtros
            if (lojaId.HasValue)
                veiculos = veiculos.Where(v => v.R_LojId == lojaId.Value).ToList();

            if (categoriaId.HasValue)
                veiculos = veiculos.Where(v => v.R_CatId == categoriaId.Value).ToList();

            var lojas = (await _lojaRepository.GetAllAsync()).ToList();
            var categorias = (await _categoriaRepository.GetAllAsync()).ToList();

            var veiculosDisponiveis = veiculos.Where(v => v.VeiSts == "D").ToList();
            var valorTotalEstoque = veiculosDisponiveis.Sum(v => v.VeiPreco);

            var resultado = new RelatorioEstoqueViewModel
            {
                TotalVeiculos = veiculos.Count,
                VeiculosDisponiveis = veiculosDisponiveis.Count,
                VeiculosVendidos = veiculos.Count(v => v.VeiSts == "V"),
                VeiculosReservados = veiculos.Count(v => v.VeiSts == "R"),
                ValorTotalEstoque = valorTotalEstoque,
                ValorMedioVeiculo = veiculosDisponiveis.Count > 0 ? valorTotalEstoque / veiculosDisponiveis.Count : 0,

                // Estoque por loja
                EstoquePorLoja = veiculosDisponiveis
                    .GroupBy(v => v.R_LojId)
                    .Select(g =>
                    {
                        var loja = lojas.FirstOrDefault(l => l.LojId == g.Key);
                        return new EstoquePorLojaViewModel
                        {
                            LojaId = g.Key,
                            LojaNome = loja?.LojNome ?? "Desconhecida",
                            Quantidade = g.Count(),
                            ValorTotal = g.Sum(v => v.VeiPreco)
                        };
                    })
                    .OrderByDescending(e => e.ValorTotal)
                    .ToList(),

                // Estoque por categoria
                EstoquePorCategoria = veiculosDisponiveis
                    .GroupBy(v => v.R_CatId)
                    .Select(g =>
                    {
                        var categoria = categorias.FirstOrDefault(c => c.CatId == g.Key);
                        return new EstoquePorCategoriaViewModel
                        {
                            CategoriaId = g.Key,
                            CategoriaNome = categoria?.CatNome ?? "Desconhecida",
                            Quantidade = g.Count(),
                            ValorTotal = g.Sum(v => v.VeiPreco)
                        };
                    })
                    .OrderByDescending(e => e.ValorTotal)
                    .ToList(),

                // Estoque por marca
                EstoquePorMarca = veiculosDisponiveis
                    .GroupBy(v => v.VeiMarca ?? "Desconhecida")
                    .Select(g => new EstoquePorMarcaViewModel
                    {
                        Marca = g.Key,
                        Quantidade = g.Count(),
                        ValorTotal = g.Sum(v => v.VeiPreco)
                    })
                    .OrderByDescending(e => e.Quantidade)
                    .Take(10)
                    .ToList()
            };

            return resultado;
        }
    }
}
