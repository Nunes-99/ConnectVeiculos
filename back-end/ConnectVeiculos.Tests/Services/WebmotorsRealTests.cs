using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Core.Webmotors;
using ConnectVeiculos.Infrastructure.Services.Webmotors;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// Teste real contra a homologacao da Webmotors. So o IP do servidor esta
    /// liberado, entao roda no PC com o trafego saindo pela VM (tunel SSH):
    ///
    ///   ssh -N -D 1080 -i ~/.ssh/connectveiculos-oracle.key ubuntu@136.248.77.154 &amp;
    ///   HTTPS_PROXY=socks5://127.0.0.1:1080 WEBMOTORS_TESTE_REAL=1 \
    ///     WEBMOTORS_CNPJ=... WEBMOTORS_EMAIL=... WEBMOTORS_SENHA=... \
    ///     dotnet test ConnectVeiculos.Tests --filter WebmotorsRealTests
    ///
    /// Sem mais nada: login, modalidades, tabelas e o palpite de codigos.
    /// Com WEBMOTORS_TESTE_ANUNCIAR=1 tambem inclui um anuncio de teste com
    /// foto, altera o preco e exclui (motivo "vendi na loja").
    /// </summary>
    public class WebmotorsRealTests
    {
        private readonly ITestOutputHelper _saida;
        public WebmotorsRealTests(ITestOutputHelper saida) => _saida = saida;

        private sealed class Config : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> V = new();
            public Task<string> GetValorAsync(string c) => Task.FromResult(V.TryGetValue(c, out var v) ? v : null!);
            public Task SetValorAsync(string c, string v) { V[c] = v; return Task.CompletedTask; }
        }

        private sealed class SemCifra : ITokenProtector
        {
            public string Protect(string s) => s;
            public string Unprotect(string s) => s;
        }

        private sealed class Anuncios : IVeiculoWebmotorsRepository
        {
            public readonly List<VeiculoWebmotors> Itens = new();
            public Task<VeiculoWebmotors?> GetByVeiculoIdAsync(int id) => Task.FromResult(Itens.FirstOrDefault(a => a.R_VeiId == id));
            public Task<IReadOnlyList<VeiculoWebmotors>> ListarPublicadosAsync() => Task.FromResult<IReadOnlyList<VeiculoWebmotors>>(Itens);
            public Task SalvarAsync(VeiculoWebmotors a) { if (!Itens.Contains(a)) Itens.Add(a); return Task.CompletedTask; }
        }

        private static string? Env(string nome) => Environment.GetEnvironmentVariable(nome) is { Length: > 0 } v ? v.Trim() : null;

        [Fact]
        public async Task Fluxo_Real_Na_Homologacao()
        {
            if (Env("WEBMOTORS_TESTE_REAL") != "1") return;

            var settings = new WebmotorsSettings();
            if (Env("WEBMOTORS_BASE_URL") is { } baseUrl) settings.BaseUrl = baseUrl;

            var placa = "CVT" + Random.Shared.Next(1, 9) + (char)('A' + Random.Shared.Next(26)) + Random.Shared.Next(10, 99);
            var veiculo = new Veiculo(9001, 1, 1, Env("WEBMOTORS_MARCA") ?? "VW - VolksWagen",
                Env("WEBMOTORS_MODELO") ?? "GOL 1.0 Mi Total Flex 8V 4p", short.Parse(Env("WEBMOTORS_ANO") ?? "2012"),
                placa, "", Env("WEBMOTORS_COR") ?? "Prata", 98000, 31900m, DateTime.Today, "D", "", 0m,
                "Anúncio de TESTE da integração ConnectVeiculos (homologação). Ignorar.", veiOpcionais: "Ar condicionado,Alarme,Trava elétrica");

            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(x => x.GetByIdAsync(9001)).ReturnsAsync(() => veiculo);
            var imagens = new Mock<IVeiculoImagemRepository>();
            imagens.Setup(x => x.GetByVeiculoIdAsync(9001)).ReturnsAsync((IEnumerable<VeiculoImagem>)new List<VeiculoImagem>
            {
                // Foto publica do catalogo (a Webmotors baixa pela URL).
                new(1, 9001, Env("WEBMOTORS_FOTO") ?? "/uploads/veiculos/1/185277a5-8a8d-422b-a4f2-d095823d716c.jpg", 1, true)
            });
            var tenant = new Mock<ITenantContext>();
            tenant.SetupGet(x => x.TenantSlug).Returns("teste-real");
            var anuncios = new Anuncios();

            var service = new WebmotorsService(new HttpClient { Timeout = TimeSpan.FromSeconds(90) }, Options.Create(settings), new Config(),
                new SemCifra(), tenant.Object, veiculos.Object, imagens.Object, new Mock<ILojaRepository>().Object, anuncios,
                NullLogger<WebmotorsService>.Instance);

            // 1. Conta
            var status = await service.ConectarAsync(new WebmotorsConectarInput
            {
                Cnpj = Env("WEBMOTORS_CNPJ")!, Email = Env("WEBMOTORS_EMAIL")!, Senha = Env("WEBMOTORS_SENHA")!
            });
            status.Conectado.Should().BeTrue();
            status.Erro.Should().BeNull();
            _saida.WriteLine($"Ambiente: {(status.Homologacao ? "homologação" : "PRODUÇÃO")}; modalidade escolhida {status.ModalidadeId}");
            foreach (var m in status.Modalidades) _saida.WriteLine($"  {m.Codigo} {m.Nome} ({m.TipoAnuncio}) {m.EmUso}/{m.Limite}");

            // 2. Palpite a partir do cadastro (como a tela faz)
            var info = await service.ObterVeiculoAsync(9001);
            info.Sugestao.Should().BeTrue();
            var d = info.Dados;
            async Task<string> Nome(string tabela, long? id) => (await service.TabelaAsync(tabela)).FirstOrDefault(i => i.Codigo == id)?.Nome ?? "—";
            var marca = await Nome("marcas", d.MarcaId);
            var modelo = d.MarcaId is { } mid ? (await service.ModelosAsync(mid)).FirstOrDefault(m => m.Codigo == d.ModeloId)?.Nome : null;
            var versao = d.ModeloId is { } moid ? (await service.VersoesAsync(moid, d.AnoModelo)).FirstOrDefault(v => v.Codigo == d.VersaoId)?.Nome : null;
            _saida.WriteLine($"{veiculo.VeiMarca} / {veiculo.VeiModelo} {veiculo.VeiAno} {veiculo.VeiCor}");
            _saida.WriteLine($"  → {marca} / {modelo} / {versao}");
            _saida.WriteLine($"  câmbio {await Nome("cambios", d.CambioId)}, combustível {await Nome("combustiveis", d.CombustivelId)}, " +
                             $"cor {await Nome("cores", d.CorId)}, portas {d.Portas}, opcionais [{string.Join(", ", d.Opcionais)}]");
            d.MarcaId.Should().NotBeNull();
            d.ModeloId.Should().NotBeNull();
            d.VersaoId.Should().NotBeNull();

            if (Env("WEBMOTORS_TESTE_ANUNCIAR") != "1")
            {
                _saida.WriteLine("Anúncio não executado (ligue WEBMOTORS_TESTE_ANUNCIAR=1).");
                return;
            }

            // 3. Incluir com foto
            d.Licenciado = true;
            d.IpvaPago = true;
            info = await service.PublicarAsync(9001, d);
            _saida.WriteLine($"Incluído: anúncio {info.CodigoAnuncio}, placa {placa}, fotos {info.Fotos}, erro: {info.Erro ?? "nenhum"}");
            info.Publicado.Should().BeTrue();
            info.Erro.Should().BeNull();
            info.Fotos.Should().Be(1);

            // 4. Alterar o preco (como ao editar o veiculo)
            veiculo.SetProperties(9001, 1, 1, veiculo.VeiMarca, veiculo.VeiModelo, veiculo.VeiAno, placa, "", veiculo.VeiCor,
                98500, 30900m, DateTime.Today, "D", "", 0m, veiculo.VeiObservacao, null, null, veiculo.VeiOpcionais, null, null);
            await service.SincronizarVeiculoAsync(9001);
            var anuncio = anuncios.Itens.Single();
            _saida.WriteLine($"Alterado: status {anuncio.VwmStatus}, erro: {anuncio.VwmErro ?? "nenhum"}");
            anuncio.VwmErro.Should().BeNull();

            // 5. Vender: exclui com motivo "vendi na loja"
            veiculo.SetProperties(9001, 1, 1, veiculo.VeiMarca, veiculo.VeiModelo, veiculo.VeiAno, placa, "", veiculo.VeiCor,
                98500, 30900m, DateTime.Today, "V", "", 0m, veiculo.VeiObservacao, null, null, veiculo.VeiOpcionais, null, null);
            await service.SincronizarVeiculoAsync(9001);
            _saida.WriteLine($"Após venda: status {anuncio.VwmStatus}, erro: {anuncio.VwmErro ?? "nenhum"}");
            anuncio.VwmStatus.Should().Be(VeiculoWebmotors.StatusRemovido);
        }
    }
}
