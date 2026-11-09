using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;

namespace ConnectVeiculos.Infrastructure.Services.Meta
{
    /// <summary>
    /// Legenda dos posts do feed (Instagram e Facebook) a partir de um modelo
    /// com variaveis, como "{marca} {modelo} por {preco}".
    ///
    /// A legenda padrao tambem e' um modelo (<see cref="Padrao"/>): assim a
    /// loja parte do texto que ja sai hoje, e o Facebook consegue reescrever a
    /// legenda com o preco novo seja qual for o texto que a loja escolheu.
    ///
    /// Linha cujas variaveis ficaram todas vazias some inteira — "📊 {km}" de
    /// carro sem km cadastrado nao vira um "📊" solto no post.
    /// </summary>
    public static class LegendaPost
    {
        public const string Instagram = "instagram";
        public const string Facebook = "facebook";

        // O Instagram corta a legenda em 2.200 caracteres; o Facebook aceita
        // muito mais, mas o mesmo teto vale para os dois para nao surpreender.
        public const int TamanhoMaximo = 2200;

        private static readonly CultureInfo PtBr = new("pt-BR");
        private static readonly Regex Variavel = new(@"\{([a-zA-Z]+)\}", RegexOptions.Compiled);

        public static readonly IReadOnlyList<VariavelLegenda> Variaveis = new[]
        {
            new VariavelLegenda("marca", "Marca do veículo", "Toyota"),
            new VariavelLegenda("modelo", "Modelo do veículo", "Corolla XEi"),
            new VariavelLegenda("ano", "Ano", "2022"),
            new VariavelLegenda("preco", "Preço de venda", "R$ 89.900,00"),
            new VariavelLegenda("km", "Quilometragem", "35.000 km"),
            new VariavelLegenda("cor", "Cor", "Prata"),
            new VariavelLegenda("opcionais", "Opcionais separados por ·", "Ar · Direção · Multimídia"),
            new VariavelLegenda("link", "Link do carro no catálogo", "https://sualoja.com.br/veiculo/7"),
            new VariavelLegenda("whatsapp", "WhatsApp da loja", "(11) 98888-7777"),
            new VariavelLegenda("loja", "Nome da loja", "Diamante Veículos"),
            new VariavelLegenda("cidade", "Cidade da loja", "São Paulo"),
            new VariavelLegenda("hashtags", "Hashtags da marca, modelo e cidade", "#Toyota #ToyotaCorolla #carros")
        };

        private const string PadraoInstagram =
            "🚗 {marca} {modelo} {ano}\n" +
            "💰 {preco}\n" +
            "📊 {km}\n" +
            "🎨 {cor}\n" +
            "✨ {opcionais}\n" +
            "\n" +
            "👉 Link na bio para mais detalhes!\n" +
            "📲 WhatsApp: {whatsapp}\n" +
            "\n" +
            "{hashtags}";

        private const string PadraoFacebook =
            "🚗 {marca} {modelo} {ano}\n" +
            "💰 {preco}\n" +
            "📊 {km}\n" +
            "🎨 Cor: {cor}\n" +
            "✨ {opcionais}\n" +
            "\n" +
            "📞 Entre em contato pelo link: {link}\n" +
            "📲 WhatsApp: {whatsapp}\n" +
            "\n" +
            "{hashtags}";

        /// <summary>O modelo que vale quando a loja nao escreveu o dela.</summary>
        public static string Padrao(string rede)
            => string.Equals(rede, Facebook, StringComparison.OrdinalIgnoreCase) ? PadraoFacebook : PadraoInstagram;

        /// <summary>
        /// Troca as variaveis pelos dados do carro. Modelo vazio usa o padrao
        /// da rede. Variavel desconhecida fica como foi escrita: e' mais facil a
        /// loja perceber o erro de digitacao no post do que um buraco no texto.
        /// </summary>
        public static string Montar(string? modelo, string rede, Veiculo veiculo, Loja? loja, string? link)
        {
            var texto = string.IsNullOrWhiteSpace(modelo) ? Padrao(rede) : modelo.Replace("\r\n", "\n");
            var valores = Valores(rede, veiculo, loja, link);

            var linhas = new List<string>();
            foreach (var linha in texto.Split('\n'))
            {
                var variaveis = Variavel.Matches(linha).Select(m => m.Groups[1].Value.ToLowerInvariant()).ToList();
                var conhecidas = variaveis.Where(valores.ContainsKey).ToList();
                if (conhecidas.Count > 0 && conhecidas.All(v => string.IsNullOrWhiteSpace(valores[v])))
                    continue;

                linhas.Add(Variavel.Replace(linha, m =>
                    valores.TryGetValue(m.Groups[1].Value.ToLowerInvariant(), out var v) ? v : m.Value).TrimEnd());
            }

            // Linha sumida no meio de um bloco pode deixar duas em branco seguidas.
            var resultado = Regex.Replace(string.Join("\n", linhas), @"\n{3,}", "\n\n").Trim();
            return resultado.Length <= TamanhoMaximo ? resultado : resultado[..TamanhoMaximo];
        }

        private static Dictionary<string, string> Valores(string rede, Veiculo v, Loja? loja, string? link)
        {
            var opcionais = string.IsNullOrWhiteSpace(v.VeiOpcionais)
                ? ""
                : v.VeiOpcionais.Replace(",", " · ").Trim().TrimEnd(' ', '·');
            return new Dictionary<string, string>
            {
                ["marca"] = (v.VeiMarca ?? "").Trim(),
                ["modelo"] = (v.VeiModelo ?? "").Trim(),
                ["ano"] = v.VeiAno > 0 ? v.VeiAno.ToString(CultureInfo.InvariantCulture) : "",
                ["preco"] = v.VeiPreco > 0 ? "R$ " + v.VeiPreco.ToString("N2", PtBr) : "",
                ["km"] = v.VeiKm > 0 ? v.VeiKm.ToString("N0", PtBr) + " km" : "",
                ["cor"] = (v.VeiCor ?? "").Trim(),
                ["opcionais"] = opcionais,
                ["link"] = link ?? "",
                ["whatsapp"] = (loja?.LojWhatsApp ?? "").Trim(),
                ["loja"] = (loja?.LojNome ?? "").Trim(),
                ["cidade"] = (loja?.LojCidade ?? "").Trim(),
                ["hashtags"] = Hashtags(rede, v, loja)
            };
        }

        private static string Hashtags(string rede, Veiculo v, Loja? loja)
        {
            var sb = new StringBuilder();
            sb.Append('#').Append(Tag(v.VeiMarca)).Append(' ');
            sb.Append('#').Append(Tag(v.VeiMarca + NomeDoModelo(v.VeiModelo))).Append(' ');
            var ehFacebook = string.Equals(rede, Facebook, StringComparison.OrdinalIgnoreCase);
            sb.Append(ehFacebook ? "#carros #usados #seminovos" : "#carros #usados #seminovos #carrosanovenda");
            if (!string.IsNullOrWhiteSpace(loja?.LojCidade))
            {
                sb.Append(" #").Append(Tag(loja!.LojCidade));
                if (!ehFacebook) sb.Append(" #").Append(Tag("carros" + loja.LojCidade));
            }
            return sb.ToString();
        }

        // So o nome do modelo, sem a versao: "Civic EXL 2.0 16V 4p Aut." virava
        // #HondaCivicEXL2016V4pAut, e o "2016" parecia o ano. Hashtag que as
        // pessoas seguem e' #HondaCivic.
        private static string NomeDoModelo(string? modelo)
        {
            if (string.IsNullOrWhiteSpace(modelo)) return "";
            return modelo.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        }

        private static string Tag(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "carros";
            var sb = new StringBuilder();
            foreach (var c in raw)
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.Length == 0 ? "carros" : sb.ToString();
        }
    }
}
