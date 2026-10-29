using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.Auth
{
    /// <inheritdoc cref="ITentativasLoginService"/>
    public class TentativasLoginService : ITentativasLoginService
    {
        // 5 erros liberam o teclado de quem so se confundiu; a partir dai o
        // bloqueio de 15 min torna forca bruta inviavel (max ~20 tentativas/hora
        // por conta e IP).
        private const int MaxFalhas = 5;

        // Teto da conta somando todos os IPs: forca bruta distribuida. Alto o
        // bastante para que trancar alguem de proposito exija muitos IPs.
        private const int MaxFalhasConta = 50;

        private static readonly TimeSpan JanelaContagem = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan DuracaoBloqueio = TimeSpan.FromMinutes(15);

        private readonly IMemoryCache _cache;
        private readonly ILogger<TentativasLoginService> _logger;

        public TentativasLoginService(IMemoryCache cache, ILogger<TentativasLoginService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        private static string Origem(string email, string? ip)
            => string.IsNullOrWhiteSpace(ip) ? Normalizar(email) : $"{Normalizar(email)}|{ip.Trim()}";
        private static string ChaveFalhas(string origem) => $"login:falhas:{origem}";
        private static string ChaveBloqueio(string origem) => $"login:bloqueio:{origem}";
        private static string ChaveFalhasConta(string email) => $"login:falhas-conta:{Normalizar(email)}";
        private static string ChaveBloqueioConta(string email) => $"login:bloqueio-conta:{Normalizar(email)}";
        private static string Normalizar(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        public int? SegundosBloqueioRestantes(string email, string? ip = null)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var restante = Restante(ChaveBloqueio(Origem(email, ip)));
            if (!string.IsNullOrWhiteSpace(ip))
            {
                var conta = Restante(ChaveBloqueioConta(email));
                if (conta.HasValue && (!restante.HasValue || conta > restante)) restante = conta;
            }
            return restante;
        }

        private int? Restante(string chave)
        {
            if (_cache.TryGetValue<DateTimeOffset>(chave, out var liberaEm))
            {
                var restante = liberaEm - DateTimeOffset.UtcNow;
                if (restante > TimeSpan.Zero)
                    return (int)Math.Ceiling(restante.TotalSeconds);
            }
            return null;
        }

        public void RegistrarFalha(string email, string? ip = null)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            var origem = Origem(email, ip);
            if (Contar(ChaveFalhas(origem), ChaveBloqueio(origem), MaxFalhas))
                _logger.LogWarning(
                    "Login de {Email} bloqueado por {Minutos} min (origem {Ip}) apos {Falhas} tentativas malsucedidas",
                    Normalizar(email), DuracaoBloqueio.TotalMinutes, ip ?? "-", MaxFalhas);

            if (!string.IsNullOrWhiteSpace(ip)
                && Contar(ChaveFalhasConta(email), ChaveBloqueioConta(email), MaxFalhasConta))
                _logger.LogWarning(
                    "Conta {Email} bloqueada por {Minutos} min: {Falhas} tentativas malsucedidas somando todos os IPs",
                    Normalizar(email), DuracaoBloqueio.TotalMinutes, MaxFalhasConta);
        }

        // Soma uma falha; devolve true quando ela atingiu o limite e bloqueou.
        private bool Contar(string chaveFalhas, string chaveBloqueio, int limite)
        {
            var falhas = _cache.TryGetValue<int>(chaveFalhas, out var atual) ? atual + 1 : 1;

            // Janela absoluta: a contagem expira sozinha se o usuario parar de
            // errar, entao um erro hoje nao soma com outro de semana passada.
            _cache.Set(chaveFalhas, falhas, JanelaContagem);
            if (falhas < limite) return false;

            _cache.Set(chaveBloqueio, DateTimeOffset.UtcNow.Add(DuracaoBloqueio), DuracaoBloqueio);
            _cache.Remove(chaveFalhas);
            return true;
        }

        public void LimparFalhas(string email, string? ip = null)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            var origem = Origem(email, ip);
            _cache.Remove(ChaveFalhas(origem));
            _cache.Remove(ChaveBloqueio(origem));
        }
    }
}
