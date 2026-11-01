using ConnectVeiculos.Core.Entities.Renave;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Renave;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Renave;
using ConnectVeiculos.Infrastructure.Services.Certificados;
using ConnectVeiculos.Infrastructure.Services.Renave;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// Fluxo real contra a homologacao do Renave-WS (Serpro), com o "cliente
    /// padrao" que dispensa certificado. Depende de rede e do Serpro no ar, por
    /// isso so roda com RENAVE_HOMOLOGACAO=1:
    ///   RENAVE_HOMOLOGACAO=1 dotnet test --filter RenaveHomologacaoTests
    /// </summary>
    public class RenaveHomologacaoTests
    {
        private readonly ITestOutputHelper _saida;
        public RenaveHomologacaoTests(ITestOutputHelper saida) => _saida = saida;

        private sealed class Config : IConfiguracaoSistemaRepository
        {
            private readonly Dictionary<string, string> _v = new();
            public Task<string> GetValorAsync(string c) => Task.FromResult(_v.TryGetValue(c, out var v) ? v : null!);
            public Task SetValorAsync(string c, string v) { _v[c] = v; return Task.CompletedTask; }
        }

        private sealed class Registros : IVeiculoRenaveRepository
        {
            public readonly Dictionary<int, VeiculoRenave> Itens = new();
            public Task<VeiculoRenave?> GetByVeiculoAsync(int id) => Task.FromResult(Itens.TryGetValue(id, out var r) ? r : null);
            public Task<IReadOnlyList<VeiculoRenave>> GetByVeiculosAsync(IEnumerable<int> ids)
                => Task.FromResult<IReadOnlyList<VeiculoRenave>>(ids.Where(Itens.ContainsKey).Select(i => Itens[i]).ToList());
            public Task SalvarAsync(VeiculoRenave r) { Itens[r.R_VeiId] = r; return Task.CompletedTask; }
        }

        [Fact]
        public async Task Fluxo_De_Entrada_Na_Homologacao_Do_Serpro()
        {
            if (Environment.GetEnvironmentVariable("RENAVE_HOMOLOGACAO") != "1") return;

            var veiculos = new Mock<IVeiculoRepository>();
            var protector = new Mock<ITokenProtector>();
            var config = new Config();
            var service = new RenaveService(new RenaveTransporte(), Options.Create(new RenaveSettings()), config,
                new Registros(), veiculos.Object, new CertificadoDigitalService(config, protector.Object, NullLogger<CertificadoDigitalService>.Instance), NullLogger<RenaveService>.Instance);

            await service.SalvarConfigAsync(new RenaveConfigInput
            {
                Ambiente = RenaveAmbiente.Homologacao, OperadorCpf = "52998224725", OperadorNome = "Teste", EmailLoja = "loja@exemplo.com"
            });

            var conexao = await service.TestarConexaoAsync();
            _saida.WriteLine($"Conectado como {conexao.Nome} ({conexao.Cnpj})");
            conexao.Cnpj.Should().Be("85106037000585");

            var mock = await service.CriarVeiculoTesteAsync();
            _saida.WriteLine($"Veiculo de teste {mock.Placa} / {mock.Renavam}");
            veiculos.Setup(v => v.GetByIdAsync(1)).ReturnsAsync(new Veiculo(
                1, 1, 1, "Teste", "Renave", 2020, mock.Placa, mock.Chassi, "Prata", 45000, 60000m,
                DateTime.Now, "D", "A", 50000m, veiRenavam: mock.Renavam));

            var aptidao = await service.ConsultarAptidaoAsync(1, new RenaveCrvInput { TipoCrv = "VERDE", NumeroCrv = mock.NumeroCrv });
            aptidao.Apto.Should().BeTrue(string.Join("; ", aptidao.Motivos));

            var s = await service.SolicitarEntradaAsync(1, new RenaveEntradaInput
            {
                TipoCrv = "VERDE", NumeroCrv = mock.NumeroCrv, CodigoSegurancaCrv = mock.CodigoSegurancaCrv,
                DocumentoVendedor = mock.DocumentoProprietario, EmailVendedor = "vendedor@exemplo.com",
                DataCompra = DateTime.Today, ValorCompra = 50000m, QuilometragemHodometro = 45000
            });
            _saida.WriteLine($"Entrada: estoque {s.IdEstoque}, estado {s.Estado}");
            s.IdEstoque.Should().NotBeNull();

            s = await service.EnviarAssinaturaVendedorAsync(1, RenaveTipoAssinatura.CrvPapel, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
            s.DataAssinaturaVendedor.Should().NotBeNull();

            s = await service.EnviarNotaFiscalAsync(1, "COMPRA", "35261111222333000181550010000001231000001234");
            s.ChaveNfEntrada.Should().NotBeNull();

            s = await service.SimularTransferenciaDetranAsync(1);
            _saida.WriteLine($"Apos Detran: {s.Estado} -> {s.ProximoPasso}");
            s.Estado.Should().Be("CONFIRMADO");

            var municipios = await service.BuscarMunicipiosAsync("SP", "SALTO");
            municipios.Should().Contain(m => m.Nome == "SALTO");
        }
    }
}
