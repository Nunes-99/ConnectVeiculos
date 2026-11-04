using ConnectVeiculos.API.Controllers;
using ConnectVeiculos.Core.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Controllers
{
    /// <summary>
    /// A volta da Credere chega no connectveiculos.dev.br, muitas vezes sem
    /// sessao (a loja entrou pelo dominio proprio ou o login expirou). O guard
    /// do Angular jogava o codigo fora; agora a loja vem do state.
    /// </summary>
    public class CredereRetornoTests
    {
        private sealed class Tenant : ITenantContext
        {
            public bool IsResolved { get; private set; }
            public int TenantId { get; private set; }
            public string TenantSlug { get; private set; } = string.Empty;
            public string DatabaseFile { get; private set; } = string.Empty;
            public void Resolve(int tenantId, string slug, string databaseFile)
            {
                IsResolved = true; TenantId = tenantId; TenantSlug = slug; DatabaseFile = databaseFile;
            }
        }

        private readonly Mock<ICredereService> _credere = new();
        private readonly Mock<IOAuthStateProtector> _state = new();

        private static object? Campo(IActionResult r, string nome)
        {
            var v = ((ObjectResult)r).Value!;
            return v.GetType().GetProperty(nome)!.GetValue(v);
        }

        [Fact]
        public async Task State_De_Outra_Loja_Devolve_O_Slug_Sem_Conectar()
        {
            _state.Setup(s => s.Decifrar("st")).Returns(new OAuthStatePayload("diamante", "n", DateTime.UtcNow.AddMinutes(5)));
            var tenant = new Tenant();
            tenant.Resolve(1, "default", "cliente.db");

            var r = await new CredereController(_credere.Object).Retorno(new CredereController.ConexaoInput { Code = "c", State = "st" }, _state.Object, tenant, default);

            r.Should().BeOfType<OkObjectResult>();
            Campo(r, "conectado").Should().Be(false);
            Campo(r, "tenant").Should().Be("diamante");
            _credere.Verify(c => c.ConectarAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Na_Loja_Certa_Troca_O_Codigo()
        {
            _state.Setup(s => s.Decifrar("st")).Returns(new OAuthStatePayload("diamante", "n", DateTime.UtcNow.AddMinutes(5)));
            _credere.Setup(c => c.ConectarAsync("c", "st", It.IsAny<CancellationToken>())).ReturnsAsync(new CredereStatus { Conectado = true });
            var tenant = new Tenant();
            tenant.Resolve(7, "diamante", "diamante.db");

            var r = await new CredereController(_credere.Object).Retorno(new CredereController.ConexaoInput { Code = "c", State = "st" }, _state.Object, tenant, default);

            Campo(r, "conectado").Should().Be(true);
            _credere.Verify(c => c.ConectarAsync("c", "st", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task State_Invalido_E_Recusado_Com_Mensagem()
        {
            _state.Setup(s => s.Decifrar(It.IsAny<string?>())).Throws(new OAuthStateException("State invalido ou expirado. Tente conectar de novo."));

            var r = await new CredereController(_credere.Object).Retorno(new CredereController.ConexaoInput { Code = "c", State = "x" }, _state.Object, new Tenant(), default);

            r.Should().BeOfType<UnprocessableEntityObjectResult>();
            Campo(r, "error").Should().Be("State invalido ou expirado. Tente conectar de novo.");
        }

        [Fact]
        public async Task Recusa_Da_Credere_Vira_Mensagem()
        {
            _state.Setup(s => s.Decifrar("st")).Returns(new OAuthStatePayload("diamante", "n", DateTime.UtcNow.AddMinutes(5)));
            _credere.Setup(c => c.ConectarAsync("c", "st", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CredereException("Esta autorizacao nao foi iniciada neste navegador."));
            var tenant = new Tenant();
            tenant.Resolve(7, "diamante", "diamante.db");

            var r = await new CredereController(_credere.Object).Retorno(new CredereController.ConexaoInput { Code = "c", State = "st" }, _state.Object, tenant, default);

            r.Should().BeOfType<UnprocessableEntityObjectResult>();
        }
    }
}
