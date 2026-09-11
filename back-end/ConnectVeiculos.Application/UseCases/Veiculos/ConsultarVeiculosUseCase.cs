using System.Globalization;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Application.ViewModels.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class ConsultarVeiculosUseCase : IConsultarVeiculosUseCase
    {
        private readonly IVeiculoOperations _veiculoOperations;
        private readonly IVeiculoPublicacaoRepository _publicacaoRepository;

        public ConsultarVeiculosUseCase(
            IVeiculoOperations veiculoOperations,
            IVeiculoPublicacaoRepository publicacaoRepository)
        {
            _veiculoOperations = veiculoOperations;
            _publicacaoRepository = publicacaoRepository;
        }

        public async Task<List<VeiculoViewModel>> Execute(string pesquisa, int? lojaId, string inicio, string intervalo)
        {
            var result = await _veiculoOperations.ConsultarVisualizacaoVeiculos(pesquisa, lojaId, inicio, intervalo);

            if (result == null)
                return new List<VeiculoViewModel>();

            // Uma consulta so pras publicacoes de todos os veiculos. A tela pinta
            // o icone da rede conforme o que ja foi publicado; perguntar por
            // veiculo seria N+1.
            var publicacoes = (await _publicacaoRepository.GetAtivasAsync())
                .GroupBy(p => p.R_VeiId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var veiculos = ((IEnumerable<dynamic>)result).Select(v => new VeiculoViewModel
            {
                VeiId = (int)(long)v.VeiId,
                R_LojId = (int)(long)v.R_LojId,
                LojaNome = v.LojaNome,
                R_CatId = (int)(long)v.R_CatId,
                CategoriaNome = v.CategoriaNome,
                VeiMarca = v.VeiMarca,
                VeiModelo = v.VeiModelo,
                VeiAno = (short)(long)v.VeiAno,
                VeiPlaca = v.VeiPlaca,
                VeiChassi = v.VeiChassi,
                VeiRenavam = v.VeiRenavam,
                VeiCor = v.VeiCor,
                VeiKm = (int)(long)v.VeiKm,
                VeiPreco = ParseDecimal(v.VeiPreco),
                VeiDtEntrada = v.VeiDtEntrada is DateTime dt ? dt : DateTime.TryParse((string)v.VeiDtEntrada, out var parsed) ? parsed : DateTime.MinValue,
                VeiSts = v.VeiSts,
                VeiSitSts = v.VeiSitSts,
                VeiPrecoCompra = ParseDecimal(v.VeiPrecoCompra),
                VeiObservacao = v.VeiObservacao is string obs ? obs : null,
                VeiOpcionais = v.VeiOpcionais is string opc ? opc : null,
                VeiDonoAtual = v.VeiDonoAtual is string da ? da : null,
                VeiDonoCelular = v.VeiDonoCelular is string dc ? dc : null,
                VeiPrecoFipe = ParseNullableDecimal(v.VeiPrecoFipe),
                VeiPostadoInsta = v.VeiPostadoInsta is long pi ? pi != 0 : v.VeiPostadoInsta is bool bpi && bpi,
                VeiPostadoFace = v.VeiPostadoFace is long pf ? pf != 0 : v.VeiPostadoFace is bool bpf && bpf,
                VeiDtPostagemInsta = v.VeiDtPostagemInsta is DateTime dti ? dti : v.VeiDtPostagemInsta is string si && DateTime.TryParse(si, out var parsedInsta) ? parsedInsta : null,
                VeiDtPostagemFace = v.VeiDtPostagemFace is DateTime dtf ? dtf : v.VeiDtPostagemFace is string sf && DateTime.TryParse(sf, out var parsedFace) ? parsedFace : null
            }).ToList();

            foreach (var veiculo in veiculos)
            {
                if (!publicacoes.TryGetValue(veiculo.VeiId, out var doVeiculo)) continue;

                var ig = doVeiculo.FirstOrDefault(p => p.PubPlataforma == "Instagram");
                if (ig != null)
                {
                    veiculo.PublicacaoInstagramUrl = ig.PubUrl;
                    veiculo.PublicacaoInstagramEm = ig.PubDtPublicacao;
                }

                var fb = doVeiculo.FirstOrDefault(p => p.PubPlataforma == "FacebookPage");
                if (fb != null)
                {
                    veiculo.PublicacaoFacebookUrl = fb.PubUrl;
                    veiculo.PublicacaoFacebookEm = fb.PubDtPublicacao;
                }
            }

            return veiculos;
        }

        private static decimal ParseDecimal(dynamic value)
        {
            if (value is decimal d) return d;
            if (value is double dbl) return (decimal)dbl;
            if (value is long l) return l;
            if (value is int i) return i;
            if (value is string s) return decimal.Parse(s, CultureInfo.InvariantCulture);
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        // VeiPrecoFipe é armazenado como TEXT no SQLite (ValueConverter em
        // ConnectVeiculosDbContext); Dapper devolve string em vez de double.
        // Cobrimos os outros tipos pra resistir a registros legados.
        private static decimal? ParseNullableDecimal(dynamic value)
        {
            if (value == null) return null;
            if (value is decimal d) return d;
            if (value is double dbl) return (decimal)dbl;
            if (value is long l) return l;
            if (value is int i) return i;
            if (value is string s)
                return string.IsNullOrWhiteSpace(s) ? (decimal?)null
                    : decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : (decimal?)null;
            return null;
        }
    }
}
