using ConnectVeiculos.Application.Interfaces.Vendas;
using ConnectVeiculos.Application.ViewModels.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;

namespace ConnectVeiculos.Application.UseCases.Vendas
{
    public class ConsultarVendaPorIdUseCase : IConsultarVendaPorIdUseCase
    {
        private readonly IVendaRepository _vendaRepository;

        public ConsultarVendaPorIdUseCase(IVendaRepository vendaRepository)
        {
            _vendaRepository = vendaRepository;
        }

        public async Task<VendaViewModel> Execute(int id)
        {
            var venda = await _vendaRepository.GetByIdAsync(id);

            if (venda == null)
                return null;

            return new VendaViewModel
            {
                VenId = venda.VenId,
                R_VeiId = venda.R_VeiId,
                R_UsuId = venda.R_UsuId,
                VendedorNome = venda.Vendedor?.UsuNome ?? "",
                VenDtVenda = venda.VenDtVenda,
                VenMarca = venda.VenMarca,
                VenModelo = venda.VenModelo,
                VenAno = venda.VenAno,
                VenChassi = venda.VenChassi,
                VenValor = venda.VenValor,
                VenComissaoPorc = venda.VenComissaoPorc,
                VenComissaoValor = venda.VenComissaoValor,
                VenCompradorNome = venda.VenCompradorNome,
                VenCompradorCpf = venda.VenCompradorCpf,
                VenCompradorTelefone = venda.VenCompradorTelefone,
                VenCompradorEmail = venda.VenCompradorEmail,
                VenCompradorEndereco = venda.VenCompradorEndereco,
                VenFormaPagamento = venda.VenFormaPagamento,
                VenObservacao = venda.VenObservacao,
                VenStatus = venda.VenStatus,
                VenDtEstorno = venda.VenDtEstorno
            };
        }
    }
}
