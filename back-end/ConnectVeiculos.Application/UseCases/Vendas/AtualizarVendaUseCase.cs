using ConnectVeiculos.Application.InputModels.Vendas;
using ConnectVeiculos.Application.Interfaces.Vendas;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;

namespace ConnectVeiculos.Application.UseCases.Vendas
{
    /// <summary>
    /// Atualiza apenas os dados nao-financeiros de uma venda (comprador, forma de
    /// pagamento, observacao). Valor, comissao, veiculo e data sao imutaveis —
    /// pra alterar isso a venda precisa ser estornada e re-registrada.
    /// </summary>
    public class AtualizarVendaUseCase : IAtualizarVendaUseCase
    {
        private readonly IVendaRepository _vendaRepository;

        public AtualizarVendaUseCase(IVendaRepository vendaRepository)
        {
            _vendaRepository = vendaRepository;
        }

        public async Task Execute(int vendaId, VendaInputModel inputModel)
        {
            var venda = await _vendaRepository.GetByIdAsync(vendaId);
            if (venda == null)
                throw new DomainException("Venda não encontrada.");

            venda.AtualizarDadosComprador(
                inputModel.VenCompradorNome,
                inputModel.VenCompradorCpf,
                inputModel.VenCompradorTelefone,
                inputModel.VenCompradorEmail,
                inputModel.VenCompradorEndereco,
                inputModel.VenFormaPagamento,
                inputModel.VenObservacao);

            await _vendaRepository.UpdateAsync(venda);
        }
    }
}
