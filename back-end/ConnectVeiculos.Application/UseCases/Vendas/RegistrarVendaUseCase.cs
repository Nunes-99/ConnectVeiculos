using ConnectVeiculos.Application.InputModels.Vendas;
using ConnectVeiculos.Application.Interfaces.Vendas;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Services;

namespace ConnectVeiculos.Application.UseCases.Vendas
{
    public class RegistrarVendaUseCase : IRegistrarVendaUseCase
    {
        private readonly IVendaRepository _vendaRepository;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IEmailService _emailService;
        private readonly INotificacaoService _notificacaoService;
        private readonly ICatalogoHubService _catalogoHubService;

        public RegistrarVendaUseCase(
            IVendaRepository vendaRepository,
            IVeiculoRepository veiculoRepository,
            IEmailService emailService,
            INotificacaoService notificacaoService,
            ICatalogoHubService catalogoHubService)
        {
            _vendaRepository = vendaRepository;
            _veiculoRepository = veiculoRepository;
            _emailService = emailService;
            _notificacaoService = notificacaoService;
            _catalogoHubService = catalogoHubService;
        }

        public async Task<int> Execute(VendaInputModel inputModel)
        {
            var veiculo = await _veiculoRepository.GetByIdAsync(inputModel.R_VeiId);

            if (veiculo == null)
                throw new DomainException("Veiculo nao encontrado.");

            if (veiculo.VeiSts == "V")
                throw new DomainException("Este veiculo ja foi vendido.");

            // Marcar veículo como vendido ANTES de criar a venda (evita venda duplicada)
            veiculo.AlterarStatus("V");
            await _veiculoRepository.UpdateAsync(veiculo);

            // Validar comissão
            if (inputModel.VenComissaoPorc < 0) inputModel.VenComissaoPorc = 0;
            if (inputModel.VenComissaoPorc > 100) inputModel.VenComissaoPorc = 100;

            // Calcular valor da comissao
            var comissaoValor = inputModel.VenValor * (inputModel.VenComissaoPorc / 100);

            var venda = new Venda(
                0,
                inputModel.R_VeiId,
                inputModel.R_UsuId,
                inputModel.VenDtVenda,
                veiculo.VeiMarca,
                veiculo.VeiModelo,
                veiculo.VeiAno,
                veiculo.VeiChassi,
                inputModel.VenValor,
                inputModel.VenComissaoPorc,
                comissaoValor,
                inputModel.VenCompradorNome,
                inputModel.VenCompradorCpf,
                inputModel.VenCompradorTelefone,
                inputModel.VenCompradorEmail,
                inputModel.VenCompradorEndereco,
                inputModel.VenFormaPagamento,
                inputModel.VenObservacao
            );

            var vendaId = await _vendaRepository.CreateAsync(venda);

            // Enviar email de confirmacao ao comprador
            if (!string.IsNullOrEmpty(inputModel.VenCompradorEmail))
            {
                var veiculoDescricao = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}";
                await _emailService.SendVendaConfirmadaAsync(
                    inputModel.VenCompradorEmail,
                    inputModel.VenCompradorNome,
                    veiculoDescricao,
                    inputModel.VenValor
                );
            }

            // Enviar notificacao em tempo real (usuarios internos)
            await _notificacaoService.EnviarParaTodosAsync("NOVA_VENDA", new
            {
                vendaId,
                veiculoNome = $"{veiculo.VeiMarca} {veiculo.VeiModelo}",
                valor = inputModel.VenValor,
                comprador = inputModel.VenCompradorNome
            });

            // Notificar catalogo publico que veiculo foi vendido
            await _catalogoHubService.NotificarAtualizacaoCatalogo(veiculo.R_LojId, "VEICULO_VENDIDO", new
            {
                veiculoId = veiculo.VeiId,
                marca = veiculo.VeiMarca,
                modelo = veiculo.VeiModelo
            });

            return vendaId;
        }
    }
}
