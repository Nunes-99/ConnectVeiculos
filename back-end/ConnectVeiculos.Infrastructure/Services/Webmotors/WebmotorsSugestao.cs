using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Webmotors;

namespace ConnectVeiculos.Infrastructure.Services.Webmotors
{
    /// <summary>
    /// Palpite dos codigos da Webmotors a partir do cadastro do veiculo, que
    /// vem da FIPE ("VW - VolksWagen", "GOL 1.0 Mi Total Flex 8V 4p"). A tela
    /// mostra o palpite para o usuario conferir antes de anunciar.
    /// </summary>
    public static class WebmotorsSugestao
    {
        private static readonly Dictionary<string, string> AliasMarca = new()
        {
            ["VW"] = "VOLKSWAGEN",
            ["GM"] = "CHEVROLET",
            ["MERCEDES"] = "MERCEDES BENZ",
            ["LR"] = "LAND ROVER",
        };

        public static string Normalizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            var sem = new StringBuilder();
            foreach (var c in texto.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sem.Append(char.IsLetterOrDigit(c) || c == '.' ? char.ToUpperInvariant(c) : ' ');
            return Regex.Replace(sem.ToString(), @"\s+", " ").Trim();
        }

        private static string[] Palavras(string normalizado) =>
            normalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        public static WebmotorsItem? Marca(string? veiMarca, IEnumerable<WebmotorsItem> marcas)
        {
            if (string.IsNullOrWhiteSpace(veiMarca)) return null;
            // FIPE: "VW - VolksWagen", "GM - Chevrolet": cada lado do hifen e' candidato.
            var candidatos = veiMarca.Split(" - ").Append(veiMarca)
                .Select(Normalizar).Where(c => c != "")
                .Select(c => AliasMarca.TryGetValue(c, out var a) ? a : c)
                .ToList();
            var lista = marcas.ToList();
            return lista.FirstOrDefault(m => candidatos.Contains(Normalizar(m.Nome)))
                   ?? lista.Where(m => Normalizar(m.Nome).Length >= 3 && candidatos.Any(c => (" " + c + " ").Contains(" " + Normalizar(m.Nome) + " ")))
                           .OrderByDescending(m => m.Nome.Length).FirstOrDefault();
        }

        /// <summary>Modelo cujo nome e' o maior inicio do modelo FIPE ("ONIX" em "ONIX HATCH LT 1.0").</summary>
        public static WebmotorsItem? Modelo(string? veiModelo, IEnumerable<WebmotorsItem> modelos)
        {
            var alvo = Normalizar(veiModelo);
            if (alvo == "") return null;
            return modelos
                .Select(m => (m, n: Normalizar(m.Nome)))
                .Where(x => x.n != "" && (alvo == x.n || alvo.StartsWith(x.n + " ")))
                .OrderByDescending(x => x.n.Length)
                .Select(x => x.m)
                .FirstOrDefault();
        }

        /// <summary>Versao do ano com mais palavras em comum com o modelo FIPE.</summary>
        public static WebmotorsVersao? Versao(string? veiModelo, WebmotorsItem? modelo, int ano, IEnumerable<WebmotorsVersao> versoes)
        {
            var alvo = Normalizar(veiModelo);
            if (modelo != null && alvo.StartsWith(Normalizar(modelo.Nome)))
                alvo = alvo[Normalizar(modelo.Nome).Length..];
            var palavras = Palavras(Expandir(alvo)).ToHashSet();
            if (palavras.Count == 0) return null;

            var lista = versoes.ToList();
            var doAno = lista.Where(v => v.Anos.Count == 0 || v.Anos.Contains(ano)).ToList();
            if (doAno.Count > 0) lista = doAno;

            return lista
                .Select(v => (v, pontos: Palavras(Normalizar(v.Nome)).Count(palavras.Contains)))
                .Where(x => x.pontos > 0)
                .OrderByDescending(x => x.pontos).ThenBy(x => x.v.Nome.Length)
                .Select(x => x.v)
                .FirstOrDefault();
        }

        /// <summary>Abreviacoes da FIPE para as palavras das versoes da Webmotors.</summary>
        private static string Expandir(string normalizado)
        {
            var t = " " + normalizado + " ";
            t = Regex.Replace(t, @" AUT\.? ", " AUTOMATICO ");
            t = Regex.Replace(t, @" MEC\.? ", " MANUAL ");
            return t.Trim();
        }

        /// <summary>Nome do combustivel da Webmotors para o texto da versao/modelo.</summary>
        public static WebmotorsItem? Combustivel(string? texto, IEnumerable<WebmotorsItem> combustiveis)
        {
            var t = " " + Normalizar(texto) + " ";
            string? alvo =
                t.Contains(" HIBRIDO ") || (t.Contains(" ELETRICO ") && t.Contains(" GASOLINA ")) ? "GASOLINA E ELETRICO" :
                t.Contains(" ELETRICO ") ? "ELETRICO" :
                t.Contains(" FLEX ") || (t.Contains(" GASOLINA ") && t.Contains(" ALCOOL ")) || t.Contains(" TOTALFLEX ") ? "GASOLINA E ALCOOL" :
                t.Contains(" DIESEL ") ? "DIESEL" :
                t.Contains(" ALCOOL ") || t.Contains(" ETANOL ") ? "ALCOOL" :
                t.Contains(" GASOLINA ") ? "GASOLINA" : null;
            return alvo == null ? null : combustiveis.FirstOrDefault(c => Normalizar(c.Nome) == alvo);
        }

        public static WebmotorsItem? Cambio(string? texto, IEnumerable<WebmotorsItem> cambios)
        {
            var t = " " + Normalizar(texto) + " ";
            string? alvo =
                t.Contains(" CVT ") ? "CVT" :
                t.Contains(" DCT ") ? "AUTOMATIZADA DCT" :
                t.Contains(" AUTOMATIZADO ") || t.Contains(" AUTOMATIZADA ") ? "AUTOMATIZADA" :
                t.Contains(" AUTOMATICO ") || t.Contains(" AUTOMATICA ") || t.Contains(" AUT ") || t.Contains(" AUT. ") ? "AUTOMATICA" :
                t.Contains(" MANUAL ") || t.Contains(" MEC ") || t.Contains(" MEC. ") ? "MANUAL" : null;
            return alvo == null ? null : cambios.FirstOrDefault(c => Normalizar(c.Nome) == alvo);
        }

        /// <summary>"4P"/"5p" no nome. Hatch de 5 portas a Webmotors cadastra como 4.</summary>
        public static int? Portas(string? texto)
        {
            var m = Regex.Match(" " + Normalizar(texto) + " ", @" ([2-5])P ");
            if (!m.Success) return null;
            var n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            return n == 5 ? 4 : n;
        }

        public static WebmotorsItem? Cor(string? veiCor, IEnumerable<WebmotorsItem> cores)
        {
            var alvo = Normalizar(veiCor);
            if (alvo == "") return null;
            var lista = cores.ToList();
            return lista.FirstOrDefault(c => Normalizar(c.Nome) == alvo)
                   ?? lista.FirstOrDefault(c => (alvo + " ").StartsWith(Normalizar(c.Nome) + " "))
                   ?? lista.FirstOrDefault(c => Normalizar(c.Nome) != "" && (" " + alvo + " ").Contains(" " + Normalizar(c.Nome) + " "));
        }

        /// <summary>Opcionais do cadastro ("Ar condicionado,Alarme") que existem na Webmotors.</summary>
        public static List<long> Opcionais(string? veiOpcionais, IEnumerable<WebmotorsItem> opcionais)
        {
            var nossos = (veiOpcionais ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalizar).Where(o => o != "").ToHashSet();
            if (nossos.Count == 0) return new();
            return opcionais.Where(o => nossos.Contains(Normalizar(o.Nome))).Select(o => o.Codigo).Distinct().ToList();
        }
    }
}
