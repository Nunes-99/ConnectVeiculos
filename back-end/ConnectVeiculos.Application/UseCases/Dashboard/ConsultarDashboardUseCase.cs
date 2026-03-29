using ConnectVeiculos.Application.Interfaces.Dashboard;
using ConnectVeiculos.Application.ViewModels.Dashboard;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;

namespace ConnectVeiculos.Application.UseCases.Dashboard
{
    public class ConsultarDashboardUseCase : IConsultarDashboardUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IUsuarioRepository _usuarioRepository;

        public ConsultarDashboardUseCase(
            IVeiculoRepository veiculoRepository,
            ILojaRepository lojaRepository,
            ICategoriaRepository categoriaRepository,
            IUsuarioRepository usuarioRepository)
        {
            _veiculoRepository = veiculoRepository;
            _lojaRepository = lojaRepository;
            _categoriaRepository = categoriaRepository;
            _usuarioRepository = usuarioRepository;
        }

        public async Task<DashboardViewModel> Execute()
        {
            var veiculos = await _veiculoRepository.GetAllAsync();
            var lojas = await _lojaRepository.GetAllAsync();
            var categorias = await _categoriaRepository.GetAllAsync();
            var usuarios = await _usuarioRepository.GetAllAsync();

            var veiculosDisponiveis = veiculos.Where(v => v.VeiSts == "D").ToList();
            var veiculosVendidos = veiculos.Where(v => v.VeiSts == "V").ToList();
            var veiculosReservados = veiculos.Where(v => v.VeiSts == "R").ToList();

            var dashboard = new DashboardViewModel
            {
                TotalVeiculos = veiculos.Count(),
                VeiculosDisponiveis = veiculosDisponiveis.Count,
                VeiculosVendidos = veiculosVendidos.Count,
                VeiculosReservados = veiculosReservados.Count,
                ValorTotalEstoque = veiculosDisponiveis.Sum(v => v.VeiPreco),
                ValorMedioVeiculo = veiculosDisponiveis.Any() ? veiculosDisponiveis.Average(v => v.VeiPreco) : 0,
                TotalLojas = lojas.Count(),
                TotalCategorias = categorias.Count(),
                TotalUsuarios = usuarios.Count(),

                VeiculosPorCategoria = veiculosDisponiveis
                    .GroupBy(v => v.R_CatId)
                    .Select(g =>
                    {
                        var categoria = categorias.FirstOrDefault(c => c.CatId == g.Key);
                        return new VeiculoPorCategoriaViewModel
                        {
                            Categoria = categoria?.CatNome ?? "Sem Categoria",
                            Quantidade = g.Count()
                        };
                    })
                    .OrderByDescending(x => x.Quantidade)
                    .ToList(),

                VeiculosPorLoja = veiculosDisponiveis
                    .GroupBy(v => v.R_LojId)
                    .Select(g =>
                    {
                        var loja = lojas.FirstOrDefault(l => l.LojId == g.Key);
                        return new VeiculoPorLojaViewModel
                        {
                            Loja = loja?.LojNome ?? "Sem Loja",
                            Quantidade = g.Count(),
                            ValorTotal = g.Sum(v => v.VeiPreco)
                        };
                    })
                    .OrderByDescending(x => x.Quantidade)
                    .ToList(),

                VeiculosRecentes = veiculos
                    .OrderByDescending(v => v.VeiId)
                    .Take(5)
                    .Select(v => new VeiculoRecenteViewModel
                    {
                        VeiId = v.VeiId,
                        Marca = v.VeiMarca,
                        Modelo = v.VeiModelo,
                        Ano = v.VeiAno,
                        Preco = v.VeiPreco,
                        Status = v.VeiSts
                    })
                    .ToList()
            };

            return dashboard;
        }
    }
}
