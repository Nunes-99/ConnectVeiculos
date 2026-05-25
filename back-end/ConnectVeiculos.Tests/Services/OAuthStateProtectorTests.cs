using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;

namespace ConnectVeiculos.Tests.Services
{
    public class OAuthStateProtectorTests
    {
        private readonly IOAuthStateProtector _stateProtector;
        private readonly ITokenProtector _tokenProtector;

        public OAuthStateProtectorTests()
        {
            // EphemeralDataProtectionProvider gera chaves novas a cada instancia —
            // perfeito pra testes isolados.
            var dataProtectionProvider = new EphemeralDataProtectionProvider();
            _tokenProtector = new DataProtectionTokenProtector(dataProtectionProvider);
            _stateProtector = new OAuthStateProtector(_tokenProtector);
        }

        [Fact]
        public void Validar_StateValido_DeveRetornarPayloadComTenantCorreto()
        {
            var state = _stateProtector.Proteger("acme");
            var payload = _stateProtector.Validar(state, "acme");
            payload.TenantSlug.Should().Be("acme");
            payload.Nonce.Should().NotBeNullOrWhiteSpace();
            payload.ExpiraEm.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public void Validar_StateVazio_DeveLancarException()
        {
            var act = () => _stateProtector.Validar("", "acme");
            act.Should().Throw<OAuthStateException>().WithMessage("*state*");
        }

        [Fact]
        public void Validar_StateAdulterado_DeveLancarException()
        {
            var state = _stateProtector.Proteger("acme");
            // Tira o primeiro caractere — invalida o ciphertext do DataProtection.
            var adulterado = state[1..] + "X";
            var act = () => _stateProtector.Validar(adulterado, "acme");
            act.Should().Throw<OAuthStateException>().WithMessage("*invalido*");
        }

        [Fact]
        public void Validar_StateDeOutroTenant_DeveLancarException()
        {
            var state = _stateProtector.Proteger("acme");
            var act = () => _stateProtector.Validar(state, "outro-tenant");
            act.Should().Throw<OAuthStateException>().WithMessage("*tenant*");
        }

        [Fact]
        public void Validar_StateCifradoComOutraChave_DeveLancarException()
        {
            // Outro provider = outras chaves = outro state space.
            var outroProtector = new OAuthStateProtector(
                new DataProtectionTokenProtector(new EphemeralDataProtectionProvider()));
            var stateOutro = outroProtector.Proteger("acme");

            var act = () => _stateProtector.Validar(stateOutro, "acme");
            act.Should().Throw<OAuthStateException>().WithMessage("*invalido*");
        }

        [Fact]
        public void Proteger_DuasChamadasComMesmoTenant_GeramStatesDiferentes()
        {
            // Nonce + timestamp garantem que cada Proteger() produz output unico
            // mesmo pro mesmo tenant — propriedade necessaria contra replay.
            var s1 = _stateProtector.Proteger("acme");
            var s2 = _stateProtector.Proteger("acme");
            s1.Should().NotBe(s2);
        }

        [Fact]
        public void TokenProtector_RoundTrip_DevePreservarString()
        {
            const string plain = "meu-access-token-secreto-abc123";
            var cipher = _tokenProtector.Protect(plain);
            cipher.Should().NotBe(plain);
            _tokenProtector.Unprotect(cipher).Should().Be(plain);
        }
    }
}
