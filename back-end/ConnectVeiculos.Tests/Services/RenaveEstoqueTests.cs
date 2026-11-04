using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Renave;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
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

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// Importacao do estoque, zero km, entregas, consulta e termos, com um
    /// Renave falso que responde por rota (formato do swagger do Renave-WS).
    /// </summary>
    public class RenaveEstoqueTests
    {
        private const string CpfOperador = "52998224725";

        private sealed class RenaveFalso : IRenaveTransporte
        {
            public readonly List<(string Metodo, string Url, string? Corpo)> Chamadas = new();
            /// <summary>Resposta pelo inicio do caminho (sem a URL base); a mais longa que casar vence.</summary>
            public readonly Dictionary<string, (HttpStatusCode Status, string Corpo)> Rotas = new();

            public async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage request, X509Certificate2? certificado, CancellationToken ct)
            {
                var url = request.RequestUri!.ToString();
                var caminho = url[(url.IndexOf("/api/", StringComparison.Ordinal))..];
                var corpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
                Chamadas.Add((request.Method.Method, caminho, corpo));
                var rota = Rotas.Keys.Where(k => caminho.StartsWith(k, StringComparison.Ordinal)).OrderByDescending(k => k.Length).FirstOrDefault();
                var (status, resposta) = rota == null ? (HttpStatusCode.NotFound, "{\"detalhe\":\"rota nao simulada\"}") : Rotas[rota];
                return new HttpResponseMessage(status) { Content = new StringContent(resposta, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class ConfigEmMemoria : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> Valores = new();
            public Task<string> GetValorAsync(string chave) => Task.FromResult(Valores.TryGetValue(chave, out var v) ? v : null!);
            public Task SetValorAsync(string chave, string valor) { Valores[chave] = valor; return Task.CompletedTask; }
        }

        private sealed class RegistrosEmMemoria : IVeiculoRenaveRepository
        {
            public readonly Dictionary<int, VeiculoRenave> Itens = new();
            public Task<VeiculoRenave?> GetByVeiculoAsync(int veiculoId) => Task.FromResult(Itens.TryGetValue(veiculoId, out var r) ? r : null);
            public Task<IReadOnlyList<VeiculoRenave>> GetByVeiculosAsync(IEnumerable<int> ids)
                => Task.FromResult<IReadOnlyList<VeiculoRenave>>(ids.Where(Itens.ContainsKey).Select(i => Itens[i]).ToList());
            public Task SalvarAsync(VeiculoRenave registro) { Itens[registro.R_VeiId] = registro; return Task.CompletedTask; }
            public Task<IReadOnlyList<VeiculoRenave>> GetTodosAsync() => Task.FromResult<IReadOnlyList<VeiculoRenave>>(Itens.Values.ToList());
        }

        private readonly RenaveFalso _renave = new();
        private readonly ConfigEmMemoria _config = new();
        private readonly RegistrosEmMemoria _registros = new();
        private readonly List<Veiculo> _veiculos = new();
        private readonly List<Veiculo> _criados = new();
        private readonly RenaveService _service;

        public RenaveEstoqueTests()
        {
            _veiculos.Add(NovoVeiculo(7, "ABC-1D23", "9BWZZZ377VT004251"));

            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(v => v.GetAllAsync()).ReturnsAsync(() => _veiculos.ToList());
            veiculos.Setup(v => v.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => _veiculos.FirstOrDefault(x => x.VeiId == id)!);
            veiculos.Setup(v => v.CreateAsync(It.IsAny<Veiculo>())).ReturnsAsync((Veiculo v) =>
            {
                var id = 100 + _criados.Count;
                _criados.Add(v);
                _veiculos.Add(NovoVeiculo(id, v.VeiPlaca, v.VeiChassi));
                return id;
            });

            var lojas = new Mock<ILojaRepository>();
            lojas.Setup(l => l.GetAllAsync()).ReturnsAsync(new[] { (Loja)Activator.CreateInstance(typeof(Loja), true)! }
                .Select(l => { typeof(Loja).GetProperty(nameof(Loja.LojId))!.SetValue(l, 3); return l; }).ToList());
            var categorias = new Mock<ICategoriaRepository>();
            categorias.Setup(c => c.GetAllAsync()).ReturnsAsync(new[] { (Categoria)Activator.CreateInstance(typeof(Categoria), true)! }
                .Select(c => { typeof(Categoria).GetProperty(nameof(Categoria.CatId))!.SetValue(c, 5); return c; }).ToList());

            var protector = new Mock<ITokenProtector>();
            protector.Setup(p => p.Protect(It.IsAny<string>())).Returns((string s) => "x" + s);
            protector.Setup(p => p.Unprotect(It.IsAny<string>())).Returns((string s) => s[1..]);

            _service = new RenaveService(_renave, Options.Create(new RenaveSettings()), _config, _registros, veiculos.Object,
                new CertificadoDigitalService(_config, protector.Object, NullLogger<CertificadoDigitalService>.Instance),
                NullLogger<RenaveService>.Instance, lojas.Object, categorias.Object);

            _service.SalvarConfigAsync(new RenaveConfigInput
            {
                Ambiente = RenaveAmbiente.Homologacao, OperadorCpf = CpfOperador, OperadorNome = "Operador", EmailLoja = "loja@ex.com"
            }).GetAwaiter().GetResult();
        }

        private static Veiculo NovoVeiculo(int id, string placa, string chassi)
            => new(id, 3, 5, "Toyota", "Corolla", 2020, placa, chassi, "Prata", 45000, 90000m, DateTime.Now, "D", "A", 80000m,
                veiRenavam: "123456789");

        private static string Estoque(long id, string estado, string? placa, string chassi, string? saida = null)
            => "{\"id\":" + id + ",\"estado\":\"" + estado + "\",\"placa\":" + (placa == null ? "null" : "\"" + placa + "\"")
               + ",\"chassi\":\"" + chassi + "\",\"renavam\":\"00987654321\",\"quilometragemHodometro\":12000,"
               + "\"entradaEstoque\":{\"dataHora\":\"2026-08-10T09:30:00\","
               + "\"chaveNotaFiscalEntrada\":\"35260811222333000181550010000000011000000014\","
               + "\"vendedor\":{\"numeroDocumento\":\"52998224725\",\"tipoDocumento\":\"CPF\"}}"
               + (saida == null ? "" : ",\"saidaEstoque\":" + saida) + "}";

        // ---------------- Importacao ----------------

        [Fact]
        public async Task Importar_vincula_pela_placa_cadastra_o_que_falta_e_nao_repete()
        {
            _registros.Itens[50] = new VeiculoRenave(50);
            _registros.Itens[50].RegistrarEntrada(900, "CONFIRMADO", DateTime.Today, 1m);

            _renave.Rotas["/api/estoques?estadoEstoque=SOLICITADO"] = (HttpStatusCode.OK, "[]");
            _renave.Rotas["/api/estoques?estadoEstoque=TRANSFERIDO"] = (HttpStatusCode.OK, $"[{Estoque(901, "TRANSFERIDO", "XYZ9A87", "9BRBLWHEXG0107721")}]");
            _renave.Rotas["/api/estoques?estadoEstoque=CONFIRMADO"] = (HttpStatusCode.OK,
                $"[{Estoque(900, "CONFIRMADO", "QWE1234", "CHASSIJAVINCULADO")},{Estoque(902, "CONFIRMADO", "ABC1D23", "9BWZZZ377VT004251")}," +
                $"{Estoque(903, "CONFIRMADO", null, "93HZERO0KM0000001")},{Estoque(904, "CONFIRMADO", "VND1A11", "CHASSIVENDIDO", "{\"dataHora\":\"2026-09-01T10:00:00\"}")}]");

            var r = await _service.ImportarEstoqueAsync();

            r.Encontrados.Should().Be(5);
            r.JaVinculados.Should().Be(1);
            r.Vinculados.Should().Be(1);
            r.Criados.Should().Be(2);
            r.Ignorados.Should().Be(1);
            r.SoUltimosTresMeses.Should().BeFalse();

            // Placa "ABC-1D23" do sistema casa com "ABC1D23" do Renave.
            var corolla = _registros.Itens[7];
            corolla.RnvIdEstoque.Should().Be(902);
            corolla.RnvEstado.Should().Be("CONFIRMADO");
            corolla.RnvChaveNfEntrada.Should().HaveLength(44);
            corolla.RnvDtAssinaturaVendedor.Should().NotBeNull("o Detran ja confirmou: a tela nao pede a assinatura de novo");
            RenaveService.ParaSituacao(7, corolla).ProximoPasso.Should().Contain("registrar a saída");

            // O que nao existia entra como Reservado, fora do catalogo.
            _criados.Should().HaveCount(2);
            _criados.Should().OnlyContain(v => v.VeiSts == "R" && v.R_LojId == 3 && v.R_CatId == 5);
            _criados.Single(v => v.VeiPlaca == "XYZ9A87").VeiKm.Should().Be(12000);
            var zeroKm = r.Itens.Single(i => i.IdEstoque == 903);
            zeroKm.ZeroKm.Should().BeTrue();
            _registros.Itens[zeroKm.VeiculoId!.Value].RnvZeroKm.Should().BeTrue();

            r.Itens.Single(i => i.IdEstoque == 904).Acao.Should().Be(RenaveAcaoImportacao.Ignorado);
            _renave.Chamadas.Where(c => c.Url.StartsWith("/api/estoques?")).Should()
                .OnlyContain(c => c.Url.Contains("dataHoraRegistroEntradaInicial=2020-01-01T00%3A00%3A00"));
            _renave.Chamadas.Should().OnlyContain(c => c.Metodo == "GET", "importar nao faz nenhuma entrada nova no Renave");

            // Rodar de novo nao duplica nada.
            var deNovo = await _service.ImportarEstoqueAsync();
            deNovo.Criados.Should().Be(0);
            deNovo.Vinculados.Should().Be(0);
            deNovo.JaVinculados.Should().Be(4);
        }

        [Fact]
        public async Task Importar_sem_o_periodo_longo_usa_o_padrao_do_Renave()
        {
            // Rota com data recusada; sem data, aceita.
            _renave.Rotas["/api/estoques?estadoEstoque="] = (HttpStatusCode.BadRequest, "{\"mensagemParaUsuarioFinal\":\"Período muito longo\"}");
            foreach (var e in new[] { "SOLICITADO", "TRANSFERIDO", "CONFIRMADO" })
                _renave.Rotas[$"/api/estoques?estadoEstoque={e}&"] = (HttpStatusCode.BadRequest, "{\"mensagemParaUsuarioFinal\":\"Período muito longo\"}");
            _renave.Rotas["/api/estoques?estadoEstoque=SOLICITADO"] = (HttpStatusCode.OK, "[]");
            _renave.Rotas["/api/estoques?estadoEstoque=TRANSFERIDO"] = (HttpStatusCode.OK, "[]");
            _renave.Rotas["/api/estoques?estadoEstoque=CONFIRMADO"] = (HttpStatusCode.OK, "[]");
            // A mais longa vence: as com "&" (periodo) recusam, as sem "&" aceitam.

            var r = await _service.ImportarEstoqueAsync();

            r.SoUltimosTresMeses.Should().BeTrue();
            r.Encontrados.Should().Be(0);
        }

        // ---------------- Zero km ----------------

        [Fact]
        public async Task Pendentes_de_zero_km_mostram_o_veiculo_do_mesmo_chassi()
        {
            _renave.Rotas["/api/veiculos-zero-km-pendentes-entrada-estoque"] = (HttpStatusCode.OK,
                """[{"chassi":"9bwzzz377vt004251","dataHoraCadastro":"2026-11-01T08:00:00","veiculoAcabado":true},{"chassi":"93HNOVO0000000002","veiculoAcabado":false}]""");

            var lista = await _service.ListarZeroKmPendentesAsync();

            lista.Should().HaveCount(2);
            lista[0].VeiculoId.Should().Be(7);
            lista[0].Acabado.Should().BeTrue();
            lista[0].DataHoraCadastro.Should().Be(new DateTime(2026, 11, 1, 8, 0, 0));
            lista[1].VeiculoId.Should().BeNull();
        }

        [Fact]
        public async Task Entrada_de_zero_km_envia_o_formato_do_Renave_e_so_cadastra_depois_que_ele_aceita()
        {
            _renave.Rotas["/api/entradas-estoque-zero-km"] = (HttpStatusCode.OK, """{"id":777,"estado":"CONFIRMADO"}""");

            var s = await _service.EntradaZeroKmAsync(new RenaveEntradaZeroKmInput
            {
                Chassi = "93h-novo0000000002", DataEntrada = new DateTime(2026, 11, 3), ValorCompra = 98000m, QuilometragemHodometro = 5,
                ChaveNotaFiscal = "35261111222333000181550010000001231000001234"
            });

            var corpo = JsonDocument.Parse(_renave.Chamadas.Single().Corpo!).RootElement;
            corpo.GetProperty("chassi").GetString().Should().Be("93HNOVO0000000002");
            corpo.GetProperty("dataEntradaEstoque").GetString().Should().Be("2026-11-03");
            corpo.GetProperty("valorCompra").GetDecimal().Should().Be(98000m);
            corpo.GetProperty("cpfOperadorResponsavel").GetString().Should().Be(CpfOperador);
            corpo.GetProperty("dataHoraMedicaoHodometro").GetString().Should().NotContain("Z").And.NotContain("+");
            corpo.GetProperty("chaveNotaFiscal").GetString().Should().HaveLength(44);

            _criados.Should().ContainSingle(v => v.VeiChassi == "93HNOVO0000000002" && v.VeiSts == "R" && v.VeiPrecoCompra == 98000m);
            s.ZeroKm.Should().BeTrue();
            s.IdEstoque.Should().Be(777);
            s.ProximoPasso.Should().Contain("registrar a saída");
        }

        [Fact]
        public async Task Entrada_de_zero_km_recusada_nao_deixa_veiculo_cadastrado()
        {
            _renave.Rotas["/api/entradas-estoque-zero-km"] = (HttpStatusCode.UnprocessableEntity,
                """{"mensagemParaUsuarioFinal":"Veículo não está pendente de entrada para este estabelecimento."}""");

            var acao = () => _service.EntradaZeroKmAsync(new RenaveEntradaZeroKmInput
            {
                Chassi = "93HNOVO0000000002", DataEntrada = DateTime.Today, ValorCompra = 1000m,
                ChaveNotaFiscal = "35261111222333000181550010000001231000001234"
            });

            await acao.Should().ThrowAsync<RenaveException>().WithMessage("*não está pendente*");
            _criados.Should().BeEmpty();
        }

        [Fact]
        public async Task Saida_de_zero_km_vai_pela_rota_propria_sem_CRV()
        {
            var registro = new VeiculoRenave(7);
            registro.RegistrarEntradaZeroKm(777, "CONFIRMADO", DateTime.Today, 98000m, null);
            _registros.Itens[7] = registro;
            _renave.Rotas["/api/saidas-estoque-veiculo-zero-km"] = (HttpStatusCode.OK, """{"id":777,"estado":"FINALIZADO"}""");

            var semEndereco = () => _service.SolicitarSaidaAsync(7, new RenaveSaidaInput
            {
                DocumentoComprador = "52998224725", NomeComprador = "Fulano", CodigoMunicipio = 7107,
                DataVenda = new DateTime(2026, 11, 4), ValorVenda = 120000m
            });
            await semEndereco.Should().ThrowAsync<RenaveException>().WithMessage("O Renave exige do comprador: e-mail, CEP, endereço, número, bairro.");

            RenaveSaidaInput Saida(string? chave) => new()
            {
                DocumentoComprador = "52998224725", NomeComprador = "Fulano", EmailComprador = "fulano@ex.com",
                CodigoMunicipio = 7107, Cep = "13320-000", Logradouro = "Rua A", Numero = "10", Bairro = "Centro",
                DataVenda = new DateTime(2026, 11, 4), ValorVenda = 120000m, ChaveNotaFiscal = chave
            };
            var semNota = () => _service.SolicitarSaidaAsync(7, Saida(null));
            await semNota.Should().ThrowAsync<RenaveException>().WithMessage("*NF-e de venda*");
            _renave.Chamadas.Should().BeEmpty();

            var s = await _service.SolicitarSaidaAsync(7, Saida("35261111222333000181550010000001231000001234"));

            var chamada = _renave.Chamadas.Single();
            chamada.Url.Should().Be("/api/saidas-estoque-veiculo-zero-km");
            var corpo = JsonDocument.Parse(chamada.Corpo!).RootElement;
            corpo.GetProperty("idEstoque").GetInt64().Should().Be(777);
            corpo.TryGetProperty("veiculo", out _).Should().BeFalse();
            corpo.GetProperty("comprador").GetProperty("endereco").GetProperty("codigoMunicipio").GetInt32().Should().Be(7107);
            corpo.GetProperty("chaveNotaFiscal").GetString().Should().HaveLength(44);
            s.DataSaida.Should().Be(new DateTime(2026, 11, 4));
            s.ChaveNfSaida.Should().HaveLength(44);
            s.ProximoPasso.Should().Be("Concluído");

            var atpv = () => _service.ObterAtpvAsync(7);
            await atpv.Should().ThrowAsync<RenaveException>().WithMessage("*Zero km não tem ATPVe*");
        }

        [Fact]
        public async Task Rejeicao_de_zero_km_so_aceita_os_motivos_do_Renave()
        {
            var invalida = () => _service.RejeitarZeroKmAsync(new RenaveRejeicaoZeroKmInput { Chassi = "93HNOVO0000000002", Motivo = "qualquer" });
            await invalida.Should().ThrowAsync<RenaveException>();
            _renave.Chamadas.Should().BeEmpty();

            _renave.Rotas["/api/rejeicoes-zero-km"] = (HttpStatusCode.OK, "");
            await _service.RejeitarZeroKmAsync(new RenaveRejeicaoZeroKmInput { Chassi = "93HNOVO0000000002", Motivo = "desistencia" });
            var corpo = JsonDocument.Parse(_renave.Chamadas.Single().Corpo!).RootElement;
            corpo.GetProperty("motivoRejeicao").GetString().Should().Be("DESISTENCIA");
            corpo.GetProperty("cpfOperadorResponsavel").GetString().Should().Be(CpfOperador);
        }

        // ---------------- Entregas ----------------

        [Fact]
        public async Task Entregas_listam_e_realizam_com_o_operador()
        {
            _renave.Rotas["/api/entregas-veiculo-zero-km?estado=PENDENTE"] = (HttpStatusCode.OK,
                """[{"id":31,"chassi":"93HNOVO0000000002","estado":"PENDENTE","cnpjEstabelecimentoVendedor":"11222333000181","idAtpv":"A1"}]""");
            _renave.Rotas["/api/entregas-veiculo-zero-km/realizacao"] = (HttpStatusCode.OK,
                """{"id":31,"estado":"REALIZADA","realizacaoEntrega":{"cpfResponsavel":"52998224725","dataHoraRegistro":"2026-11-04T15:00:00"}}""");

            var lista = await _service.ListarEntregasAsync("pendente");
            lista.Should().ContainSingle(e => e.Id == 31 && e.Estado == "PENDENTE" && e.CnpjVendedor == "11222333000181");

            var feita = await _service.RealizarEntregaAsync(31);
            feita.Estado.Should().Be("REALIZADA");
            feita.DataRealizacao.Should().Be(new DateTime(2026, 11, 4, 15, 0, 0));
            var corpo = JsonDocument.Parse(_renave.Chamadas.Last().Corpo!).RootElement;
            corpo.GetProperty("idEntrega").GetInt64().Should().Be(31);
            corpo.GetProperty("cpfOperadorResponsavel").GetString().Should().Be(CpfOperador);

            var estadoInvalido = () => _service.ListarEntregasAsync("xyz");
            await estadoInvalido.Should().ThrowAsync<RenaveException>();
        }

        // ---------------- Consulta e termos ----------------

        [Fact]
        public async Task Consulta_traz_restricoes_estoques_e_o_veiculo_do_sistema()
        {
            var vazia = () => _service.ConsultarVeiculoAsync(null, " ", null);
            await vazia.Should().ThrowAsync<RenaveException>().WithMessage("*placa, o chassi ou o Renavam*");

            _renave.Rotas["/api/veiculos?placa=ABC1D23"] = (HttpStatusCode.OK,
                """{"placa":"ABC1D23","renavam":"00123456789","chassi":"9BWZZZ377VT004251","veiculoAcabado":true,"restricoes":[{"codigoTipo":"1","tipo":"ALIENACAO FIDUCIARIA"}]}""");
            _renave.Rotas["/api/estoques?placa=ABC1D23"] = (HttpStatusCode.OK,
                $"[{Estoque(902, "FINALIZADO", "ABC1D23", "9BWZZZ377VT004251", "{\"dataHora\":\"2026-09-01T10:00:00\",\"motivo\":\"VENDA\"}")}]");

            var c = await _service.ConsultarVeiculoAsync("abc-1d23", null, null);

            c.Restricoes.Should().Equal("ALIENACAO FIDUCIARIA");
            c.VeiculoId.Should().Be(7);
            c.Estoques.Should().ContainSingle(e => e.Id == 902 && e.MotivoSaida == "VENDA" && e.DataSaida == new DateTime(2026, 9, 1, 10, 0, 0));
        }

        [Fact]
        public async Task Termo_de_saida_so_depois_da_saida_e_volta_o_pdf()
        {
            var registro = new VeiculoRenave(7);
            registro.RegistrarEntrada(902, "CONFIRMADO", DateTime.Today, 1m);
            _registros.Itens[7] = registro;

            var antes = () => _service.ObterTermoAsync(7, saida: true);
            await antes.Should().ThrowAsync<RenaveException>().WithMessage("*depois da saída*");
            var semAssinatura = () => _service.ObterTermoAsync(7, saida: false);
            await semAssinatura.Should().ThrowAsync<RenaveException>().WithMessage("*assinatura do vendedor*");
            registro.RegistrarAssinaturaVendedor();

            var pdf = Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.4"));
            _renave.Rotas["/api/estoques/902/termo-entrada-estoque"] = (HttpStatusCode.OK, $$"""{"numeroTermoEntradaEstoque":55,"pdfBase64":"{{pdf}}"}""");
            var (bytes, numero) = await _service.ObterTermoAsync(7, saida: false);
            Encoding.ASCII.GetString(bytes).Should().Be("%PDF-1.4");
            numero.Should().Be("55");
        }

        // ---------------- Carro que volta para a loja ----------------

        [Fact]
        public void Entrada_nova_depois_de_vendido_comeca_um_ciclo_limpo()
        {
            var r = new VeiculoRenave(7);
            r.RegistrarEntrada(1, "CONFIRMADO", DateTime.Today.AddYears(-1), 50000m, "52998224725");
            r.RegistrarAssinaturaVendedor();
            r.RegistrarSaida("FINALIZADO", DateTime.Today.AddMonths(-6), 60000m, "11222333000181", "Comprador");
            r.RegistrarNotaSaida("35260811222333000181550010000000011000000014");
            r.EmEstoque.Should().BeFalse();

            r.RegistrarEntrada(2, "SOLICITADO", DateTime.Today, 55000m, "11222333000181");

            r.EmEstoque.Should().BeTrue();
            r.RnvDtAssinaturaVendedor.Should().BeNull();
            r.RnvChaveNfSaida.Should().BeNull();
            RenaveService.ParaSituacao(7, r).ProximoPasso.Should().Contain("assinatura do vendedor");
        }
    }
}
