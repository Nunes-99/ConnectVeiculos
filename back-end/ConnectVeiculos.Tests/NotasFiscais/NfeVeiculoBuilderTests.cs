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
            // Regime normal vendendo a pessoa fisica de outro estado: DIFAL.
            dados.AliquotaInternaDestino = 18m;
            if (uf == "PR") { dados.Endereco.Uf = "PR"; dados.Endereco.CodigoMunicipio = 4106902; dados.Endereco.Municipio = "Curitiba"; }

            var xml = NfeVeiculoBuilder.Montar(cfg, dados, Carro(), 1, 321, 87654321, DateTime.Now, homologacao: true);
            var assinado = Assinar(xml).ConteudoXMLAssinado.OuterXml;

            assinado.Should().Contain("<Signature");
            xml.NFe[0].InfNFeField.Det[0].Prod.CFOP.Should().StartWith(uf == "SP" ? (tipo == "SAIDA" ? "5" : "1") : (tipo == "SAIDA" ? "6" : "2"));
        }

        [Theory]
        [InlineData("isento", null, 2, true)]
        [InlineData("nao_contribuinte", null, 9, true)]
        [InlineData("contribuinte", "110042490114", 1, false)]
        [InlineData(null, "110042490114", 1, false)]
        [InlineData(null, null, 9, true)]
        public void Situacao_Do_Cliente_No_Icms_Vai_Na_Nota(string? indicador, string? ie, int esperado, bool consumidorFinal)
        {
            var dados = Venda();
            dados.Documento = "11.444.777/0001-61";
            dados.IndicadorIe = indicador;
            dados.InscricaoEstadual = ie;

            var xml = NfeVeiculoBuilder.Montar(Config(), dados, Carro(), 1, 10, 11111111, DateTime.Now, homologacao: true);
            Assinar(xml).ConteudoXMLAssinado.OuterXml.Should().Contain("<Signature");

            var inf = xml.NFe[0].InfNFeField;
            ((int)inf.Dest.IndIEDest).Should().Be(esperado);
            // Isento e nao contribuinte nao levam IE.
            (inf.Dest.IE != null).Should().Be(esperado == 1);
            (inf.Ide.IndFinal == SimNao.Sim).Should().Be(consumidorFinal);
        }

        [Fact]
        public void Contribuinte_Sem_Inscricao_E_Recusado()
        {
            var dados = Venda();
            dados.IndicadorIe = "contribuinte";

            var acao = () => NfeVeiculoBuilder.Montar(Config(), dados, Carro(), 1, 10, 11111111, DateTime.Now, homologacao: true);

            acao.Should().Throw<NfeException>().WithMessage("*inscrição estadual*");
        }

        public static NfeVeiculoNovo ZeroKm() => new()
        {
            TipoOperacao = 1, Chassi = "9BRBL3HE0L0123456", CodigoCor = "1E7", DescricaoCor = "Prata Supernova",
            Potencia = "177", Cilindradas = "1987", PesoLiquido = "1.3500", PesoBruto = "1.7800", NumeroSerie = "123456",
            TipoCombustivel = "16", NumeroMotor = "M20AFKS123456", CapacidadeTracao = "1.7800", DistanciaEixos = "2700",
            AnoModelo = 2027, AnoFabricacao = 2026, TipoPintura = "M", TipoVeiculo = "06", EspecieVeiculo = "1",
            Condicao = 1, CodigoMarcaModelo = 123456, CorDenatran = "10", Lotacao = 5, Restricao = 0
        };

        [Theory]
        [InlineData(1, "500")]
        [InlineData(3, "60")]
        public void Zero_Km_Leva_O_Detalhamento_Do_Veiculo_E_O_Cest(int crt, string codigoSt)
        {
            var cfg = Config(crt);
            cfg.Venda.CstIcms = codigoSt;
            cfg.Venda.AliquotaIcms = 0;
            cfg.Venda.ReducaoBaseIcms = 0;
            cfg.CestPadrao = "0105300";
            var dados = Venda();
            dados.VeiculoNovo = ZeroKm();

            var xml = NfeVeiculoBuilder.Montar(cfg, dados, Carro(), 1, 77, 22222222, DateTime.Now, homologacao: true);
            var assinado = Assinar(xml).ConteudoXMLAssinado.OuterXml;

            assinado.Should().Contain("<veicProd>").And.Contain("<chassi>9BRBL3HE0L0123456</chassi>")
                .And.Contain("<cCorDENATRAN>10</cCorDENATRAN>").And.Contain("<CEST>0105300</CEST>");
        }

        [Fact]
        public void Icms_Por_Substituicao_Sem_Cest_E_Recusado_Antes_Da_Sefaz()
        {
            var cfg = Config();
            cfg.Venda.CstIcms = "500";

            var acao = () => NfeVeiculoBuilder.Montar(cfg, Venda(), Carro(), 1, 77, 22222222, DateTime.Now, homologacao: true);

            acao.Should().Throw<NfeException>().WithMessage("*CEST*");
        }

        [Fact]
        public void Zero_Km_Sem_Os_Dados_Da_Montadora_E_Recusado()
        {
            var dados = Venda();
            dados.VeiculoNovo = ZeroKm();
            dados.VeiculoNovo.NumeroMotor = "";

            var acao = () => NfeVeiculoBuilder.Montar(Config(), dados, Carro(), 1, 77, 22222222, DateTime.Now, homologacao: true);

            acao.Should().Throw<NfeException>().WithMessage("*número do motor*");
        }

        [Theory]
        [InlineData("SP", "MG", 12)]
        [InlineData("SP", "BA", 7)]
        [InlineData("SP", "ES", 7)]
        [InlineData("BA", "SP", 12)]
        [InlineData("ES", "BA", 12)]
        public void Aliquota_Interestadual_Segue_A_Regiao(string origem, string destino, int esperada)
            => NfeVeiculoBuilder.AliquotaInterestadual(NfeVeiculoBuilder.Uf(origem), NfeVeiculoBuilder.Uf(destino)).Should().Be(esperada);

        [Fact]
        public void Venda_Interestadual_A_Consumidor_Final_Leva_O_Difal()
        {
            var cfg = Config(3);
            cfg.Venda.CstIcms = "00";
            cfg.Venda.AliquotaIcms = 12m;
            cfg.Venda.ReducaoBaseIcms = 0;
            var dados = Venda();
            dados.Valor = 100000m;
            dados.Endereco.Uf = "BA"; dados.Endereco.CodigoMunicipio = 2927408; dados.Endereco.Municipio = "Salvador";
            dados.AliquotaInternaDestino = 20.5m;
            dados.PercentualFcpDestino = 2m;

            var xml = NfeVeiculoBuilder.Montar(cfg, dados, Carro(), 1, 88, 33333333, DateTime.Now, homologacao: true);
            Assinar(xml).ConteudoXMLAssinado.OuterXml.Should().Contain("<ICMSUFDest>");

            var difal = xml.NFe[0].InfNFeField.Det[0].Imposto.ICMSUFDest;
            difal.PICMSInter.Should().Be(7);           // SP -> BA
            difal.VICMSUFDest.Should().Be(13500);      // 100.000 x (20,5% - 7%)
            difal.VFCPUFDest.Should().Be(2000);
            xml.NFe[0].InfNFeField.Total.ICMSTot.VICMSUFDest.Should().Be(13500);
        }

        [Fact]
        public void Difal_Sem_Aliquota_Do_Destino_E_Recusado_E_Simples_Nao_Leva()
        {
            var dados = Venda();
            dados.Endereco.Uf = "MG"; dados.Endereco.CodigoMunicipio = 3106200; dados.Endereco.Municipio = "Belo Horizonte";

            var regimeNormal = () => NfeVeiculoBuilder.Montar(Config(3), dados, Carro(), 1, 88, 33333333, DateTime.Now, homologacao: true);
            regimeNormal.Should().Throw<NfeException>().WithMessage("*DIFAL*");

            var simples = NfeVeiculoBuilder.Montar(Config(1), dados, Carro(), 1, 88, 33333333, DateTime.Now, homologacao: true);
            simples.NFe[0].InfNFeField.Det[0].Imposto.ICMSUFDest.Should().BeNull();
        }

        [Fact]
        public void Pis_E_Cofins_Tributados_Calculam_E_Somam_No_Total()
        {
            var cfg = Config(3);
            cfg.Venda.CstPis = "01"; cfg.Venda.AliquotaPis = 0.65m;
            cfg.Venda.CstCofins = "01"; cfg.Venda.AliquotaCofins = 3m;
            var dados = Venda();
            dados.Valor = 100000m;

            var xml = NfeVeiculoBuilder.Montar(cfg, dados, Carro(), 1, 99, 44444444, DateTime.Now, homologacao: true);
            Assinar(xml).ConteudoXMLAssinado.OuterXml.Should().Contain("<PISAliq>").And.Contain("<COFINSAliq>");

            var tot = xml.NFe[0].InfNFeField.Total.ICMSTot;
            tot.VPIS.Should().Be(650);
            tot.VCOFINS.Should().Be(3000);
        }

        private const string ChaveVenda = "35261111222333000181550010000000101234567890";

        [Theory]
        [InlineData(1, "SP")]
        [InlineData(3, "SP")]
        [InlineData(3, "MG")]
        public void Devolucao_Referencia_A_Venda_E_Passa_No_Schema(int crt, string uf)
        {
            var dados = Venda();
            dados.Tipo = "DEVOLUCAO";
            dados.ChaveReferenciada = ChaveVenda;
            dados.MeioPagamento = "17"; // a devolucao ignora: sai sem pagamento
            if (uf == "MG") { dados.Endereco.Uf = "MG"; dados.Endereco.CodigoMunicipio = 3106200; dados.Endereco.Municipio = "Belo Horizonte"; }

            var xml = NfeVeiculoBuilder.Montar(Config(crt), dados, Carro(), 1, 55, 55555555, DateTime.Now, homologacao: true);
            Assinar(xml).ConteudoXMLAssinado.OuterXml.Should().Contain("<refNFe>" + ChaveVenda + "</refNFe>");

            var inf = xml.NFe[0].InfNFeField;
            inf.Ide.TpNF.Should().Be(TipoOperacao.Entrada);
            inf.Ide.FinNFe.Should().Be(FinalidadeNFe.Devolucao);
            inf.Det[0].Prod.CFOP.Should().Be(uf == "SP" ? "1202" : "2202");
            inf.Pag.DetPag[0].TPag.Should().Be(MeioPagamento.SemPagamento);
            inf.Det[0].Imposto.ICMSUFDest.Should().BeNull("DIFAL e' so da venda");
        }

        [Fact]
        public void Devolucao_Sem_A_Chave_Da_Venda_E_Recusada()
        {
            var dados = Venda();
            dados.Tipo = "DEVOLUCAO";

            var acao = () => NfeVeiculoBuilder.Montar(Config(), dados, Carro(), 1, 55, 55555555, DateTime.Now, homologacao: true);

            acao.Should().Throw<NfeException>().WithMessage("*chave*");
        }

        private static NfeConfigInfo ConfigIbsCbs(string cst = "000", decimal reducao = 0)
        {
            var cfg = Config(3);
            cfg.Venda.CstIcms = "00";
            cfg.Venda.AliquotaIcms = 12m;
            cfg.Venda.ReducaoBaseIcms = 0;
            cfg.IbsCbsAtivo = true;
            cfg.AliquotaCbs = 0.9m;
            cfg.AliquotaIbsUf = 0.1m;
            cfg.AliquotaIbsMun = 0m;
            cfg.Venda.CstIbsCbs = cst;
            cfg.Venda.ClassTribIbsCbs = cst == "200" ? "200001" : cst == "410" ? "410001" : "000001";
            cfg.Venda.ReducaoIbsCbs = reducao;
            return cfg;
        }

        [Fact]
        public void Ibs_E_Cbs_Saem_No_Schema_Com_A_Base_Sem_O_Icms()
        {
            var dados = Venda();
            dados.Valor = 100000m;

            var xml = NfeVeiculoBuilder.Montar(ConfigIbsCbs(), dados, Carro(), 1, 60, 66666666, DateTime.Now, homologacao: true);
            var assinado = Assinar(xml).ConteudoXMLAssinado.OuterXml;
            assinado.Should().Contain("<IBSCBS>").And.Contain("<IBSCBSTot>");

            var g = xml.NFe[0].InfNFeField.Det[0].Imposto.IBSCBS;
            g.CST.Should().Be("000");
            g.GIBSCBS.VBC.Should().Be(88000);          // 100.000 - 12.000 de ICMS
            g.GIBSCBS.GCBS.VCBS.Should().Be(792);      // 0,9%
            g.GIBSCBS.GIBSUF.VIBSUF.Should().Be(88);   // 0,1%
            g.GIBSCBS.VIBS.Should().Be(88);
            var tot = xml.NFe[0].InfNFeField.Total.IBSCBSTot;
            tot.VBCIBSCBS.Should().Be(88000);
            tot.GCBS.VCBS.Should().Be(792);
            tot.GIBS.VIBS.Should().Be(88);
            xml.NFe[0].InfNFeField.Total.ICMSTot.VNF.Should().Be(100000, "na transicao o IBS/CBS nao soma no total da nota");
        }

        [Fact]
        public void Ibs_E_Cbs_Com_Reducao_Usam_A_Aliquota_Efetiva()
        {
            var dados = Venda();
            dados.Valor = 100000m;

            var xml = NfeVeiculoBuilder.Montar(ConfigIbsCbs("200", 60m), dados, Carro(), 1, 61, 66666667, DateTime.Now, homologacao: true);
            Assinar(xml).ConteudoXMLAssinado.OuterXml.Should().Contain("<gRed>");

            var cbs = xml.NFe[0].InfNFeField.Det[0].Imposto.IBSCBS.GIBSCBS.GCBS;
            cbs.GRed.PAliqEfet.Should().Be(0.36);
            cbs.VCBS.Should().Be(316.8);               // 88.000 x 0,36%
        }

        [Fact]
        public void Ibs_E_Cbs_Nao_Incidencia_Vai_Sem_Valores()
        {
            var xml = NfeVeiculoBuilder.Montar(ConfigIbsCbs("410"), Venda(), Carro(), 1, 62, 66666668, DateTime.Now, homologacao: true);
            Assinar(xml).ConteudoXMLAssinado.OuterXml.Should().Contain("<IBSCBS>");

            xml.NFe[0].InfNFeField.Det[0].Imposto.IBSCBS.GIBSCBS.Should().BeNull();
            xml.NFe[0].InfNFeField.Total.IBSCBSTot.VBCIBSCBS.Should().Be(0);
        }

        [Fact]
        public void Ibs_E_Cbs_Desligados_Nao_Vao_Na_Nota()
        {
            var xml = NfeVeiculoBuilder.Montar(Config(3), Venda(), Carro(), 1, 63, 66666669, DateTime.Now, homologacao: true);

            xml.NFe[0].InfNFeField.Det[0].Imposto.IBSCBS.Should().BeNull();
            xml.NFe[0].InfNFeField.Total.IBSCBSTot.Should().BeNull();
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
