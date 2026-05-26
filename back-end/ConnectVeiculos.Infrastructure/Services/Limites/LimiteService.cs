using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Tenants;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Services.Limites
{
    public class LimiteService : ILimiteService
    {
        private readonly ConnectVeiculosDbContext _tenantDb;
        private readonly IPlanoRepository _planoRepo;
        private readonly ITenantStore _tenantStore;
        private readonly ITenantContext _tenantContext;

        public LimiteService(
            ConnectVeiculosDbContext tenantDb,
            IPlanoRepository planoRepo,
            ITenantStore tenantStore,
            ITenantContext tenantContext)
        {
            _tenantDb = tenantDb;
            _planoRepo = planoRepo;
            _tenantStore = tenantStore;
            _tenantContext = tenantContext;
        }

        public async Task GarantirPodeCriarVeiculoAsync(CancellationToken ct = default)
        {
            var (plano, emTrial) = await ResolverPlanoDoTenantAtual(ct);
            if (emTrial || plano?.PlaMaxVeiculos == null) return;

            // Conta apenas veiculos ativos (nao 'I' inativo).
            var atual = await _tenantDb.Veiculos
                .Where(v => v.VeiSts != "I")
                .CountAsync(ct);
            if (atual >= plano.PlaMaxVeiculos.Value)
                throw new LimitePlanoException("veículos", plano.PlaMaxVeiculos.Value, atual, plano.PlaNome);
        }

        public async Task GarantirPodeCriarLojaAsync(CancellationToken ct = default)
        {
            var (plano, emTrial) = await ResolverPlanoDoTenantAtual(ct);
            if (emTrial || plano?.PlaMaxLojas == null) return;

            var atual = await _tenantDb.Lojas.CountAsync(ct);
            if (atual >= plano.PlaMaxLojas.Value)
                throw new LimitePlanoException("lojas", plano.PlaMaxLojas.Value, atual, plano.PlaNome);
        }

        public async Task GarantirPodeCriarUsuarioAsync(CancellationToken ct = default)
        {
            var (plano, emTrial) = await ResolverPlanoDoTenantAtual(ct);
            if (emTrial || plano?.PlaMaxUsuarios == null) return;

            var atual = await _tenantDb.Usuarios
                .Where(u => u.UsuSts)
                .CountAsync(ct);
            if (atual >= plano.PlaMaxUsuarios.Value)
                throw new LimitePlanoException("usuários", plano.PlaMaxUsuarios.Value, atual, plano.PlaNome);
        }

        private async Task<(Core.Entities.Tenants.Plano? Plano, bool EmTrial)> ResolverPlanoDoTenantAtual(CancellationToken ct)
        {
            if (!_tenantContext.IsResolved) return (null, false);

            var tenant = await _tenantStore.GetByIdAsync(_tenantContext.TenantId, ct);
            if (tenant == null) return (null, false);

            var emTrial = tenant.EmTrial();
            var planoId = tenant.TenPlaId;
            if (planoId == null) return (null, emTrial);

            var plano = await _planoRepo.GetByIdAsync(planoId.Value, ct);
            return (plano, emTrial);
        }
    }
}
