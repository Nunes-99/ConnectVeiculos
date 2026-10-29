using System.Net;
using ConnectVeiculos.Infrastructure.Services.Webhook;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// Webhook de saida nao pode apontar para dentro da VM nem da nuvem.
    /// </summary>
    public class UrlExternaTests
    {
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("10.0.0.5")]
        [InlineData("172.18.0.3")]       // rede do Docker
        [InlineData("192.168.1.1")]
        [InlineData("169.254.169.254")]  // metadados da nuvem
        [InlineData("100.64.0.1")]
        [InlineData("0.0.0.0")]
        [InlineData("::1")]
        [InlineData("fd00::1")]
        [InlineData("fe80::1")]
        [InlineData("::ffff:127.0.0.1")]
        public void Endereco_interno_nao_e_publico(string ip)
        {
            UrlExterna.EhPublico(IPAddress.Parse(ip)).Should().BeFalse();
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("136.248.77.154")]
        [InlineData("2606:4700:4700::1111")]
        public void Endereco_da_internet_e_publico(string ip)
        {
            UrlExterna.EhPublico(IPAddress.Parse(ip)).Should().BeTrue();
        }

        [Theory]
        [InlineData("http://8.8.8.8/hook")]          // sem https
        [InlineData("https://127.0.0.1/hook")]
        [InlineData("https://169.254.169.254/latest")]
        [InlineData("https://localhost/hook")]
        [InlineData("ftp://8.8.8.8/")]
        [InlineData("nao e url")]
        [InlineData(null)]
        public async Task Url_insegura_e_recusada(string? url)
        {
            (await UrlExterna.EhSeguraAsync(url)).Should().BeFalse();
        }

        [Fact]
        public async Task Https_para_ip_publico_passa()
        {
            (await UrlExterna.EhSeguraAsync("https://8.8.8.8/hook")).Should().BeTrue();
        }
    }
}
