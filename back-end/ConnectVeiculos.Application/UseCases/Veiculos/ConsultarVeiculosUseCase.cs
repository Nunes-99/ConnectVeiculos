using System.Globalization;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Application.ViewModels.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Veiculos;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class ConsultarVeiculosUseCase : IConsultarVeiculosUseCase
    {
        private readonly IVeiculoOperations _veiculoOperations;

        public ConsultarVeiculosUseCase(IVeiculoOperations veiculoOperations)
        {
            _veiculoOperations = veiculoOperations;
        }

        public async Task<List<VeiculoViewModel>> Execute(string pesquisa, int? lojaId, string inicio, string intervalo)
        {
            var result = await _veiculoOperations.ConsultarVisualizacaoVeiculos(pesquisa, lojaId, inicio, intervalo);

            if (result == null)
                return new List<VeiculoViewModel>();

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
                VeiCor = v.VeiCor,
                VeiKm = (int)(long)v.VeiKm,
                VeiPreco = ParseDecimal(v.VeiPreco),
                VeiDtEntrada = v.VeiDtEntrada is DateTime dt ? dt : DateTime.TryParse((string)v.VeiDtEntrada, out var parsed) ? parsed : DateTime.MinValue,
                VeiSts = v.VeiSts,
                VeiSitSts = v.VeiSitSts,
                VeiPrecoCompra = ParseDecimal(v.VeiPrecoCompra),
                VeiObservacao = v.VeiObservacao is string obs ? obs : null,
                VeiPostadoInsta = v.VeiPostadoInsta is long pi ? pi != 0 : v.VeiPostadoInsta is bool bpi && bpi,
                VeiPostadoFace = v.VeiPostadoFace is long pf ? pf != 0 : v.VeiPostadoFace is bool bpf && bpf,
                VeiDtPostagemInsta = v.VeiDtPostagemInsta is DateTime dti ? dti : v.VeiDtPostagemInsta is string si && DateTime.TryParse(si, out var parsedInsta) ? parsedInsta : null,
                VeiDtPostagemFace = v.VeiDtPostagemFace is DateTime dtf ? dtf : v.VeiDtPostagemFace is string sf && DateTime.TryParse(sf, out var parsedFace) ? parsedFace : null
            }).ToList();

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
    }
}
