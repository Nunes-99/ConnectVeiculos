using ConnectVeiculos.Infrastructure.Services.NotasFiscais;
using FluentAssertions;
using Unimake.Business.DFe.Servicos;
using Unimake.Business.DFe.Servicos.NFe;
using Xunit;

namespace ConnectVeiculos.Tests.NotasFiscais
{
    public class DanfeGeradorTests
    {
        /// <summary>NF-e assinada de verdade (certificado de teste) dentro de um nfeProc com protocolo ficticio.</summary>
        public static string NfeProcDeExemplo()
        {
            var xml = NfeVeiculoBuilder.Montar(NfeVeiculoBuilderTests.Config(3), NfeVeiculoBuilderTests.Venda(),
                NfeVeiculoBuilderTests.Carro(), 1, 1234, 87654321, new DateTime(2026, 11, 1, 10, 30, 0), homologacao: true);
            using var cert = NfeVeiculoBuilderTests.CertificadoDeTeste();
            var aut = new Autorizacao(xml, new Configuracao { TipoDFe = TipoDFe.NFe, TipoEmissao = TipoEmissao.Normal, CertificadoDigital = cert });
            var nfe = aut.ConteudoXMLAssinado.GetElementsByTagName("NFe")[0]!.OuterXml;
            var chave = xml.NFe[0].InfNFeField.Chave;
            return "<nfeProc xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"4.00\">" + nfe +
                   "<protNFe versao=\"4.00\"><infProt><tpAmb>2</tpAmb><chNFe>" + chave +
                   "</chNFe><dhRecbto>2026-11-01T10:30:05-03:00</dhRecbto><nProt>135260000001234</nProt><cStat>100</cStat>" +
                   "<xMotivo>Autorizado o uso da NF-e</xMotivo></infProt></protNFe></nfeProc>";
        }

        [Fact]
        public void Gera_Pdf_Do_Danfe_A_Partir_Do_Xml_Autorizado()
        {
            var pdf = DanfeGerador.Gerar(NfeProcDeExemplo());

            pdf.Length.Should().BeGreaterThan(5000);
            System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");

            // Imagem para conferencia visual quando DANFE_PNG aponta para um arquivo.
            if (Environment.GetEnvironmentVariable("DANFE_PNG") is { Length: > 0 } destino)
                File.WriteAllBytes(destino, DanfeGerador.GerarImagem(NfeProcDeExemplo()));
        }

        [Fact]
        public void Chave_Sai_Em_Grupos_De_Quatro()
            => DanfeGerador.ChaveFormatada("35261111222333000181550010000001231000001234")
                .Should().Be("3526 1111 2223 3300 0181 5500 1000 0001 2310 0000 1234");
    }
}
