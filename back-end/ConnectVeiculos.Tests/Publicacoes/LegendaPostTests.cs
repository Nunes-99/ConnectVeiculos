using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Infrastructure.Services.Meta;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Publicacoes
{
    /// <summary>Legenda dos posts a partir do modelo da loja (ou do padrao).</summary>
    public class LegendaPostTests
    {
        private static readonly Veiculo Gol =
            new(7, 1, 1, "VW", "Gol", 2012, "ABC1D23", "", "Prata", 98000, 31900m, DateTime.Today, "D", "", 0m, "");

        private static Loja Loja() => new(
            1, "Diamante Veículos", "Rua A", "10", "Centro", "São Paulo", "SP", "01000-000",
            null, "contato@diamante.com.br", null, null, "(11) 98888-7777", null, null, null, true);

        [Fact]
        public void PadraoDoInstagramContinuaIgualAoTextoDeAntes()
        {
            // Mesma saida do MontarLegenda antigo: quem nunca mexeu nao percebe mudanca.
            LegendaPost.Montar(null, LegendaPost.Instagram, Gol, null, null).Should().Be(
                "🚗 VW Gol 2012\n💰 R$ 31.900,00\n📊 98.000 km\n🎨 Prata\n\n" +
                "👉 Link na bio para mais detalhes!\n\n" +
                "#VW #VWGol #carros #usados #seminovos #carrosanovenda");
        }

        [Fact]
        public void PadraoDoFacebookTemOLinkEOWhatsApp()
        {
            var texto = LegendaPost.Montar("", LegendaPost.Facebook, Gol, Loja(), "https://loja/v/7");

            texto.Should().Contain("🎨 Cor: Prata")
                 .And.Contain("📞 Entre em contato pelo link: https://loja/v/7")
                 .And.Contain("📲 WhatsApp: (11) 98888-7777")
                 .And.EndWith("#VW #VWGol #carros #usados #seminovos #SãoPaulo");
        }

        [Fact]
        public void HashtagDoModeloNaoLevaAVersao()
        {
            // "Civic EXL 2.0 16V 4p Aut." virava #HondaCivicEXL2016V4pAut: o "2016" parecia o ano.
            var civic = new Veiculo(8, 1, 1, "Honda", "Civic EXL 2.0 16V 4p Aut.", 2023, "ABC1D23", "", "Branco", 15000, 115000m, DateTime.Today, "D", "", 0m, "");

            LegendaPost.Montar("{hashtags}", LegendaPost.Instagram, civic, null, null)
                .Should().StartWith("#Honda #HondaCivic #carros");
        }

        [Fact]
        public void ModeloDaLojaTrocaAsVariaveis()
        {
            var texto = LegendaPost.Montar("{MARCA} {modelo} por {preco} na {loja}!", LegendaPost.Instagram, Gol, Loja(), null);

            texto.Should().Be("VW Gol por R$ 31.900,00 na Diamante Veículos!");
        }

        [Fact]
        public void LinhaComTodasAsVariaveisVaziasSome()
        {
            var texto = LegendaPost.Montar("Carro: {modelo}\n✨ {opcionais}\n\n\n📲 {whatsapp}\nFim", LegendaPost.Instagram, Gol, null, null);

            texto.Should().Be("Carro: Gol\n\nFim");
        }

        [Fact]
        public void VariavelDesconhecidaFicaComoFoiEscrita()
        {
            LegendaPost.Montar("Preço {valor}", LegendaPost.Instagram, Gol, null, null).Should().Be("Preço {valor}");
        }

        [Fact]
        public void LegendaLongaECortadaNoLimiteDoInstagram()
        {
            LegendaPost.Montar(new string('a', 5000), LegendaPost.Instagram, Gol, null, null)
                .Should().HaveLength(LegendaPost.TamanhoMaximo);
        }
    }
}
