using ConnectVeiculos.Infrastructure.Services.Auth;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// O e-mail da loja aparece no catalogo. Com o bloqueio so por conta,
    /// qualquer um trancava o admin errando a senha 5 vezes a cada 15 min.
    /// </summary>
    public class TentativasLoginPorIpTests
    {
        private const string Email = "admin@loja.com.br";

        private static TentativasLoginService Criar() =>
            new(new MemoryCache(new MemoryCacheOptions()), NullLogger<TentativasLoginService>.Instance);

        [Fact]
        public void Erros_de_outro_ip_nao_trancam_o_dono_da_conta()
        {
            var s = Criar();
            for (var i = 0; i < 5; i++) s.RegistrarFalha(Email, "203.0.113.9");

            s.SegundosBloqueioRestantes(Email, "203.0.113.9").Should().NotBeNull("o atacante fica bloqueado");
            s.SegundosBloqueioRestantes(Email, "198.51.100.7").Should().BeNull("o dono, em outro IP, entra");
        }

        [Fact]
        public void Forca_bruta_espalhada_em_muitos_ips_tranca_a_conta()
        {
            var s = Criar();
            for (var i = 0; i < 50; i++) s.RegistrarFalha(Email, $"203.0.113.{i % 12}");

            s.SegundosBloqueioRestantes(Email, "198.51.100.7").Should().NotBeNull();
        }

        [Fact]
        public void Login_certo_libera_so_aquele_ip()
        {
            var s = Criar();
            for (var i = 0; i < 5; i++) s.RegistrarFalha(Email, "198.51.100.7");

            s.LimparFalhas(Email, "198.51.100.7");

            s.SegundosBloqueioRestantes(Email, "198.51.100.7").Should().BeNull();
        }
    }
}
