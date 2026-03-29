using ConnectVeiculos.Application.Interfaces.Vendas;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Email;

namespace ConnectVeiculos.Application.UseCases.Vendas
{
    public class EstornarVendaUseCase : IEstornarVendaUseCase
    {
        private readonly IVendaRepository _vendaRepository;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IEmailService _emailService;

        public EstornarVendaUseCase(
            IVendaRepository vendaRepository,
            IVeiculoRepository veiculoRepository,
            IEmailService emailService)
        {
            _vendaRepository = vendaRepository;
            _veiculoRepository = veiculoRepository;
            _emailService = emailService;
        }

        public async Task Execute(int vendaId)
        {
            var venda = await _vendaRepository.GetByIdAsync(vendaId);

            if (venda == null)
                throw new DomainException("Venda nao encontrada.");

            // Guardar dados para email antes do estorno
            var compradorEmail = venda.VenCompradorEmail;
            var compradorNome = venda.VenCompradorNome;
            var veiculoDescricao = $"{venda.VenMarca} {venda.VenModelo} {venda.VenAno}";

            // Estornar a venda
            venda.Estornar();
            await _vendaRepository.UpdateAsync(venda);

            // Retornar o veiculo para disponivel
            var veiculo = await _veiculoRepository.GetByIdAsync(venda.R_VeiId);
            if (veiculo != null)
            {
                veiculo.AlterarStatus("D");
                await _veiculoRepository.UpdateAsync(veiculo);
            }

            // Enviar email de notificacao de estorno
            if (!string.IsNullOrEmpty(compradorEmail))
            {
                await _emailService.SendVendaEstornadaAsync(
                    compradorEmail,
                    compradorNome,
                    veiculoDescricao
                );
            }
        }
    }
}
