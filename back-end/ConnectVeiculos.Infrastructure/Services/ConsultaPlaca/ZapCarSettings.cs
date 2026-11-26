using System.Globalization;

namespace ConnectVeiculos.Infrastructure.Services.ConsultaPlaca
{
    /// <summary>
    /// Conta ZapCar da plataforma (as lojas pagam com creditos). Env:
    /// ZapCarSettings__ApiKey (zc_live_... ou zc_test_...), __PrecoCompleta,
    /// __PrecoSimples, __Pacotes ("60;180;300"). Sem a chave, a consulta fica desligada.
    /// </summary>
    public class ZapCarSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.zapcarconsulta.com.br/v1";

        /// <summary>Preco para a loja da "consulta-completa" (custo ZapCar ~R$ 36).</summary>
        public decimal PrecoCompleta { get; set; } = 60m;

        /// <summary>Preco para a loja da "consulta" simples (custo ZapCar ~R$ 6).</summary>
        public decimal PrecoSimples { get; set; } = 15m;

        /// <summary>Valores de recarga por Pix, separados por ";".</summary>
        public string Pacotes { get; set; } = "60;180;300";

        public bool Configurado => !string.IsNullOrWhiteSpace(ApiKey);
        public bool Teste => ApiKey.Trim().StartsWith("zc_test_", StringComparison.Ordinal);

        public IReadOnlyList<decimal> ValoresPacotes() =>
            (Pacotes ?? "")
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0)
                .Where(v => v > 0)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
    }
}
