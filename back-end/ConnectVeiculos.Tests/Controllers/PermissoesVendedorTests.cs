using System.Reflection;
using System.Security.Claims;
using ConnectVeiculos.API.Controllers;
using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Application.ViewModels.Veiculos;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Controllers
{
    /// <summary>
    /// Perfil Vendedor (decidido em 04/11/2026): nao inativa veiculo, nao edita
    /// nem estorna venda e nao ve o preco de compra (que entrega o lucro).
    /// </summary>
    public class PermissoesVendedorTests
    {
        private static VeiculosController Controller(string papel)
        {
            var c = new VeiculosController();
            c.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, papel) }, "teste"))
                }
            };
            return c;
        }

        private static Mock<IConsultarVeiculoPorIdUseCase> PorId() =>
            new Mock<IConsultarVeiculoPorIdUseCase>().Also(m =>
                m.Setup(u => u.Execute(7)).ReturnsAsync(() => new VeiculoViewModel { VeiId = 7, VeiPreco = 90000m, VeiPrecoCompra = 70000m }));

        [Theory]
        [InlineData("Vendedor", 0)]
        [InlineData("Gerente", 70000)]
        [InlineData("Administrador", 70000)]
        public async Task Preco_de_compra_so_aparece_para_quem_nao_e_vendedor(string papel, decimal esperado)
        {
            var r = await Controller(papel).ConsultarVeiculoPorId(PorId().Object, 7) as OkObjectResult;

            ((VeiculoViewModel)r!.Value!).VeiPrecoCompra.Should().Be(esperado);
        }

        [Fact]
        public async Task Lista_tambem_vem_sem_custo_para_o_vendedor()
        {
            var lista = new Mock<IConsultarVeiculosUseCase>();
            lista.Setup(u => u.Execute(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<VeiculoViewModel> { new() { VeiId = 1, VeiPrecoCompra = 50000m }, new() { VeiId = 2, VeiPrecoCompra = 60000m } });

            var r = await Controller("Vendedor").ConsultarVeiculos(lista.Object) as OkObjectResult;

            ((IEnumerable<VeiculoViewModel>)r!.Value!).Should().OnlyContain(v => v.VeiPrecoCompra == 0);
        }

        [Fact]
        public async Task Vendedor_salvando_o_veiculo_nao_apaga_o_preco_de_compra()
        {
            var repo = new Mock<IVeiculoRepository>();
            repo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(new Veiculo(7, 1, 1, "Toyota", "Corolla", 2020, "ABC1D23", "", "Prata",
                45000, 90000m, DateTime.Now, "D", "A", 70000m));
            VeiculoInputModel? enviado = null;
            var atualizar = new Mock<IAtualizarVeiculoUseCase>();
            atualizar.Setup(u => u.Execute(It.IsAny<VeiculoInputModel>())).Callback<VeiculoInputModel>(m => enviado = m).Returns(Task.CompletedTask);

            await Controller("Vendedor").AtualizarVeiculo(atualizar.Object, repo.Object, 7, new VeiculoInputModel { VeiPreco = 95000m, VeiPrecoCompra = 0 });

            enviado!.VeiPrecoCompra.Should().Be(70000m, "o vendedor recebeu 0 e não pode sobrescrever o custo");
            enviado.VeiPreco.Should().Be(95000m);
        }

        [Fact]
        public async Task Gerente_continua_alterando_o_preco_de_compra()
        {
            var repo = new Mock<IVeiculoRepository>();
            VeiculoInputModel? enviado = null;
            var atualizar = new Mock<IAtualizarVeiculoUseCase>();
            atualizar.Setup(u => u.Execute(It.IsAny<VeiculoInputModel>())).Callback<VeiculoInputModel>(m => enviado = m).Returns(Task.CompletedTask);

            await Controller("Gerente").AtualizarVeiculo(atualizar.Object, repo.Object, 7, new VeiculoInputModel { VeiPrecoCompra = 72000m });

            enviado!.VeiPrecoCompra.Should().Be(72000m);
            repo.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Theory]
        [InlineData(typeof(VeiculosController), nameof(VeiculosController.InativarVeiculo))]
        [InlineData(typeof(VendasController), nameof(VendasController.AtualizarVenda))]
        [InlineData(typeof(VendasController), nameof(VendasController.EstornarVenda))]
        public void Inativar_veiculo_editar_e_estornar_venda_so_para_gestao(Type controller, string acao)
        {
            var papeis = controller.GetMethod(acao)!.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).ToList();

            papeis.Should().ContainSingle().Which.Should().Be("Administrador,Gerente");
        }
    }

    internal static class MockExtensoes
    {
        public static T Also<T>(this T obj, Action<T> acao) { acao(obj); return obj; }
    }
}
