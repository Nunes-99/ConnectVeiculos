using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Application.ViewModels.Common;
using ConnectVeiculos.Application.ViewModels.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class BuscaAvancadaVeiculosUseCase : IBuscaAvancadaVeiculosUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;

        public BuscaAvancadaVeiculosUseCase(IVeiculoRepository veiculoRepository)
        {
            _veiculoRepository = veiculoRepository;
        }

        public async Task<PagedResult<VeiculoViewModel>> Execute(BuscaAvancadaVeiculoInputModel input)
        {
            var parametros = new BuscaAvancadaParams
            {
                Texto = input.Texto,
                Marca = input.Marca,
                Modelo = input.Modelo,
                AnoMinimo = input.AnoMinimo,
                AnoMaximo = input.AnoMaximo,
                PrecoMinimo = input.PrecoMinimo,
                PrecoMaximo = input.PrecoMaximo,
                KmMaximo = input.KmMaximo,
                Cor = input.Cor,
                LojaId = input.LojaId,
                CategoriaId = input.CategoriaId,
                Status = input.Status,
                Situacao = input.Situacao,
                CaracteristicasIds = input.CaracteristicasIds,
                OrdenarPor = input.OrdenarPor,
                Direcao = input.Direcao ?? "desc",
                Pagina = input.Pagina,
                TamanhoPagina = input.TamanhoPagina
            };

            var (items, total) = await _veiculoRepository.BuscaAvancadaAsync(parametros);

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
                VeiDtPostagemFace = v.VeiDtPostagemFace,
                Caracteristicas = v.Caracteristicas?.Select(c => new CaracteristicaVeiculoViewModel
                {
                    CarId = c.Caracteristica?.CarId ?? 0,
                    CarNome = c.Caracteristica?.CarNome ?? ""
                }).ToList() ?? new List<CaracteristicaVeiculoViewModel>(),
                Imagens = v.Imagens?.Select(i => new ImagemVeiculoViewModel
                {
                    ImgId = i.ImgId,
                    ImgCaminho = i.ImgCaminho,
                    ImgOrdem = i.ImgOrdem
                }).OrderBy(i => i.ImgOrdem).ToList() ?? new List<ImagemVeiculoViewModel>()
            }).ToList();

            return new PagedResult<VeiculoViewModel>(viewModels, total, input.Pagina, input.TamanhoPagina);
        }
    }
}
