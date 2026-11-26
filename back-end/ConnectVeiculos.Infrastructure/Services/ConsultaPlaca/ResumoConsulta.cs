using System.Globalization;
using System.Text.Json;

namespace ConnectVeiculos.Infrastructure.Services.ConsultaPlaca
{
    public class AlertaConsulta
    {
        public string Texto { get; set; } = "";
        /// <summary>Impede ou complica a compra (gravame, roubo, leilao...).</summary>
        public bool Grave { get; set; }
    }

    /// <summary>
    /// Le o bloco "veiculo" da ZapCar e diz o que preocupa. Regras do contrato
    /// deles: "ativa" da restricao e' a fonte da verdade; null em leilao,
    /// sinistro e recall = nao verificado (nunca "nada consta").
    /// </summary>
    public static class ResumoConsulta
    {
        private static readonly CultureInfo PtBr = new("pt-BR");

        public static List<AlertaConsulta> Avaliar(string? veiculoJson)
        {
            var alertas = new List<AlertaConsulta>();
            if (string.IsNullOrWhiteSpace(veiculoJson)) return alertas;

            using var doc = JsonDocument.Parse(veiculoJson);
            var v = doc.RootElement;

            if (Bool(v, "baixado") == true)
                alertas.Add(new() { Texto = "Veículo baixado: não pode voltar a circular.", Grave = true });

            if (v.TryGetProperty("restricoes", out var restricoes) && restricoes.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in restricoes.EnumerateArray())
                {
                    if (Bool(r, "ativa") != true) continue;
                    var descricao = Texto(r, "descricao");
                    var texto = Texto(r, "tipo") switch
                    {
                        "FINANCEIRA" => "Gravame (financiamento em aberto)",
                        "RENAJUD" => "Restrição judicial RENAJUD",
                        "JUDICIAL" => "Restrição judicial",
                        "ADMINISTRATIVA" => "Restrição administrativa",
                        "TRIBUTARIA" => "Restrição tributária",
                        "GUINCHO" => "Veículo removido para pátio (guincho)",
                        "ROUBO_FURTO" => "Registro de roubo ou furto ATIVO",
                        "COMUNICACAO_VENDA" => "Comunicação de venda registrada",
                        _ => "Restrição"
                    };
                    alertas.Add(new()
                    {
                        Texto = string.IsNullOrWhiteSpace(descricao) ? texto : $"{texto}: {descricao}",
                        // Comunicacao de venda so diz que o dono informou a venda.
                        Grave = Texto(r, "tipo") != "COMUNICACAO_VENDA"
                    });
                }
            }

            if (v.TryGetProperty("leilao", out var leilao) && leilao.ValueKind == JsonValueKind.Object)
            {
                var ocorrencias = Inteiro(leilao, "ocorrencias") ?? 0;
                var fotos = Inteiro(leilao, "fotos") ?? 0;
                if (Bool(leilao, "consta") == true || ocorrencias > 0 || fotos > 0)
                    alertas.Add(new()
                    {
                        Texto = ocorrencias > 0
                            ? $"Passou por leilão ({ocorrencias} {(ocorrencias == 1 ? "registro" : "registros")})"
                            : "Passou por leilão",
                        Grave = true
                    });
            }

            if (Bool(v, "sinistro") == true)
                alertas.Add(new() { Texto = "Registro de sinistro (batida/perda)", Grave = true });

            if (Bool(v, "recall") == true)
                alertas.Add(new() { Texto = "Recall em aberto", Grave = false });

            var totalDebitos = Inteiro(v, "debitos_total_centavos") ?? 0;
            var semValor = false;
            if (v.TryGetProperty("debitos", out var debitos) && debitos.ValueKind == JsonValueKind.Array)
                semValor = debitos.EnumerateArray().Any(d => Bool(d, "valor_informado") == false);
            if (totalDebitos > 0)
                alertas.Add(new() { Texto = $"Débitos em aberto: {(totalDebitos / 100m).ToString("C", PtBr)}", Grave = false });
            else if (semValor)
                alertas.Add(new() { Texto = "Débitos em aberto (a base não informou o valor)", Grave = false });

            return alertas;
        }

        private static string? Texto(JsonElement e, string p)
            => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static bool? Bool(JsonElement e, string p)
            => e.TryGetProperty(p, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

        private static long? Inteiro(JsonElement e, string p)
            => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;
    }
}
