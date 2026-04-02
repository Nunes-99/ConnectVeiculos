using ConnectVeiculos.Application.Interfaces.Catalogo;
using ConnectVeiculos.Application.ViewModels.Catalogo;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;

namespace ConnectVeiculos.Application.UseCases.Catalogo
{
    public class ConsultarCatalogoUseCase : IConsultarCatalogoUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;

        public ConsultarCatalogoUseCase(
            IVeiculoRepository veiculoRepository,
            ILojaRepository lojaRepository,
            IVeiculoImagemRepository imagemRepository)
        {
            _veiculoRepository = veiculoRepository;
            _lojaRepository = lojaRepository;
            _imagemRepository = imagemRepository;
        }

        public async Task<CatalogoResultadoViewModel> Execute(string marca, int? anoMin, int? anoMax, decimal? precoMin, decimal? precoMax, int? lojaId = null)
        {
            var veiculos = await _veiculoRepository.GetAllAsync();
            var lojas = await _lojaRepository.GetAllAsync();

            // Filtrar apenas veiculos disponiveis (status D)
            var veiculosDisponiveis = veiculos.Where(v => v.VeiSts == "D").ToList();

            // Filtrar por loja se especificado
            if (lojaId.HasValue && lojaId.Value > 0)
            {
                veiculosDisponiveis = veiculosDisponiveis.Where(v => v.R_LojId == lojaId.Value).ToList();
            }

            // Aplicar filtros
            if (!string.IsNullOrEmpty(marca))
            {
                veiculosDisponiveis = veiculosDisponiveis
                    .Where(v => v.VeiMarca.Contains(marca, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (anoMin.HasValue)
            {
                veiculosDisponiveis = veiculosDisponiveis.Where(v => v.VeiAno >= anoMin.Value).ToList();
            }

            if (anoMax.HasValue)
            {
                veiculosDisponiveis = veiculosDisponiveis.Where(v => v.VeiAno <= anoMax.Value).ToList();
            }

            if (precoMin.HasValue)
            {
                veiculosDisponiveis = veiculosDisponiveis.Where(v => v.VeiPreco >= precoMin.Value).ToList();
            }

            if (precoMax.HasValue)
            {
                veiculosDisponiveis = veiculosDisponiveis.Where(v => v.VeiPreco <= precoMax.Value).ToList();
            }

            // Carregar imagens de todos os veiculos disponiveis
            var imagensTodas = await _imagemRepository.GetAllAsync();
            var imagensPorVeiculo = imagensTodas
                .GroupBy(i => i.R_VeiId)
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.ImgOrdem).Select(i => i.ImgCaminho).ToList());

            // Montar resultado
            var resultado = new CatalogoResultadoViewModel
            {
                Veiculos = veiculosDisponiveis.Select(v =>
                {
                    var loja = lojas.FirstOrDefault(l => l.LojId == v.R_LojId);
                    return new CatalogoVeiculoViewModel
                    {
                        VeiId = v.VeiId,
                        VeiMarca = v.VeiMarca,
                        VeiModelo = v.VeiModelo,
                        VeiAno = v.VeiAno,
                        VeiCor = v.VeiCor,
                        VeiKm = v.VeiKm,
                        VeiPreco = v.VeiPreco,
                        VeiPlaca = v.VeiPlaca,
                        VeiObservacao = v.VeiObservacao,
                        VeiOpcionais = v.VeiOpcionais,
                        CategoriaNome = v.Categoria?.CatNome ?? "",
                        LojaNome = loja?.LojNome ?? "",
                        LojaCidade = loja?.LojCidade ?? "",
                        LojaEstado = loja?.LojEstado ?? "",
                        LojaWhatsApp = loja?.LojWhatsApp ?? loja?.LojTel1 ?? "",
                        LojaLogo = loja?.LojImg ?? "",
                        Imagens = imagensPorVeiculo.ContainsKey(v.VeiId) ? imagensPorVeiculo[v.VeiId] : new List<string>()
                    };
                }).ToList(),
                Total = veiculosDisponiveis.Count
            };

            // Dados da loja (quando filtrado por loja)
            if (lojaId.HasValue && lojaId.Value > 0)
            {
                var lojaInfo = lojas.FirstOrDefault(l => l.LojId == lojaId.Value);
                if (lojaInfo != null)
                {
                    resultado.Loja = new CatalogoLojaViewModel
                    {
                        LojId = lojaInfo.LojId,
                        LojNome = lojaInfo.LojNome,
                        LojSlug = lojaInfo.LojSlug,
                        LojCidade = lojaInfo.LojCidade,
                        LojEstado = lojaInfo.LojEstado,
                        LojTel1 = lojaInfo.LojTel1,
                        LojWhatsApp = lojaInfo.LojWhatsApp,
                        LojEmail = lojaInfo.LojEmail,
                        LojImg = lojaInfo.LojImg,
                        LojEndereco = $"{lojaInfo.LojLogradouro}, {lojaInfo.LojNumero} - {lojaInfo.LojBairro}, {lojaInfo.LojCidade}/{lojaInfo.LojEstado}",
                        LojCorPrimaria = lojaInfo.LojCorPrimaria ?? "#1a237e",
                        LojCorSecundaria = lojaInfo.LojCorSecundaria ?? "#25d366",
                        LojInstagram = lojaInfo.LojInstagram,
                        LojFacebook = lojaInfo.LojFacebook
                    };
                }
            }

            // Lojas disponíveis para filtro
            var lojasComVeiculos = veiculos.Where(v => v.VeiSts == "D").Select(v => v.R_LojId).Distinct();
            resultado.Lojas = lojas
                .Where(l => l.LojSts && lojasComVeiculos.Contains(l.LojId))
                .Select(l => new CatalogoLojaResumoViewModel { LojId = l.LojId, LojNome = l.LojNome, LojSlug = l.LojSlug })
                .OrderBy(l => l.LojNome)
                .ToList();

            // Montar filtros disponiveis (baseado em veiculos da loja, se filtrado)
            var baseParaFiltros = lojaId.HasValue && lojaId.Value > 0
                ? veiculos.Where(v => v.VeiSts == "D" && v.R_LojId == lojaId.Value).ToList()
                : veiculos.Where(v => v.VeiSts == "D").ToList();

            resultado.Filtros = new CatalogoFiltroViewModel
            {
                Marcas = baseParaFiltros.Select(v => v.VeiMarca).Distinct().OrderBy(m => m).ToList(),
                AnoMin = baseParaFiltros.Any() ? baseParaFiltros.Min(v => v.VeiAno) : DateTime.Now.Year,
                AnoMax = baseParaFiltros.Any() ? baseParaFiltros.Max(v => v.VeiAno) : DateTime.Now.Year,
                PrecoMin = baseParaFiltros.Any() ? baseParaFiltros.Min(v => v.VeiPreco) : 0,
                PrecoMax = baseParaFiltros.Any() ? baseParaFiltros.Max(v => v.VeiPreco) : 0
            };

            return resultado;
        }
    }
}
