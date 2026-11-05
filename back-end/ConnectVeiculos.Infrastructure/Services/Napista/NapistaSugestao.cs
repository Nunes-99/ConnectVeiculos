using System.Globalization;
using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Napista;
using ConnectVeiculos.Infrastructure.Services.Webmotors;

namespace ConnectVeiculos.Infrastructure.Services.Napista
{
    /// <summary>
    /// Palpite da versao e dos valores do NaPista a partir do cadastro do
    /// veiculo, que vem da FIPE ("VW - VolksWagen", "GOL 1.0 Mi Total Flex 8V 4p").
    /// A tela mostra o palpite para o usuario conferir antes de anunciar.
    /// Mesmo criterio da Webmotors (WebmotorsSugestao), com ids em texto.
    /// </summary>
    public static class NapistaSugestao
    {
        private static readonly Dictionary<string, string> AliasMarca = new()
        {
            ["VW"] = "VOLKSWAGEN",
            ["GM"] = "CHEVROLET",
            ["MERCEDES"] = "MERCEDES BENZ",
            ["LR"] = "LAND ROVER",
        };

        private static string N(string? texto) => WebmotorsSugestao.Normalizar(texto);

        private static string[] Palavras(string normalizado) => normalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        public static NapistaItem? Marca(string? veiMarca, IEnumerable<NapistaItem> marcas)
        {
            if (string.IsNullOrWhiteSpace(veiMarca)) return null;
            var candidatos = veiMarca.Split(" - ").Append(veiMarca)
                .Select(N).Where(c => c != "")
                .Select(c => AliasMarca.TryGetValue(c, out var a) ? a : c)
                .ToList();
            var lista = marcas.ToList();
            return lista.FirstOrDefault(m => candidatos.Contains(N(m.Nome)) || candidatos.Contains(N(m.Id)))
                   ?? lista.Where(m => N(m.Nome).Length >= 3 && candidatos.Any(c => (" " + c + " ").Contains(" " + N(m.Nome) + " ")))
                           .OrderByDescending(m => m.Nome.Length).FirstOrDefault();
        }

        /// <summary>Modelo cujo nome e' o maior inicio do modelo FIPE ("ARGO" em "ARGO DRIVE 1.0").</summary>
        public static NapistaItem? Modelo(string? veiModelo, IEnumerable<NapistaItem> modelos)
        {
            var alvo = N(veiModelo);
            if (alvo == "") return null;
            return modelos
                .Select(m => (m, n: N(m.Nome)))
                .Where(x => x.n != "" && (alvo == x.n || alvo.StartsWith(x.n + " ")))
                .OrderByDescending(x => x.n.Length)
                .Select(x => x.m)
                .FirstOrDefault();
        }

        /// <summary>Versao com mais palavras em comum com o modelo FIPE (tirando o nome do modelo).</summary>
        public static NapistaItem? Versao(string? veiModelo, NapistaItem? modelo, IEnumerable<NapistaItem> versoes)
        {
            var alvo = N(veiModelo);
            if (modelo != null && alvo.StartsWith(N(modelo.Nome))) alvo = alvo[N(modelo.Nome).Length..];
            alvo = Regex.Replace(" " + alvo + " ", @" AUT\.? ", " AUTOMATICO ");
            var palavras = Palavras(alvo).ToHashSet();
            if (palavras.Count == 0) return null;
            return versoes
                .Select(v => (v, pontos: Palavras(N(v.Nome)).Count(palavras.Contains)))
                .Where(x => x.pontos > 0)
                .OrderByDescending(x => x.pontos).ThenBy(x => x.v.Nome.Length)
                .Select(x => x.v)
                .FirstOrDefault();
        }

        /// <summary>Id do combustivel (enum do NaPista) a partir do texto da versao/modelo.</summary>
        public static string? Combustivel(string? texto)
        {
            var t = " " + N(texto) + " ";
            return t.Contains(" HIBRIDO ") ? "HYBRID" :
                   t.Contains(" ELETRICO ") ? "ELECTRIC" :
                   t.Contains(" FLEX ") || t.Contains(" TOTALFLEX ") || (t.Contains(" GASOLINA ") && t.Contains(" ALCOOL ")) ? "FLEX" :
                   t.Contains(" DIESEL ") ? "DIESEL" :
                   t.Contains(" ALCOOL ") || t.Contains(" ETANOL ") ? "ALCOHOL" :
                   t.Contains(" GASOLINA ") ? "GASOLINE" : null;
        }

        /// <summary>MANUAL ou AUTOMATIC (o NaPista nao separa CVT/automatizado).</summary>
        public static string? Cambio(string? texto)
        {
            var t = " " + N(texto) + " ";
            if (t.Contains(" AUT ") || t.Contains(" AUT. ") || t.Contains(" AUTOMATICO ") || t.Contains(" AUTOMATICA ")
                || t.Contains(" AUTOMATIZADO ") || t.Contains(" AUTOMATIZADA ") || t.Contains(" CVT ") || t.Contains(" DCT "))
                return "AUTOMATIC";
            return t.Contains(" MANUAL ") || t.Contains(" MEC ") || t.Contains(" MEC. ") ? "MANUAL" : null;
        }

        /// <summary>"4P"/"5p" no nome do modelo.</summary>
        public static int? Portas(string? texto)
        {
            var m = Regex.Match(" " + N(texto) + " ", @" ([2-5])P ");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
        }

        /// <summary>Cor pelo nome em portugues que o NaPista devolve ("Prata" -> SILVER).</summary>
        public static string? Cor(string? veiCor, IEnumerable<NapistaItem> cores)
        {
            var alvo = N(veiCor);
            if (alvo == "") return null;
            var lista = cores.ToList();
            return (lista.FirstOrDefault(c => N(c.Nome) == alvo)
                    ?? lista.FirstOrDefault(c => N(c.Nome).Length >= 4 && (" " + alvo + " ").Contains(" " + N(c.Nome) + " ")))?.Id;
        }

        /// <summary>Opcionais do cadastro (texto livre) que batem com o nome de um opcional do NaPista.</summary>
        public static List<string> Opcionais(string? veiOpcionais, IEnumerable<NapistaItem> opcionais)
        {
            if (string.IsNullOrWhiteSpace(veiOpcionais)) return new();
            var itens = veiOpcionais.Split(new[] { ',', ';', '\n', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(N).Where(x => x.Length >= 3).ToHashSet();
            return opcionais.Where(o => itens.Contains(N(o.Nome))).Select(o => o.Id).Distinct().ToList();
        }
    }
}
