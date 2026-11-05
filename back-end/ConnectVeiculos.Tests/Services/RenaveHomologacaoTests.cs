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
            public Task<IReadOnlyList<VeiculoRenave>> GetTodosAsync() => Task.FromResult<IReadOnlyList<VeiculoRenave>>(Itens.Values.ToList());
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

            // Termo de entrada: so depois da assinatura do vendedor.
            var (termo, numeroTermo) = await service.ObterTermoAsync(1, saida: false);
            _saida.WriteLine($"Termo de entrada {numeroTermo}: {termo.Length} bytes");
            termo.Length.Should().BeGreaterThan(100);

            s = await service.EnviarNotaFiscalAsync(1, "COMPRA", "35261111222333000181550010000001231000001234");
            s.ChaveNfEntrada.Should().NotBeNull();

            s = await service.SimularTransferenciaDetranAsync(1);
            _saida.WriteLine($"Apos Detran: {s.Estado} -> {s.ProximoPasso}");
            s.Estado.Should().Be("CONFIRMADO");

            var municipios = await service.BuscarMunicipiosAsync("SP", "SALTO");
            municipios.Should().Contain(m => m.Nome == "SALTO");
        }

        /// <summary>
        /// Zero km da montadora (pendente -> entrada -> termo -> saida), consulta,
        /// entregas e a importacao do estoque, tudo contra o Serpro de verdade:
        ///   RENAVE_HOMOLOGACAO=1 dotnet test --filter RenaveHomologacaoTests
        /// </summary>
        [Fact]
        public async Task Zero_Km_Consulta_Entregas_E_Importacao_Na_Homologacao_Do_Serpro()
        {
            if (Environment.GetEnvironmentVariable("RENAVE_HOMOLOGACAO") != "1") return;

            var lista = new List<Veiculo>();
            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(v => v.GetAllAsync()).ReturnsAsync(() => lista.ToList());
            veiculos.Setup(v => v.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => lista.FirstOrDefault(x => x.VeiId == id)!);
            veiculos.Setup(v => v.CreateAsync(It.IsAny<Veiculo>())).ReturnsAsync((Veiculo v) =>
            {
                var id = lista.Count + 1;
                lista.Add(new Veiculo(id, 1, 1, v.VeiMarca, v.VeiModelo, v.VeiAno, v.VeiPlaca, v.VeiChassi, v.VeiCor, v.VeiKm,
                    v.VeiPreco, v.VeiDtEntrada, v.VeiSts, v.VeiSitSts, v.VeiPrecoCompra, veiRenavam: v.VeiRenavam));
                return id;
            });
            var lojas = new Mock<ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas.ILojaRepository>();
            var loja = (ConnectVeiculos.Core.Entities.Lojas.Loja)Activator.CreateInstance(typeof(ConnectVeiculos.Core.Entities.Lojas.Loja), true)!;
            typeof(ConnectVeiculos.Core.Entities.Lojas.Loja).GetProperty("LojId")!.SetValue(loja, 1);
            lojas.Setup(l => l.GetAllAsync()).ReturnsAsync(new[] { loja });
            var categorias = new Mock<ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias.ICategoriaRepository>();
            var cat = (ConnectVeiculos.Core.Entities.Categorias.Categoria)Activator.CreateInstance(typeof(ConnectVeiculos.Core.Entities.Categorias.Categoria), true)!;
            typeof(ConnectVeiculos.Core.Entities.Categorias.Categoria).GetProperty("CatId")!.SetValue(cat, 1);
            categorias.Setup(c => c.GetAllAsync()).ReturnsAsync(new[] { cat });

            var protector = new Mock<ITokenProtector>();
            var config = new Config();
            var registros = new Registros();
            var service = new RenaveService(new RenaveTransporte(), Options.Create(new RenaveSettings()), config, registros,
                veiculos.Object, new CertificadoDigitalService(config, protector.Object, NullLogger<CertificadoDigitalService>.Instance),
                NullLogger<RenaveService>.Instance, lojas.Object, categorias.Object);
            await service.SalvarConfigAsync(new RenaveConfigInput
            {
                Ambiente = RenaveAmbiente.Homologacao, OperadorCpf = "52998224725", OperadorNome = "Teste", EmailLoja = "loja@exemplo.com"
            });

            // 1. A montadora "manda" um zero km para a loja.
            var zero = await service.CriarZeroKmTesteAsync(acabado: true);
            _saida.WriteLine($"Zero km de teste: {zero.Chassi}");

            var pendentes = await service.ListarZeroKmPendentesAsync();
            _saida.WriteLine($"Pendentes: {pendentes.Count}");
            pendentes.Should().Contain(p => p.Chassi == ConnectVeiculos.Infrastructure.Services.Renave.RenaveService.NormalizarChassi(zero.Chassi));

            // 2. Entrada no estoque: cadastra o veiculo so depois do Renave aceitar.
            var s = await service.EntradaZeroKmAsync(new RenaveEntradaZeroKmInput
            {
                Chassi = zero.Chassi, DataEntrada = DateTime.Today, ValorCompra = 98000m, QuilometragemHodometro = 3,
                ChaveNotaFiscal = "35261111222333000181550010000001231000001234"
            });
            _saida.WriteLine($"Entrada zero km: estoque {s.IdEstoque}, estado {s.Estado}, veiculo {s.VeiculoId} -> {s.ProximoPasso}");
            s.IdEstoque.Should().NotBeNull();
            s.ZeroKm.Should().BeTrue();
            lista.Should().ContainSingle(v => v.VeiSts == "R");

            // Termo so existe para usado: para zero km a tela nem oferece.
            var termoZeroKm = () => service.ObterTermoAsync(s.VeiculoId, saida: false);
            await termoZeroKm.Should().ThrowAsync<RenaveException>().WithMessage("Zero km não tem termo*");

            // 3. Consulta pelo chassi enxerga o estoque.
            var consulta = await service.ConsultarVeiculoAsync(null, zero.Chassi, null);
            _saida.WriteLine($"Consulta: acabado={consulta.Acabado}, estoques={consulta.Estoques.Count}, restricoes={string.Join(",", consulta.Restricoes)}");
            consulta.VeiculoId.Should().Be(s.VeiculoId);

            // 4. Importar nao duplica o que ja esta ligado.
            // A loja de teste da homologacao e' compartilhada por todos os
            // integradores e tem estoque enorme: em 04/11/2026 a lista estourou o
            // tempo e o servico ficou fora (503) por alguns minutos. So roda pedindo.
            if (Environment.GetEnvironmentVariable("RENAVE_TESTE_IMPORTAR") == "1")
            try
            {
                var imp = await service.ImportarEstoqueAsync();
                _saida.WriteLine($"Importacao: {imp.Encontrados} no Renave, {imp.Vinculados} vinculados, {imp.Criados} criados, {imp.JaVinculados} ja ligados, {imp.Ignorados} ignorados, so3meses={imp.SoUltimosTresMeses}");
                imp.Itens.Should().Contain(i => i.IdEstoque == s.IdEstoque && i.Acao == RenaveAcaoImportacao.JaVinculado);
            }
            catch (RenaveException ex) when (ex.StatusHttp is null or >= 500)
            {
                _saida.WriteLine($"Importacao nao concluiu na homologacao (estoque compartilhado grande demais): {ex.Message}");
            }

            // 5. Saida (venda) do zero km.
            var municipio = (await service.BuscarMunicipiosAsync("SP", "SALTO")).First(m => m.Nome == "SALTO");
            s = await service.SolicitarSaidaAsync(s.VeiculoId, new RenaveSaidaInput
            {
                DocumentoComprador = "52998224725", NomeComprador = "Comprador de Teste", EmailComprador = "comprador@exemplo.com",
                CodigoMunicipio = municipio.Id, Cep = "13320000", Logradouro = "Rua Teste", Numero = "1", Bairro = "Centro",
                DataVenda = DateTime.Today, ValorVenda = 120000m,
                ChaveNotaFiscal = "35261111222333000181550010000001231000001234"
            });
            _saida.WriteLine($"Saida zero km: estado {s.Estado} -> {s.ProximoPasso}");
            s.DataSaida.Should().NotBeNull();


            // 6. Entregas indicadas pela montadora (na homologacao a lista pode vir vazia).
            var entregas = await service.ListarEntregasAsync(null);
            _saida.WriteLine($"Entregas: {entregas.Count} ({string.Join(", ", entregas.Take(5).Select(e => $"{e.Id}:{e.Estado}"))})");

            // 7. Outro zero km, rejeitado.
            var outro = await service.CriarZeroKmTesteAsync(acabado: true);
            await service.RejeitarZeroKmAsync(new RenaveRejeicaoZeroKmInput { Chassi = outro.Chassi, Motivo = "DESISTENCIA" });
            (await service.ListarZeroKmPendentesAsync()).Should().NotContain(p => p.Chassi == outro.Chassi);
            _saida.WriteLine($"Rejeitado: {outro.Chassi}");
        }
    }
}
