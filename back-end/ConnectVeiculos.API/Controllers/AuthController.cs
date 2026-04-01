using ConnectVeiculos.Application.InputModels.Auth;
using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;
using ConnectVeiculos.Application.Interfaces.Auth;
using ConnectVeiculos.Application.Interfaces.RecuperacaoSenha;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
            [FromServices] ILoginUseCase loginUseCase,
            [FromBody] LoginInputModel input)
        {
            if (input == null || string.IsNullOrEmpty(input.Email) || string.IsNullOrEmpty(input.Senha))
                return BadRequest("Email e senha são obrigatórios.");

            var result = await loginUseCase.Execute(input);

            if (result == null)
                return Unauthorized("Email ou senha inválidos.");

            return Ok(result);
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
                return BadRequest(new { message = "E-mail e obrigatorio." });

            try
            {
                var token = await useCase.ExecutarAsync(input);
                // Sempre retorna sucesso por seguranca (nao revela se e-mail existe)
                return Ok(new {
                    mensagem = "Se o e-mail estiver cadastrado, voce recebera as instrucoes para recuperacao.",
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
    }
}
