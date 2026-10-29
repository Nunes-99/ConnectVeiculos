using System.Security.Cryptography;
using System.Text;
using ConnectVeiculos.API.Controllers;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Publicacoes
{
    /// <summary>
    /// O webhook do WhatsApp e' anonimo. Sem conferir o X-Hub-Signature-256,
    /// qualquer um criava leads em qualquer loja com um POST inventado.
    /// </summary>
    public class AssinaturaWebhookMetaTests
    {
        private const string Secret = "segredo-do-app";
        private const string Corpo = "{\"entry\":[]}";

        private static string Assinar(string corpo, string secret)
            => "sha256=" + Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(corpo))).ToLowerInvariant();

        [Fact]
        public void Assinatura_da_meta_passa()
        {
            IntegracoesController.AssinaturaMetaValida(Corpo, Assinar(Corpo, Secret), Secret).Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("sha256=")]
        [InlineData("sha256=nao-e-hex")]
        [InlineData("sha1=abcdef")]
        public void Sem_assinatura_ou_assinatura_malformada_e_recusada(string? cabecalho)
        {
            IntegracoesController.AssinaturaMetaValida(Corpo, cabecalho, Secret).Should().BeFalse();
        }

        [Fact]
        public void Corpo_alterado_depois_de_assinado_e_recusado()
        {
            var assinatura = Assinar(Corpo, Secret);

            IntegracoesController.AssinaturaMetaValida("{\"entry\":[{}]}", assinatura, Secret).Should().BeFalse();
        }

        [Fact]
        public void Assinado_com_outro_secret_e_recusado()
        {
            IntegracoesController.AssinaturaMetaValida(Corpo, Assinar(Corpo, "outro"), Secret).Should().BeFalse();
        }

        [Fact]
        public void Sem_app_secret_configurado_recusa_em_vez_de_aceitar_tudo()
        {
            IntegracoesController.AssinaturaMetaValida(Corpo, Assinar(Corpo, ""), "").Should().BeFalse();
        }
    }
}
