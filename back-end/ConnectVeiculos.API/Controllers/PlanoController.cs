using ConnectVeiculos.Core.Interfaces.Database.Repositories.Tenants;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlanoController : ControllerBase
    {
         // Lista publica dos planos (sem auth) — usada pela landing pra renderizar
         // os cards de preco dinamicamente em vez de hardcoded no Angular.
         [HttpGet]
         [AllowAnonymous]
         public async Task<IActionResult> ListarPlanos([FromServices] IPlanoRepository planoRepo, CancellationToken ct)
         {
             var planos = await planoRepo.ListarAtivosAsync(ct);
             return Ok(planos.Select(p => new
             {
                 id = p.PlaId,
                 nome = p.PlaNome,
                 preco = p.PlaPreco,
                 maxVeiculos = p.PlaMaxVeiculos,
                 maxLojas = p.PlaMaxLojas,
                 maxUsuarios = p.PlaMaxUsuarios,
                 maxLeadsMes = p.PlaMaxLeadsMes,
                 ordem = p.PlaOrdem
             }));
         }

         // Plano atual do tenant logado + uso ativo. Frontend usa pra renderizar
         // a tela 'Meu Plano' com badges X/Y por recurso e indicador de trial.
         [HttpGet("meu")]
         [Authorize]
         public async Task<IActionResult> MeuPlano(
             [FromServices] IPlanoRepository planoRepo,
             [FromServices] ITenantStore tenantStore,
             [FromServices] ITenantContext tenantContext,
             [FromServices] ConnectVeiculosDbContext tenantDb,
             CancellationToken ct)
         {
             if (!tenantContext.IsResolved)
                 return BadRequest(new { error = "tenant_nao_resolvido" });

             var tenant = await tenantStore.GetByIdAsync(tenantContext.TenantId, ct);
             if (tenant == null) return NotFound();

             var emTrial = tenant.EmTrial();
             var diasRestantesTrial = emTrial && tenant.TenTrialAte.HasValue
                 ? (int)Math.Ceiling((tenant.TenTrialAte.Value - DateTime.UtcNow).TotalDays)
                 : 0;

             var plano = tenant.TenPlaId.HasValue
                 ? await planoRepo.GetByIdAsync(tenant.TenPlaId.Value, ct)
                 : null;

             // Conta uso atual de cada recurso no banco do tenant.
             var usoVeiculos = await tenantDb.Veiculos.Where(v => v.VeiSts != "I").CountAsync(ct);
             var usoLojas = await tenantDb.Lojas.CountAsync(ct);
             var usoUsuarios = await tenantDb.Usuarios.Where(u => u.UsuSts).CountAsync(ct);
             var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
             var usoLeadsMes = await tenantDb.Leads.Where(l => l.LeaDtCriacao >= inicioMes).CountAsync(ct);

             return Ok(new
             {
                 tenantId = tenant.TenId,
                 tenantNome = tenant.TenNome,
                 emTrial,
                 trialAte = tenant.TenTrialAte,
                 diasRestantesTrial,
                 plano = plano == null ? null : new
                 {
                     id = plano.PlaId,
                     nome = plano.PlaNome,
                     preco = plano.PlaPreco,
                     maxVeiculos = plano.PlaMaxVeiculos,
                     maxLojas = plano.PlaMaxLojas,
                     maxUsuarios = plano.PlaMaxUsuarios,
                     maxLeadsMes = plano.PlaMaxLeadsMes
                 },
                 uso = new
                 {
                     veiculos = usoVeiculos,
                     lojas = usoLojas,
                     usuarios = usoUsuarios,
                     leadsMes = usoLeadsMes
                 }
             });
         }
    }
}
