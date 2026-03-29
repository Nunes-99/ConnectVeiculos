using ConnectVeiculos.Application.Interfaces.Vendas;
using ConnectVeiculos.Application.ViewModels.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;

namespace ConnectVeiculos.Application.UseCases.Vendas
{
    public class ConsultarVendasUseCase : IConsultarVendasUseCase
    {
        private readonly IVendaRepository _vendaRepository;

        public ConsultarVendasUseCase(IVendaRepository vendaRepository)
        {
            _vendaRepository = vendaRepository;
        }

        public async Task<IEnumerable<VendaViewModel>> Execute()
        {
            var vendas = await _vendaRepository.GetAllAsync();

            return vendas.Select(v => new VendaViewModel
            {
                VenId = v.VenId,
                R_VeiId = v.R_VeiId,
                R_UsuId = v.R_UsuId,
                VendedorNome = v.Vendedor?.UsuNome ?? "",
                VenDtVenda = v.VenDtVenda,
                VenMarca = v.VenMarca,
                VenModelo = v.VenModelo,
                VenAno = v.VenAno,
                VenChassi = v.VenChassi,
                VenValor = v.VenValor,
                VenComissaoPorc = v.VenComissaoPorc,
                VenComissaoValor = v.VenComissaoValor,
                VenCompradorNome = v.VenCompradorNome,
                VenCompradorCpf = v.VenCompradorCpf,
                VenCompradorTelefone = v.VenCompradorTelefone,
                VenCompradorEmail = v.VenCompradorEmail,
                VenCompradorEndereco = v.VenCompradorEndereco,
                VenFormaPagamento = v.VenFormaPagamento,
                VenObservacao = v.VenObservacao,
                VenStatus = v.VenStatus,
                VenDtEstorno = v.VenDtEstorno
            }).OrderByDescending(v => v.VenDtVenda);
        }
    }
}
