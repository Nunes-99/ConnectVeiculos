using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Moq;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// O state so provava "foi gerado para esta loja": um admin de outra loja
    /// mandava o link de conexao para o dono de uma Page ou conta do ML, e quem
    /// aprovasse ligava a propria conta na loja do atacante. Agora o state so
    /// vale no navegador que recebeu o cookie ao pedir a conexao.
    /// </summary>
    public class OAuthStateNavegadorTests
    {
        private readonly ITokenProtector _tokenProtector =
            new DataProtectionTokenProtector(new EphemeralDataProtectionProvider());

        private (OAuthStateProtector protector, Mock<IHttpContextAccessor> accessor) Criar()
        {
            var accessor = new Mock<IHttpContextAccessor>();
            return (new OAuthStateProtector(_tokenProtector, accessor.Object), accessor);
        }

        private static string CookieGravado(HttpContext ctx)
            => ctx.Response.Headers.SetCookie.ToString().Split(';')[0];

        [Fact]
        public void Mesmo_navegador_que_pediu_a_conexao_passa()
        {
            var (protector, accessor) = Criar();
            var pedido = new DefaultHttpContext();
            accessor.SetupGet(a => a.HttpContext).Returns(pedido);
            var state = protector.Proteger("acme");

            var retorno = new DefaultHttpContext();
            retorno.Request.Headers.Cookie = CookieGravado(pedido);
            accessor.SetupGet(a => a.HttpContext).Returns(retorno);

            protector.Validar(state, "acme").TenantSlug.Should().Be("acme");
        }

        [Fact]
        public void Link_aberto_em_outro_navegador_e_recusado()
        {
            var (protector, accessor) = Criar();
            accessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext());
            var state = protector.Proteger("acme");

            // Vitima abre o link: o navegador dela nao tem o cookie.
            accessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext());

            var act = () => protector.Validar(state, "acme");
            act.Should().Throw<OAuthStateException>().WithMessage("*navegador*");
        }

        [Fact]
        public void Cookie_e_de_uso_unico()
        {
            var (protector, accessor) = Criar();
            var pedido = new DefaultHttpContext();
            accessor.SetupGet(a => a.HttpContext).Returns(pedido);
            var state = protector.Proteger("acme");

            var retorno = new DefaultHttpContext();
            retorno.Request.Headers.Cookie = CookieGravado(pedido);
            accessor.SetupGet(a => a.HttpContext).Returns(retorno);
            protector.Validar(state, "acme");

            retorno.Response.Headers.SetCookie.ToString().Should().Contain("expires=Thu, 01 Jan 1970");
        }
    }
}
