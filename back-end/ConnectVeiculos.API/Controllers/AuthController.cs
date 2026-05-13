using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.InputModels.Auth;
using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;
using ConnectVeiculos.Application.Interfaces.Auth;
using ConnectVeiculos.Application.Interfaces.RecuperacaoSenha;
using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Database.Interceptors;
using ConnectVeiculos.Infrastructure.IoC;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller responsavel pela autenticacao de usuarios
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private static readonly Regex SlugSanitize = new("[^a-z0-9-]+", RegexOptions.Compiled);

        public sealed class RegistrarInputModel
        {
            public string NomeEmpresa { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Senha { get; set; } = string.Empty;
            public string ConfirmacaoSenha { get; set; } = string.Empty;
            public string? NomeAdmin { get; set; }
        }

        /// <summary>
        /// Cadastro publico self-service: cria um novo tenant isolado (banco proprio)
        /// e ja gera o usuario Administrador. Retorna JWT auto-login.
        /// </summary>
        /// <response code="201">Conta criada, retorna token JWT + slug do tenant</response>
        /// <response code="400">Dados invalidos</response>
        /// <response code="409">Slug derivado ja existe (raro — fallback gera unico automaticamente)</response>
        [HttpPost("registrar")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Registrar(
            [FromServices] MasterDbContext master,
            [FromServices] ITenantStore tenantStore,
            [FromServices] ITenantConnectionFactory connFactory,
            [FromServices] SoftDeleteInterceptor softDeleteInterceptor,
            [FromServices] IConfiguration configuration,
            [FromServices] ILogger<AuthController> logger,
            [FromBody] RegistrarInputModel input,
            CancellationToken ct)
        {
            if (input == null
                || string.IsNullOrWhiteSpace(input.NomeEmpresa)
                || string.IsNullOrWhiteSpace(input.Email)
                || string.IsNullOrWhiteSpace(input.Senha))
                return BadRequest(new { message = "Nome da empresa, e-mail e senha são obrigatórios." });

            if (input.Senha.Length < 6)
                return BadRequest(new { message = "Senha deve ter no minimo 6 caracteres." });

            if (input.Senha != input.ConfirmacaoSenha)
                return BadRequest(new { message = "Senha e confirmação não conferem." });

            if (!input.Email.Contains('@') || input.Email.Length > 255)
                return BadRequest(new { message = "E-mail inválido." });

            var emailNormalizadoRegistrar = input.Email.Trim().ToLowerInvariant();
            var emailJaUsado = await master.UserEmailMaps.AnyAsync(u => u.Email == emailNormalizadoRegistrar, ct);
            if (emailJaUsado)
                return Conflict(new { message = "Este e-mail ja esta cadastrado. Faca login com a senha existente ou recupere a senha." });

            var slugBase = GerarSlug(input.NomeEmpresa);
            if (slugBase.Length < 3)
                slugBase = GerarSlug(input.Email.Split('@')[0]);
            if (slugBase.Length < 3)
                slugBase = "loja-" + DateTime.UtcNow.Ticks.ToString().Substring(8);

            var slug = await ResolverSlugUnicoAsync(master, slugBase, ct);

            var tenant = new Tenant(slug, input.NomeEmpresa.Trim());
            master.Tenants.Add(tenant);
            await master.SaveChangesAsync(ct);

            logger.LogInformation("Self-registro: tenant '{Slug}' (id {Id}) criado", tenant.TenSlug, tenant.TenId);

            var connStr = connFactory.GetConnectionStringForTenant(tenant.TenSlug, tenant.TenDatabaseFile);
            var optionsBuilder = new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                .UseSqlite(connStr)
                .AddInterceptors(softDeleteInterceptor);

            int adminUsuId;
            using (var ctx = new ConnectVeiculosDbContext(optionsBuilder.Options))
            {
                await ctx.Database.EnsureCreatedAsync(ct);
                DependencyInjectionExtensions.ApplySchemaUpdates(ctx);
                DependencyInjectionExtensions.SeedSystemReferences(ctx);

                var senhaHash = BCrypt.Net.BCrypt.HashPassword(input.Senha);
                var admin = new Usuario(
                    usuId: 0,
                    usuNome: string.IsNullOrWhiteSpace(input.NomeAdmin) ? "Administrador" : input.NomeAdmin.Trim(),
                    usuCPF: string.Empty,
                    usuRG: string.Empty,
                    usuEmail: input.Email.Trim().ToLowerInvariant(),
                    usuSenha: senhaHash,
                    usuFuncao: "Administrador",
                    usuSts: true);
                ctx.Usuarios.Add(admin);
                await ctx.SaveChangesAsync(ct);
                adminUsuId = admin.UsuId;
            }

            master.UserEmailMaps.Add(new Core.Entities.Tenants.UserEmailMap(emailNormalizadoRegistrar, tenant.TenId, tenant.TenSlug));
            await master.SaveChangesAsync(ct);

            tenantStore.InvalidateCache();

            var (token, expiration) = GerarJwt(
                configuration,
                adminUsuId,
                input.Email.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(input.NomeAdmin) ? "Administrador" : input.NomeAdmin.Trim(),
                "Administrador",
                tenant.TenId,
                tenant.TenSlug);

            return StatusCode(StatusCodes.Status201Created, new
            {
                tenantSlug = tenant.TenSlug,
                tenantNome = tenant.TenNome,
                token,
                expiration,
                usuId = adminUsuId,
                usuNome = string.IsNullOrWhiteSpace(input.NomeAdmin) ? "Administrador" : input.NomeAdmin.Trim(),
                usuEmail = input.Email.Trim().ToLowerInvariant(),
                usuFuncao = "Administrador"
            });
        }

        private static string GerarSlug(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            var normalized = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            var ascii = sb.ToString().Normalize(NormalizationForm.FormC).Replace(' ', '-');
            ascii = SlugSanitize.Replace(ascii, "");
            ascii = ascii.Trim('-');
            if (ascii.Length > 0 && !char.IsLetter(ascii[0])) ascii = "loja-" + ascii;
            if (ascii.Length > 30) ascii = ascii.Substring(0, 30).Trim('-');
            return ascii;
        }

        private static async Task<string> ResolverSlugUnicoAsync(MasterDbContext master, string slugBase, CancellationToken ct)
        {
            var slug = slugBase;
            var sufixo = 2;
            while (await master.Tenants.AnyAsync(t => t.TenSlug == slug, ct))
            {
                slug = slugBase + "-" + sufixo;
                sufixo++;
                if (sufixo > 999) slug = slugBase + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            }
            return slug;
        }

        private static (string Token, DateTime Expiration) GerarJwt(
            IConfiguration configuration,
            int userId,
            string email,
            string nome,
            string funcao,
            int tenantId,
            string tenantSlug)
            => GerarJwtComJti(configuration, userId, email, nome, funcao, tenantId, tenantSlug, Guid.NewGuid().ToString());

        private static (string Token, DateTime Expiration) GerarJwtComJti(
            IConfiguration configuration,
            int userId,
            string email,
            string nome,
            string funcao,
            int tenantId,
            string tenantSlug,
            string jti)
        {
            var jwtSettings = configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            // JWT curto (default 1h). Refresh token cobre sessoes longas e permite revogacao no logout.
            var expirationHours = int.Parse(jwtSettings["ExpirationInHours"] ?? "1");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Email, email),
                new(ClaimTypes.Name, nome),
                new(ClaimTypes.Role, funcao),
                new("tenant_id", tenantId.ToString()),
                new("tenant_slug", tenantSlug),
                new(JwtRegisteredClaimNames.Jti, jti)
            };

            var expiration = DateTime.UtcNow.AddHours(expirationHours);
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiration,
                signingCredentials: creds);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
        }

        public sealed class RefreshTokenRequest
        {
            public string RefreshToken { get; set; } = string.Empty;
        }

        /// <summary>
        /// Troca refresh token por novo par (JWT + refresh) — usado quando o JWT
        /// expira e o cliente quer continuar a sessao sem login.
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken(
            [FromServices] MasterDbContext master,
            [FromServices] ITenantConnectionFactory connFactory,
            [FromServices] SoftDeleteInterceptor softDeleteInterceptor,
            [FromServices] IConfiguration configuration,
            [FromServices] ILogger<AuthController> logger,
            [FromBody] RefreshTokenRequest req,
            CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.RefreshToken))
                return BadRequest(new { message = "Refresh token é obrigatório." });

            var tenants = await master.Tenants
                .Where(t => t.TenStatus == TenantStatus.Active)
                .AsNoTracking()
                .ToListAsync(ct);

            foreach (var tenant in tenants)
            {
                var connStr = connFactory.GetConnectionStringForTenant(tenant.TenSlug, tenant.TenDatabaseFile);
                var optsBuilder = new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                    .UseSqlite(connStr)
                    .AddInterceptors(softDeleteInterceptor);

                using var ctx = new ConnectVeiculosDbContext(optsBuilder.Options);
                Core.Entities.RefreshTokens.RefreshToken? rt;
                try
                {
                    rt = await ctx.RefreshTokens.FirstOrDefaultAsync(r => r.RefToken == req.RefreshToken, ct);
                }
                catch
                {
                    continue;
                }

                if (rt == null || !rt.IsAtivo) continue;

                var usuario = await ctx.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.UsuId == rt.R_UsuId, ct);
                if (usuario == null || !usuario.UsuSts) return Unauthorized(new { message = "Usuario inativo ou removido." });

                // Emite par novo
                var newJti = Guid.NewGuid().ToString();
                var (newJwt, newJwtExp) = GerarJwtComJti(
                    configuration, usuario.UsuId, usuario.UsuEmail, usuario.UsuNome,
                    usuario.UsuFuncao ?? "Vendedor", tenant.TenId, tenant.TenSlug, newJti);

                var newRefresh = Core.Entities.RefreshTokens.RefreshToken.Criar(usuario.UsuId, newJti, 7);
                ctx.RefreshTokens.Add(newRefresh);
                rt.MarcarComoUsado(newRefresh.RefToken);
                await ctx.SaveChangesAsync(ct);

                logger.LogInformation("Refresh OK: usuario {Email} no tenant {Slug}", usuario.UsuEmail, tenant.TenSlug);

                return Ok(new Application.ViewModels.Auth.LoginViewModel
                {
                    UsuId = usuario.UsuId,
                    UsuNome = usuario.UsuNome,
                    UsuEmail = usuario.UsuEmail,
                    UsuFuncao = usuario.UsuFuncao ?? string.Empty,
                    Token = newJwt,
                    Expiration = newJwtExp,
                    RefreshToken = newRefresh.RefToken,
                    RefreshExpiration = newRefresh.RefExpiraEm,
                    TenantSlug = tenant.TenSlug,
                    TenantNome = tenant.TenNome
                });
            }

            return Unauthorized(new { message = "Refresh token inválido ou expirado." });
        }

        /// <summary>
        /// Revoga o refresh token do usuario (efetivo logout — JWT atual ainda
        /// vale ate expirar, mas nao pode mais ser renovado).
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout(
            [FromServices] MasterDbContext master,
            [FromServices] ITenantConnectionFactory connFactory,
            [FromServices] SoftDeleteInterceptor softDeleteInterceptor,
            [FromServices] ILogger<AuthController> logger,
            [FromBody] RefreshTokenRequest? req,
            CancellationToken ct)
        {
            // Tenta usar o tenant_slug do JWT para nao varrer todos os tenants.
            var tenantSlug = User.FindFirst("tenant_slug")?.Value;
            if (string.IsNullOrEmpty(tenantSlug))
                return Ok(new { mensagem = "Logout concluido (sem tenant claim no token)." });

            var tenant = await master.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.TenSlug == tenantSlug, ct);
            if (tenant == null)
                return Ok(new { mensagem = "Logout concluído (tenant não encontrado)." });

            var connStr = connFactory.GetConnectionStringForTenant(tenant.TenSlug, tenant.TenDatabaseFile);
            var optsBuilder = new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                .UseSqlite(connStr)
                .AddInterceptors(softDeleteInterceptor);

            using var ctx = new ConnectVeiculosDbContext(optsBuilder.Options);
            var revoked = 0;

            // Se o cliente passou o refresh token especifico, revoga so esse.
            // Caso contrario, revoga todos os ativos do usuario (logout total).
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Ok(new { mensagem = "Logout concluido (claim invalida)." });

            if (req != null && !string.IsNullOrWhiteSpace(req.RefreshToken))
            {
                var rt = await ctx.RefreshTokens.FirstOrDefaultAsync(r => r.RefToken == req.RefreshToken && r.R_UsuId == userId, ct);
                if (rt != null && rt.IsAtivo)
                {
                    rt.Revogar();
                    revoked = 1;
                }
            }
            else
            {
                var ativos = await ctx.RefreshTokens
                    .Where(r => r.R_UsuId == userId && !r.RefUsado && !r.RefRevogado && r.RefExpiraEm > DateTime.UtcNow)
                    .ToListAsync(ct);
                foreach (var rt in ativos) rt.Revogar();
                revoked = ativos.Count;
            }

            await ctx.SaveChangesAsync(ct);
            logger.LogInformation("Logout: usuario {UserId} no tenant {Slug} — {Count} refresh tokens revogados", userId, tenantSlug, revoked);

            return Ok(new { mensagem = "Logout concluido.", refreshTokensRevogados = revoked });
        }

        /// <summary>
        /// Realiza o login do usuario e retorna um token JWT
        /// </summary>
        /// <param name="loginUseCase">Use case de login injetado</param>
        /// <param name="input">Credenciais do usuario (email e senha)</param>
        /// <returns>Token JWT e dados do usuario autenticado</returns>
        /// <response code="200">Login realizado com sucesso</response>
        /// <response code="400">Dados de entrada invalidos</response>
        /// <response code="401">Email ou senha incorretos</response>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Login(
            [FromServices] MasterDbContext master,
            [FromServices] ITenantConnectionFactory connFactory,
            [FromServices] SoftDeleteInterceptor softDeleteInterceptor,
            [FromServices] IConfiguration configuration,
            [FromServices] ILogger<AuthController> logger,
            [FromBody] LoginInputModel input,
            CancellationToken ct)
        {
            if (input == null || string.IsNullOrEmpty(input.Email) || string.IsNullOrEmpty(input.Senha))
                return BadRequest("Email e senha são obrigatórios.");

            var emailNormalizado = input.Email.Trim().ToLowerInvariant();

            // Busca cross-tenant: itera todos os tenants ativos procurando o e-mail.
            // Pra cada match, valida a senha; primeiro hit valido vence.
            // Performance: com N tenants, faz ate N consultas SQLite (rapidas).
            // Para muitos tenants (~ centenas), considerar um indice email->tenant
            // no master pra evitar varredura.
            var tenants = await master.Tenants
                .Where(t => t.TenStatus == TenantStatus.Active)
                .AsNoTracking()
                .ToListAsync(ct);

            foreach (var tenant in tenants)
            {
                var connStr = connFactory.GetConnectionStringForTenant(tenant.TenSlug, tenant.TenDatabaseFile);
                var optsBuilder = new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                    .UseSqlite(connStr)
                    .AddInterceptors(softDeleteInterceptor);

                using var ctx = new ConnectVeiculosDbContext(optsBuilder.Options);
                Usuario? usuario;
                try
                {
                    usuario = await ctx.Usuarios
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.UsuEmail == emailNormalizado, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Falha ao consultar Usuarios no tenant {Slug}", tenant.TenSlug);
                    continue;
                }

                if (usuario == null) continue;
                if (!usuario.UsuSts) continue;
                if (!BCrypt.Net.BCrypt.Verify(input.Senha, usuario.UsuSenha)) continue;

                var jwtId = Guid.NewGuid().ToString();
                var (token, expiration) = GerarJwtComJti(
                    configuration,
                    usuario.UsuId,
                    usuario.UsuEmail,
                    usuario.UsuNome,
                    usuario.UsuFuncao ?? "Vendedor",
                    tenant.TenId,
                    tenant.TenSlug,
                    jwtId);

                // Persiste refresh token no banco do tenant (escrita: novo ctx sem AsNoTracking).
                using var writeCtx = new ConnectVeiculosDbContext(optsBuilder.Options);
                var refresh = Core.Entities.RefreshTokens.RefreshToken.Criar(usuario.UsuId, jwtId, 7);
                writeCtx.RefreshTokens.Add(refresh);
                await writeCtx.SaveChangesAsync(ct);

                logger.LogInformation("Login OK: usuario {Email} no tenant {Slug}", emailNormalizado, tenant.TenSlug);

                return Ok(new Application.ViewModels.Auth.LoginViewModel
                {
                    UsuId = usuario.UsuId,
                    UsuNome = usuario.UsuNome,
                    UsuEmail = usuario.UsuEmail,
                    UsuFuncao = usuario.UsuFuncao ?? string.Empty,
                    Token = token,
                    Expiration = expiration,
                    RefreshToken = refresh.RefToken,
                    RefreshExpiration = refresh.RefExpiraEm,
                    TenantSlug = tenant.TenSlug,
                    TenantNome = tenant.TenNome
                });
            }

            logger.LogInformation("Login FALHOU: e-mail {Email} nao encontrado em nenhum tenant ativo", emailNormalizado);
            return Unauthorized("Email ou senha inválidos.");
        }

        /// <summary>
        /// Solicita recuperacao de senha (envia email com token)
        /// </summary>
        /// <param name="useCase">Use case de solicitacao injetado</param>
        /// <param name="input">Email do usuario</param>
        /// <returns>Mensagem de sucesso</returns>
        /// <response code="200">Solicitacao processada</response>
        /// <response code="400">Dados de entrada invalidos</response>
        [HttpPost("recuperar-senha")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SolicitarRecuperacao(
            [FromServices] ISolicitarRecuperacaoSenhaUseCase useCase,
            [FromBody] SolicitarRecuperacaoInputModel input)
        {
            if (input == null || string.IsNullOrEmpty(input.Email))
                return BadRequest(new { message = "E-mail é obrigatório." });

            try
            {
                var token = await useCase.ExecutarAsync(input);
                // Sempre retorna sucesso por seguranca (nao revela se e-mail existe)
                return Ok(new {
                    mensagem = "Se o e-mail estiver cadastrado, você receberá as instruções para recuperação.",
                    token // Remover em producao
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Redefine a senha do usuario usando o token de recuperacao
        /// </summary>
        /// <param name="useCase">Use case de redefinicao injetado</param>
        /// <param name="input">Token e nova senha</param>
        /// <returns>Mensagem de sucesso</returns>
        /// <response code="200">Senha redefinida com sucesso</response>
        /// <response code="400">Token invalido ou expirado</response>
        [HttpPost("redefinir-senha")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RedefinirSenha(
            [FromServices] IRedefinirSenhaUseCase useCase,
            [FromBody] RedefinirSenhaInputModel input)
        {
            if (input == null)
                return BadRequest("Dados invalidos.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await useCase.ExecutarAsync(input);
                return Ok(new { mensagem = "Senha redefinida com sucesso." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Troca a senha do usuario autenticado (exige senha atual)
        /// </summary>
        /// <remarks>
        /// O token JWT atual continua valido ate sua expiracao natural — a nova
        /// senha so e exigida no proximo login. Essa abordagem evita interromper
        /// a sessao em curso quando o usuario troca a senha.
        /// </remarks>
        /// <response code="200">Senha alterada com sucesso</response>
        /// <response code="400">Senha atual incorreta ou nova senha invalida</response>
        /// <response code="401">Nao autenticado</response>
        [HttpPost("trocar-senha")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> TrocarSenha(
            [FromServices] ITrocarSenhaUseCase useCase,
            [FromBody] TrocarSenhaInputModel input)
        {
            if (input == null)
                return BadRequest("Dados invalidos.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("UserId")?.Value;

            if (!int.TryParse(usuarioIdClaim, out var usuarioId) || usuarioId <= 0)
                return Unauthorized("Token invalido.");

            try
            {
                await useCase.ExecutarAsync(usuarioId, input);
                return Ok(new
                {
                    mensagem = "Senha alterada com sucesso. A nova senha será exigida no próximo login.",
                });
            }
            catch (InputModelException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
