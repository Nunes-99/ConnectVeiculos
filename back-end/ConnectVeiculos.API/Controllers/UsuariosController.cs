using ConnectVeiculos.Application.InputModels.Usuarios;
using ConnectVeiculos.Application.Interfaces.Usuarios;
using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsuariosController : ControllerBase
    {
        #region GET

        /// <summary>
        /// Verifica se um e-mail ja esta cadastrado em qualquer tenant.
        /// Retorna apenas (livre, em-uso-mesmo-tenant, em-uso-outro-tenant) sem
        /// expor de qual tenant especifico (privacidade entre clientes do SaaS).
        /// </summary>
        [HttpGet("check-email")]
        [Authorize(Roles = "Administrador,Gerente")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckEmail(
            [FromQuery] string email,
            [FromServices] MasterDbContext master,
            [FromServices] ITenantContext tenantContext)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return Ok(new { disponivel = false, motivo = "invalido" });

            var emailNorm = email.Trim().ToLowerInvariant();
            var existente = await master.UserEmailMaps.AsNoTracking().FirstOrDefaultAsync(u => u.Email == emailNorm);

            if (existente == null)
                return Ok(new { disponivel = true });

            var mesmoTenant = tenantContext.IsResolved && existente.TenantId == tenantContext.TenantId;
            return Ok(new
            {
                disponivel = false,
                motivo = mesmoTenant ? "ja-cadastrado-nesta-empresa" : "ja-cadastrado-em-outra-empresa"
            });
        }

        [HttpGet]
        public async Task<IActionResult> ConsultarUsuarios(
            [FromServices] IConsultarUsuariosUseCase consultarUsuariosUseCase,
            [FromQuery] string pesquisa = "",
            [FromQuery] string inicio = "0",
            [FromQuery] string intervalo = "50")
        {
            var usuarios = await consultarUsuariosUseCase.Execute(pesquisa, inicio, intervalo);
            return Ok(usuarios);
        }

        [HttpGet("paged")]
        public async Task<IActionResult> ConsultarUsuariosPaginado(
            [FromServices] IConsultarUsuariosPaginadoUseCase consultarUsuariosPaginadoUseCase,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var result = await consultarUsuariosPaginadoUseCase.Execute(page, pageSize, search);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ConsultarUsuarioPorId(
            [FromServices] IConsultarUsuarioPorIdUseCase consultarUsuarioPorIdUseCase,
            int id)
        {
            var usuario = await consultarUsuarioPorIdUseCase.Execute(id);

            if (usuario == null)
                return NotFound();

            return Ok(usuario);
        }

        #endregion

        #region POST

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> CadastrarUsuario(
            [FromServices] ICadastrarUsuarioUseCase cadastrarUsuarioUseCase,
            [FromServices] MasterDbContext master,
            [FromServices] ITenantContext tenantContext,
            [FromBody] UsuarioInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados do usuario nao informados.");

            // Bloqueia e-mail duplicado em qualquer tenant (registry global no master).
            var emailNorm = (inputModel.UsuEmail ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(emailNorm))
            {
                var existente = await master.UserEmailMaps.FirstOrDefaultAsync(u => u.Email == emailNorm);
                if (existente != null)
                {
                    if (tenantContext.IsResolved && existente.TenantId == tenantContext.TenantId)
                        return Conflict(new { message = "Este e-mail ja esta cadastrado para um usuario nesta empresa." });
                    return Conflict(new { message = "Este e-mail ja esta em uso em outra empresa do sistema." });
                }
            }

            var id = await cadastrarUsuarioUseCase.Execute(inputModel);

            // Registra no map global apos sucesso.
            if (!string.IsNullOrEmpty(emailNorm) && tenantContext.IsResolved)
            {
                master.UserEmailMaps.Add(new UserEmailMap(emailNorm, tenantContext.TenantId, tenantContext.TenantSlug));
                await master.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(ConsultarUsuarioPorId), new { id }, new { id });
        }

        #endregion

        #region PUT

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> AtualizarUsuario(
            [FromServices] IAtualizarUsuarioUseCase atualizarUsuarioUseCase,
            int id,
            [FromBody] UsuarioInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados do usuario nao informados.");

            inputModel.UsuId = id;
            await atualizarUsuarioUseCase.Execute(inputModel);
            return NoContent();
        }

        #endregion

        #region DELETE

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> InativarUsuario(
            [FromServices] IInativarUsuarioUseCase inativarUsuarioUseCase,
            int id)
        {
            await inativarUsuarioUseCase.Execute(id);
            return NoContent();
        }

        #endregion
    }
}
