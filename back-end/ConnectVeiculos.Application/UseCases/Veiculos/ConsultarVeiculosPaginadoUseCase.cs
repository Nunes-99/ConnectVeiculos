using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class ConsultarVeiculosPaginadoUseCase : IConsultarVeiculosPaginadoUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;

        public ConsultarVeiculosPaginadoUseCase(IVeiculoRepository veiculoRepository)
        {
            _veiculoRepository = veiculoRepository;
        }

        public async Task<PagedResult<VeiculoViewModel>> Execute(int page, int pageSize, string? search = null, int? lojaId = null)
        {
            var (items, total) = await _veiculoRepository.GetPagedAsync(page, pageSize, search, lojaId);

            var viewModels = items.Select(v => new VeiculoViewModel
            {
                VeiId = v.VeiId,
                R_LojId = v.R_LojId,
                LojaNome = v.Loja?.LojNome ?? "",
                R_CatId = v.R_CatId,
                CategoriaNome = v.Categoria?.CatNome ?? "",
                VeiMarca = v.VeiMarca,
                VeiModelo = v.VeiModelo,
                VeiAno = v.VeiAno,
                VeiPlaca = v.VeiPlaca,
                VeiChassi = v.VeiChassi,
                VeiCor = v.VeiCor,
                VeiKm = v.VeiKm,
                VeiPreco = v.VeiPreco,
                VeiDtEntrada = v.VeiDtEntrada,
                VeiSts = v.VeiSts,
                VeiSitSts = v.VeiSitSts,
                VeiPrecoCompra = v.VeiPrecoCompra,
                VeiObservacao = v.VeiObservacao,
                VeiPostadoInsta = v.VeiPostadoInsta,
                VeiPostadoFace = v.VeiPostadoFace,
                VeiDtPostagemInsta = v.VeiDtPostagemInsta,
                VeiDtPostagemFace = v.VeiDtPostagemFace
            }).ToList();

            return new PagedResult<VeiculoViewModel>(viewModels, total, page, pageSize);
        }
    }
}
