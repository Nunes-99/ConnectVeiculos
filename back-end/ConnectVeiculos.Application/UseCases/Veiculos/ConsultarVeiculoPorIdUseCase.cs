using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Application.ViewModels.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class ConsultarVeiculoPorIdUseCase : IConsultarVeiculoPorIdUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;

        public ConsultarVeiculoPorIdUseCase(IVeiculoRepository veiculoRepository)
        {
            _veiculoRepository = veiculoRepository;
        }

        public async Task<VeiculoViewModel> Execute(int id)
        {
            var veiculo = await _veiculoRepository.GetByIdAsync(id);

            if (veiculo == null)
                return null;

            return new VeiculoViewModel
            {
                VeiId = veiculo.VeiId,
                R_LojId = veiculo.R_LojId,
                LojaNome = veiculo.Loja?.LojNome ?? string.Empty,
                R_CatId = veiculo.R_CatId,
                CategoriaNome = veiculo.Categoria?.CatNome ?? string.Empty,
                VeiMarca = veiculo.VeiMarca,
                VeiModelo = veiculo.VeiModelo,
                VeiAno = veiculo.VeiAno,
                VeiPlaca = veiculo.VeiPlaca,
                VeiChassi = veiculo.VeiChassi,
                VeiCor = veiculo.VeiCor,
                VeiKm = veiculo.VeiKm,
                VeiPreco = veiculo.VeiPreco,
                VeiDtEntrada = veiculo.VeiDtEntrada,
                VeiSts = veiculo.VeiSts,
                VeiSitSts = veiculo.VeiSitSts,
                VeiPrecoCompra = veiculo.VeiPrecoCompra,
                VeiObservacao = veiculo.VeiObservacao,
                VeiOpcionais = veiculo.VeiOpcionais,
                VeiPrecoFipe = veiculo.VeiPrecoFipe,
                VeiPostadoInsta = veiculo.VeiPostadoInsta,
                VeiPostadoFace = veiculo.VeiPostadoFace,
                VeiDtPostagemInsta = veiculo.VeiDtPostagemInsta,
                VeiDtPostagemFace = veiculo.VeiDtPostagemFace,
                Caracteristicas = veiculo.Caracteristicas?.Select(c => new CaracteristicaVeiculoViewModel
                {
                    CarId = c.R_CarId,
                    CarNome = c.Caracteristica?.CarNome ?? string.Empty
                }).ToList() ?? new List<CaracteristicaVeiculoViewModel>(),
                Observacoes = veiculo.Observacoes?.Select(o => new ObservacaoVeiculoViewModel
                {
                    ObsId = o.R_ObsId,
                    ObsNome = o.Observacao?.ObsNome ?? string.Empty
                }).ToList() ?? new List<ObservacaoVeiculoViewModel>(),
                Imagens = veiculo.Imagens?.Select(i => new ImagemVeiculoViewModel
                {
                    ImgId = i.ImgId,
                    ImgCaminho = i.ImgCaminho,
                    ImgOrdem = i.ImgOrdem
                }).ToList() ?? new List<ImagemVeiculoViewModel>()
            };
        }
    }
}
