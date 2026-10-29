using ConnectVeiculos.Infrastructure.Services.Meta;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Publicacoes
{
    /// <summary>
    /// O token da Page nao expira, mas para de valer quando a pessoa troca a
    /// senha ou remove o app. O lojista precisa ler "reconecte", nao o JSON da Meta.
    /// </summary>
    public class MetaErroTests
    {
        [Theory]
        [InlineData(190)]
        [InlineData(102)]
        public void Token_invalido_pede_para_reconectar(int codigo)
        {
            var body = "{\"error\":{\"message\":\"Error validating access token\",\"type\":\"OAuthException\",\"code\":" + codigo + "}}";

            MetaErro.Descrever(400, body).Should().Be(MetaErro.MensagemReconectar);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(200)]
        public void Falta_de_permissao_pede_para_aceitar_as_permissoes(int codigo)
        {
            var body = "{\"error\":{\"message\":\"Permission denied\",\"code\":" + codigo + "}}";

            MetaErro.Descrever(403, body).Should().Contain("falta de permissão");
        }

        [Fact]
        public void Outro_erro_mostra_a_mensagem_da_meta_sem_o_json()
        {
            var body = """{"error":{"message":"Unsupported get request.","code":100}}""";

            MetaErro.Descrever(400, body).Should().Be("Falha na Meta: Unsupported get request.");
        }

        [Fact]
        public void Resposta_que_nao_e_json_continua_aparecendo()
        {
            MetaErro.Descrever(502, "Bad Gateway").Should().Be("Falha (502): Bad Gateway");
        }
    }
}
