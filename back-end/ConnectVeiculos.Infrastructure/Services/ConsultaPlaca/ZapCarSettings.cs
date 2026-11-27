using System.Globalization;

namespace ConnectVeiculos.Infrastructure.Services.ConsultaPlaca
{
    /// <summary>
    /// Conta ZapCar da plataforma (as lojas pagam com creditos). Env:
    /// ZapCarSettings__ApiKey (zc_live_... ou zc_test_...), __AcrescimoPadrao (R$),
    /// __Acrescimos ("Free=20;Essencial=15;..." em R$ por plano) e __Pacotes ("30;60;150;300").
    /// Preco de cada consulta = custo ZapCar + acrescimo fixo (R$) do plano da loja,
    /// a menos que a tela de precos (admin) tenha um valor fixo para o servico/plano.
    /// Sem a chave, a consulta fica desligada.
    /// </summary>
    public class ZapCarSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.zapcarconsulta.com.br/v1";

        /// <summary>Acrescimo (R$) sobre o custo quando o plano nao tem acrescimo proprio.</summary>
        public decimal AcrescimoPadrao { get; set; } = 10m;

        /// <summary>Acrescimo (R$) por nome de plano, separado por ";".</summary>
        public string Acrescimos { get; set; } = "Free=20;Essencial=15;Profissional=10;Premium=5";

        /// <summary>Valores de recarga por Pix, separados por ";".</summary>
        public string Pacotes { get; set; } = "30;60;150;300";

        public bool Configurado => !string.IsNullOrWhiteSpace(ApiKey);
        public bool Teste => ApiKey.Trim().StartsWith("zc_test_", StringComparison.Ordinal);

        public decimal AcrescimoDoPlano(string? plano)
        {
            foreach (var par in (Acrescimos ?? "").Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = par.Split('=', 2, StringSplitOptions.TrimEntries);
                if (kv.Length == 2 && string.Equals(kv[0], plano, StringComparison.OrdinalIgnoreCase)
                    && decimal.TryParse(kv[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var m) && m >= 0)
                    return m;
            }
            return AcrescimoPadrao;
        }

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
