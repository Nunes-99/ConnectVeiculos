using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.NotasFiscais;
using ConnectVeiculos.Infrastructure.Services.NotasFiscais;
using FluentAssertions;
using Unimake.Business.DFe.Servicos;
using Unimake.Business.DFe.Servicos.NFe;
using Xunit;

namespace ConnectVeiculos.Tests.NotasFiscais
{
    public class NfeVeiculoBuilderTests
    {
        public static NfeConfigInfo Config(int crt = 1) => new()
        {
            Ambiente = "homologacao",
            Cnpj = "11222333000181",
            InscricaoEstadual = "111111111111",
            RazaoSocial = "Diamante Veiculos Comercio de Automoveis Ltda",
            NomeFantasia = "Diamante Veículos",
            Crt = crt,
            Logradouro = "Rua Teste",
            Numero = "100",
            Bairro = "Centro",
            Cep = "13320000",
            CodigoMunicipio = 3545209,
            Municipio = "Salto",
            Uf = "SP",
            Telefone = "11949803898",
            Serie = 1,
            Compra = new NfeRegraOperacao
            {
                NaturezaOperacao = "Compra de veículo usado", CfopInterno = "1102", CfopInterestadual = "2102",
                CstIcms = crt == 3 ? "90" : "900", CstPis = "49", CstCofins = "49"
            },
            Venda = new NfeRegraOperacao
            {
                NaturezaOperacao = "Venda de veículo usado", CfopInterno = "5102", CfopInterestadual = "6102",
                CstIcms = crt == 3 ? "20" : "102", AliquotaIcms = crt == 3 ? 18m : 0m, ReducaoBaseIcms = crt == 3 ? 95m : 0m,
                CstPis = "49", CstCofins = "49"
            }
        };

        public static Veiculo Carro() => new(
            42, 1, 1, "Toyota", "Corolla XEi", 2020, "ABC1D23", "9BRBL3HE0L0123456", "Prata", 45000, 90000m,
            DateTime.Now, "D", "A", 80000m, veiRenavam: "01234567890");

        public static NfeEmissaoInput Venda() => new()
        {
            Tipo = "SAIDA",
            Documento = "529.982.247-25",
            Nome = "João da Silva",
            Email = "joao@exemplo.com",
            Endereco = new NfeEndereco
            {
                Logradouro = "Av. Brasil", Numero = "10", Bairro = "Centro", Cep = "13320-000",
                CodigoMunicipio = 3545209, Municipio = "Salto", Uf = "SP"
            },
            Valor = 95000m,
            MeioPagamento = "17"
        };

        /// <summary>Certificado de teste (auto-assinado) so para assinar e validar, sem Sefaz.</summary>
        public static X509Certificate2 CertificadoDeTeste()
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=TESTE:11222333000181", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
            return new X509Certificate2(cert.Export(X509ContentType.Pfx, "1"), "1", X509KeyStorageFlags.Exportable);
        }

        private static Autorizacao Assinar(Unimake.Business.DFe.Xml.NFe.EnviNFe xml)
        {
            using var cert = CertificadoDeTeste();
            var config = new Configuracao { TipoDFe = TipoDFe.NFe, TipoEmissao = TipoEmissao.Normal, CertificadoDigital = cert };
            // O construtor assina e valida o XML contra o schema oficial (XSD) da NF-e.
            return new Autorizacao(xml, config);
        }

        [Theory]
        [InlineData(1, "SAIDA")]
        [InlineData(1, "ENTRADA")]
        [InlineData(3, "SAIDA")]
        [InlineData(3, "ENTRADA")]
        public void Nota_De_Veiculo_Passa_No_Schema_Oficial(int crt, string tipo)
        {
            var dados = Venda();
            dados.Tipo = tipo;
            if (tipo == "ENTRADA") dados.MeioPagamento = "90";
            var xml = NfeVeiculoBuilder.Montar(Config(crt), dados, Carro(), 1, 123, 12345678, DateTime.Now, homologacao: true);

            var autorizacao = Assinar(xml);

            var assinado = autorizacao.ConteudoXMLAssinado.OuterXml;
            assinado.Should().Contain("<Signature");
            assinado.Should().Contain("PLACA ABC1D23").And.Contain("RENAVAM 01234567890");
            assinado.Should().Contain(NfeVeiculoBuilder.NomeHomologacao);
            xml.NFe[0].InfNFeField.Chave.Should().HaveLength(44).And.StartWith("35");
        }

        /// <summary>Variacoes reais de cliente, endereco, regime e valor: todas no schema oficial.</summary>
        [Theory]
        [InlineData("SAIDA", 1, "PF", "SP", "Joao D'Avila & Cia", "10", 95000.50)]
        [InlineData("SAIDA", 1, "PJ", "SP", "Auto Pecas São José Ltda", "", 120000)]
        [InlineData("SAIDA", 3, "PF", "MG", "María Conceição Ñandú", "S/N", 45999.99)]
        [InlineData("SAIDA", 4, "PF", "SP", "Cliente Do Mei", "1500", 30000)]
        [InlineData("ENTRADA", 1, "PF", "SP", "Vendedor Pessoa Fisica", "22", 50000)]
        [InlineData("ENTRADA", 3, "PJ", "PR", "Locadora Exemplo S.A.", "1", 70000)]
        public void Variacoes_Passam_No_Schema(string tipo, int crt, string pessoa, string uf, string nome, string numero, double valor)
        {
            var cfg = Config(crt == 4 ? 1 : crt);
            cfg.Crt = crt;
            var dados = Venda();
            dados.Tipo = tipo;
            dados.Nome = nome;
            dados.Valor = (decimal)valor;
            dados.Endereco.Numero = numero;
            dados.Endereco.Complemento = "Apto 12 - Bloco B";
            if (tipo == "ENTRADA") dados.MeioPagamento = "90";
            if (pessoa == "PJ")
            {
                dados.Documento = "11.444.777/0001-61";
                dados.InscricaoEstadual = uf == "SP" ? "110042490114" : "1234567850";
            }
            if (uf == "MG") { dados.Endereco.Uf = "MG"; dados.Endereco.CodigoMunicipio = 3106200; dados.Endereco.Municipio = "Belo Horizonte"; }
            if (uf == "PR") { dados.Endereco.Uf = "PR"; dados.Endereco.CodigoMunicipio = 4106902; dados.Endereco.Municipio = "Curitiba"; }

            var xml = NfeVeiculoBuilder.Montar(cfg, dados, Carro(), 1, 321, 87654321, DateTime.Now, homologacao: true);
            var assinado = Assinar(xml).ConteudoXMLAssinado.OuterXml;

            assinado.Should().Contain("<Signature");
            xml.NFe[0].InfNFeField.Det[0].Prod.CFOP.Should().StartWith(uf == "SP" ? (tipo == "SAIDA" ? "5" : "1") : (tipo == "SAIDA" ? "6" : "2"));
        }

        [Fact]
        public void Nota_Fora_Do_Schema_E_Recusada_Antes_De_Ir_Para_A_Sefaz()
        {
            var dados = Venda();
            dados.Ncm = "123"; // NCM tem 8 digitos
            var xml = NfeVeiculoBuilder.Montar(Config(), dados, Carro(), 1, 7, 12345678, DateTime.Now, homologacao: true);

            var acao = () => Assinar(xml);

            acao.Should().Throw<Exception>().Which.Message.Should().Contain("NCM");
        }

        [Fact]
        public void Regime_Normal_Calcula_Icms_Com_Base_Reduzida()
        {
            var (_, vBc, vIcms) = NfeVeiculoBuilder.MontarIcms(3, Config(3).Venda, 100000m);

            vBc.Should().Be(5000m);     // 5% do valor
            vIcms.Should().Be(900m);    // 18% de 5.000
        }

        [Fact]
        public void Venda_Para_Outro_Estado_Usa_Cfop_Interestadual()
        {
            var dados = Venda();
            dados.Endereco.Uf = "MG";
            dados.Endereco.CodigoMunicipio = 3106200;
            dados.Endereco.Municipio = "Belo Horizonte";

            var xml = NfeVeiculoBuilder.Montar(Config(), dados, Carro(), 1, 1, 1, DateTime.Now, homologacao: true);

            xml.NFe[0].InfNFeField.Det[0].Prod.CFOP.Should().Be("6102");
            xml.NFe[0].InfNFeField.Ide.IdDest.Should().Be(DestinoOperacao.OperacaoInterestadual);
        }

        [Fact]
        public void Codigo_De_Imposto_Desconhecido_E_Recusado_Com_Mensagem()
        {
            var cfg = Config();
            cfg.Venda.CstIcms = "101";

            var acao = () => NfeVeiculoBuilder.Montar(cfg, Venda(), Carro(), 1, 1, 1, DateTime.Now, true);

            acao.Should().Throw<NfeException>().WithMessage("*CSOSN*101*");
        }
    }
}
