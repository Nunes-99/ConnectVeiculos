using ConnectVeiculos.Application.InputModels.Vendas;
using ConnectVeiculos.Application.Interfaces.Vendas;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;

namespace ConnectVeiculos.Application.UseCases.Vendas
{
    public class RegistrarVendaUseCase : IRegistrarVendaUseCase
    {
        private readonly IVendaRepository _vendaRepository;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IEmailService _emailService;
        private readonly INotificacaoService _notificacaoService;
        private readonly ICatalogoHubService _catalogoHubService;
        private readonly ITenantContext _tenantContext;
        private readonly ITenantBackgroundRunner _backgroundRunner;

        public RegistrarVendaUseCase(
            IVendaRepository vendaRepository,
            IVeiculoRepository veiculoRepository,
            IEmailService emailService,
            INotificacaoService notificacaoService,
            ICatalogoHubService catalogoHubService,
            ITenantContext tenantContext,
            ITenantBackgroundRunner backgroundRunner)
        {
            _vendaRepository = vendaRepository;
            _veiculoRepository = veiculoRepository;
            _emailService = emailService;
            _notificacaoService = notificacaoService;
            _catalogoHubService = catalogoHubService;
            _tenantContext = tenantContext;
            _backgroundRunner = backgroundRunner;
        }

        public async Task<int> Execute(VendaInputModel inputModel)
        {
            var veiculo = await _veiculoRepository.GetByIdAsync(inputModel.R_VeiId);

            if (veiculo == null)
                throw new DomainException("Veículo não encontrado.");

            if (veiculo.VeiSts == "V")
                throw new DomainException("Este veículo já foi vendido.");

            // Marcar veículo como vendido ANTES de criar a venda (evita venda duplicada)
            veiculo.AlterarStatus("V");
            await _veiculoRepository.UpdateAsync(veiculo);

            // Registrar a venda aqui nao avisava nenhuma plataforma: o anuncio do
            // Mercado Livre seguia aberto e o post do Facebook continuava
            // anunciando o carro. So o caminho de editar o veiculo fazia isso.
            //
            // Em background porque sao varias chamadas de rede: a venda nao pode
            // depender do Facebook estar de pe.
            var idParaPlataformas = veiculo.VeiId;
            _backgroundRunner.Enqueue<IPublicacaoAutomaticaService>(
                s => s.MarcarVeiculoIndisponivelAsync(idParaPlataformas, "V"),
                $"tirar veiculo {idParaPlataformas} das plataformas apos a venda");

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
            await _catalogoHubService.NotificarAtualizacaoCatalogo(_tenantContext.TenantSlug, veiculo.R_LojId, "VEICULO_VENDIDO", new
            {
                veiculoId = veiculo.VeiId,
                marca = veiculo.VeiMarca,
                modelo = veiculo.VeiModelo
            });

            return vendaId;
        }
    }
}
