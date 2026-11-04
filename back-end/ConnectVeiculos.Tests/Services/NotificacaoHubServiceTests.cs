using ConnectVeiculos.Core.Entities.Notificacoes;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Notificacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Hubs;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// A notificacao abre um escopo novo para gravar; ele tem de apontar para o
    /// banco da mesma loja. Ja aconteceu de cair no banco padrao: o aviso de
    /// "novo veiculo" de uma loja ia para os usuarios de outra.
    /// </summary>
    public class NotificacaoHubServiceTests
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

        [Fact]
        public async Task Grava_A_Notificacao_No_Banco_Da_Mesma_Loja()
        {
            var vistos = new List<string>();
            var services = new ServiceCollection();
            services.AddScoped<ITenantContext, Tenant>();
            services.AddScoped(sp =>
            {
                var t = sp.GetRequiredService<ITenantContext>();
                var m = new Mock<IUsuarioRepository>();
                m.Setup(r => r.GetAllAsync()).ReturnsAsync(() =>
                {
                    vistos.Add(t.IsResolved ? t.DatabaseFile : "(sem loja)");
                    return new List<Usuario> { new Usuario() };
                });
                return m.Object;
            });
            services.AddScoped(sp =>
            {
                var t = sp.GetRequiredService<ITenantContext>();
                var m = new Mock<INotificacaoRepository>();
                m.Setup(r => r.AddAsync(It.IsAny<Notificacao>())).ReturnsAsync((Notificacao n) =>
                {
                    vistos.Add(t.IsResolved ? t.DatabaseFile : "(sem loja)");
                    return n;
                });
                return m.Object;
            });
            var provider = services.BuildServiceProvider();

            var hub = new Mock<IHubContext<NotificacaoHub>>();
            var clientes = new Mock<IHubClients>();
            clientes.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
            hub.SetupGet(h => h.Clients).Returns(clientes.Object);

            using var requisicao = provider.CreateScope();
            var tenant = requisicao.ServiceProvider.GetRequiredService<ITenantContext>();
            tenant.Resolve(7, "diamante", "diamante.db");
            var servico = new NotificacaoHubService(hub.Object, provider.GetRequiredService<IServiceScopeFactory>(), tenant);

            await servico.EnviarParaTodosAsync("NOVO_VEICULO", new { marca = "Fiat", modelo = "Argo", ano = 2023 });
            await servico.EnviarParaUsuarioAsync(1, "NOVO_LEAD", new { nome = "Cliente" });

            vistos.Should().NotBeEmpty().And.OnlyContain(v => v == "diamante.db");
        }
    }
}
