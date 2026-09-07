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
        // por conta).
        private const int MaxFalhas = 5;
        private static readonly TimeSpan JanelaContagem = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan DuracaoBloqueio = TimeSpan.FromMinutes(15);

        private readonly IMemoryCache _cache;
        private readonly ILogger<TentativasLoginService> _logger;

        public TentativasLoginService(IMemoryCache cache, ILogger<TentativasLoginService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        private static string ChaveFalhas(string email) => $"login:falhas:{Normalizar(email)}";
        private static string ChaveBloqueio(string email) => $"login:bloqueio:{Normalizar(email)}";
        private static string Normalizar(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        public int? SegundosBloqueioRestantes(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            if (_cache.TryGetValue<DateTimeOffset>(ChaveBloqueio(email), out var liberaEm))
            {
                var restante = liberaEm - DateTimeOffset.UtcNow;
                if (restante > TimeSpan.Zero)
                    return (int)Math.Ceiling(restante.TotalSeconds);
            }

            return null;
        }

        public void RegistrarFalha(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            var chave = ChaveFalhas(email);
            var falhas = _cache.TryGetValue<int>(chave, out var atual) ? atual + 1 : 1;

            // Janela absoluta: a contagem expira sozinha se o usuario parar de
            // errar, entao um erro hoje nao soma com outro de semana passada.
            _cache.Set(chave, falhas, JanelaContagem);

            if (falhas >= MaxFalhas)
            {
                _cache.Set(ChaveBloqueio(email), DateTimeOffset.UtcNow.Add(DuracaoBloqueio), DuracaoBloqueio);
                _cache.Remove(chave);
                _logger.LogWarning(
                    "Conta {Email} bloqueada por {Minutos} min apos {Falhas} tentativas de login malsucedidas",
                    Normalizar(email), DuracaoBloqueio.TotalMinutes, falhas);
            }
        }

        public void LimparFalhas(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            _cache.Remove(ChaveFalhas(email));
            _cache.Remove(ChaveBloqueio(email));
        }
    }
}
