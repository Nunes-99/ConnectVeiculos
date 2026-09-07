using ConnectVeiculos.Infrastructure.Services.Auth;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    public class TentativasLoginServiceTests
    {
        private static TentativasLoginService Criar() =>
            new TentativasLoginService(
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<TentativasLoginService>.Instance);

        private const string Email = "vendedor@loja.com.br";

        [Fact]
        public void ContaNova_NaoDeveEstarBloqueada()
        {
            Criar().SegundosBloqueioRestantes(Email).Should().BeNull();
        }

        [Fact]
        public void QuatroFalhas_AindaNaoBloqueiam()
        {
            var s = Criar();
            for (var i = 0; i < 4; i++) s.RegistrarFalha(Email);

            // Quem so errou a senha algumas vezes continua podendo tentar.
            s.SegundosBloqueioRestantes(Email).Should().BeNull();
        }

        [Fact]
        public void QuintaFalha_DeveBloquearPorQuinzeMinutos()
        {
            var s = Criar();
            for (var i = 0; i < 5; i++) s.RegistrarFalha(Email);

            var restante = s.SegundosBloqueioRestantes(Email);
            restante.Should().NotBeNull();
            restante!.Value.Should().BeInRange(14 * 60, 15 * 60);
        }

        [Fact]
        public void LoginBemSucedido_DeveLiberarAConta()
        {
            var s = Criar();
            for (var i = 0; i < 5; i++) s.RegistrarFalha(Email);
            s.SegundosBloqueioRestantes(Email).Should().NotBeNull();

            s.LimparFalhas(Email);

            s.SegundosBloqueioRestantes(Email).Should().BeNull();
        }

        [Fact]
        public void BloqueioDeUmaConta_NaoAfetaOutra()
        {
            // O ponto do #23: o colega na mesma rede (mesmo IP) segue entrando.
            var s = Criar();
            for (var i = 0; i < 5; i++) s.RegistrarFalha(Email);

            s.SegundosBloqueioRestantes(Email).Should().NotBeNull();
            s.SegundosBloqueioRestantes("gerente@loja.com.br").Should().BeNull();
        }

        [Theory]
        [InlineData("  VENDEDOR@Loja.com.BR  ")]
        [InlineData("Vendedor@Loja.Com.Br")]
        public void DeveNormalizarEmail_NaoDaPraDriblarTrocandoCaixa(string variacao)
        {
            var s = Criar();
            for (var i = 0; i < 5; i++) s.RegistrarFalha(Email);

            s.SegundosBloqueioRestantes(variacao).Should().NotBeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EmailVazio_NaoDeveQuebrar(string email)
        {
            var s = Criar();
            s.RegistrarFalha(email);
            s.LimparFalhas(email);
            s.SegundosBloqueioRestantes(email).Should().BeNull();
        }
    }
}
