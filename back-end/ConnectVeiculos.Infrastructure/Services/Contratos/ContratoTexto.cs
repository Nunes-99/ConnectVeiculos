using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Contratos;

namespace ConnectVeiculos.Infrastructure.Services.Contratos
{
    /// <summary>
    /// Texto dos contratos: troca as variaveis {{grupo.nome}} pelos valores e
    /// quebra a marcacao em blocos ("# " titulo, "## " secao, paragrafos
    /// separados por linha em branco, **negrito**). A tela, o link publico e o
    /// PDF usam os mesmos blocos, entao o que se ve e o que se assina.
    /// </summary>
    public static class ContratoTexto
    {
        private static readonly CultureInfo PtBr = new("pt-BR");
        private static readonly Regex Variavel = new(@"\{\{\s*([a-z_]+\.[a-z0-9_]+)\s*\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Extra = new(@"\{\{\s*extra\.([a-z0-9_]+)\s*\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Marca, no texto ja preenchido, onde ficou faltando valor.</summary>
        public const string MarcaFaltando = "⁣";
        public const string Lacuna = "________________";

        public static readonly IReadOnlyList<ContratoVariavel> Variaveis = new List<ContratoVariavel>
        {
            V("loja.nome", "Razão social ou nome da loja", "Loja"),
            V("loja.cnpj", "CNPJ da loja", "Loja"),
            V("loja.endereco", "Endereço completo da loja", "Loja"),
            V("loja.cidade", "Cidade da loja", "Loja"),
            V("loja.uf", "UF da loja", "Loja"),
            V("loja.telefone", "Telefone da loja", "Loja"),
            V("loja.email", "E-mail da loja", "Loja"),
            V("veiculo.marca", "Marca", "Veículo"),
            V("veiculo.modelo", "Modelo", "Veículo"),
            V("veiculo.ano", "Ano", "Veículo"),
            V("veiculo.cor", "Cor", "Veículo"),
            V("veiculo.placa", "Placa", "Veículo"),
            V("veiculo.chassi", "Chassi", "Veículo"),
            V("veiculo.renavam", "Renavam", "Veículo"),
            V("veiculo.km", "Quilometragem", "Veículo"),
            V("veiculo.preco", "Preço anunciado", "Veículo"),
            V("veiculo.opcionais", "Opcionais", "Veículo"),
            V("cliente.papel", "Comprador, vendedor ou consignante", "Cliente"),
            V("cliente.nome", "Nome do cliente", "Cliente"),
            V("cliente.documento", "CPF ou CNPJ do cliente", "Cliente"),
            V("cliente.email", "E-mail do cliente", "Cliente"),
            V("cliente.telefone", "Telefone do cliente", "Cliente"),
            V("cliente.endereco", "Endereço do cliente", "Cliente"),
            V("contrato.valor", "Valor do negócio", "Contrato"),
            V("contrato.valor_extenso", "Valor por extenso", "Contrato"),
            V("contrato.forma_pagamento", "Forma de pagamento", "Contrato"),
            V("data.hoje", "Data de hoje (04/11/2026)", "Data"),
            V("data.extenso", "Data por extenso (4 de novembro de 2026)", "Data"),
            V("extra.nome_do_campo", "Campo livre: vira um campo para preencher na hora de gerar", "Campos livres")
        };

        private static ContratoVariavel V(string chave, string descricao, string grupo)
            => new() { Chave = chave, Descricao = descricao, Grupo = grupo };

        /// <summary>Campos livres ({{extra.x}}) do modelo, na ordem em que aparecem.</summary>
        public static List<ContratoExtra> Extras(string conteudo)
            => Extra.Matches(conteudo ?? "")
                .Select(m => m.Groups[1].Value.ToLowerInvariant())
                .Distinct()
                .Select(k => new ContratoExtra { Chave = k, Rotulo = Rotulo(k) })
                .ToList();

        private static string Rotulo(string chave)
        {
            var t = chave.Replace('_', ' ').Trim();
            return t.Length == 0 ? chave : char.ToUpper(t[0], PtBr) + t[1..];
        }

        /// <summary>
        /// Troca as variaveis. Variavel sem valor vira lacuna marcada, e a
        /// chave entra em <paramref name="faltando"/>.
        /// </summary>
        public static string Preencher(string conteudo, IReadOnlyDictionary<string, string?> valores, List<string> faltando)
            => Variavel.Replace(conteudo ?? "", m =>
            {
                var chave = m.Groups[1].Value.ToLowerInvariant();
                if (valores.TryGetValue(chave, out var v) && !string.IsNullOrWhiteSpace(v)) return v!.Trim();
                if (!faltando.Contains(chave)) faltando.Add(chave);
                return MarcaFaltando + Lacuna + MarcaFaltando;
            });

        /// <summary>Quebra o texto ja preenchido em blocos.</summary>
        public static List<ContratoBloco> Blocos(string texto)
        {
            var blocos = new List<ContratoBloco>();
            var paragrafo = new List<string>();

            void FecharParagrafo()
            {
                if (paragrafo.Count == 0) return;
                blocos.Add(new ContratoBloco { Tipo = "paragrafo", Trechos = Trechos(string.Join(" ", paragrafo)) });
                paragrafo.Clear();
            }

            foreach (var bruta in (texto ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var linha = bruta.Trim();
                if (linha.Length == 0) { FecharParagrafo(); continue; }
                if (linha.StartsWith("## "))
                {
                    FecharParagrafo();
                    blocos.Add(new ContratoBloco { Tipo = "secao", Trechos = Trechos(linha[3..].Trim()) });
                }
                else if (linha.StartsWith("# "))
                {
                    FecharParagrafo();
                    blocos.Add(new ContratoBloco { Tipo = "titulo", Trechos = Trechos(linha[2..].Trim()) });
                }
                else paragrafo.Add(linha);
            }
            FecharParagrafo();
            return blocos;
        }

        /// <summary>Divide em trechos normais, em **negrito** e lacunas.</summary>
        private static List<ContratoTrecho> Trechos(string texto)
        {
            var trechos = new List<ContratoTrecho>();
            var partes = texto.Split("**");
            for (var i = 0; i < partes.Length; i++)
            {
                var negrito = i % 2 == 1 && i < partes.Length - 1;
                var pedacos = partes[i].Split(MarcaFaltando);
                for (var j = 0; j < pedacos.Length; j++)
                {
                    if (pedacos[j].Length == 0) continue;
                    trechos.Add(new ContratoTrecho { Texto = pedacos[j], Negrito = negrito, Faltando = j % 2 == 1 });
                }
            }
            return trechos;
        }

        /// <summary>Titulo do contrato: o primeiro "# " do texto, ou o nome do modelo.</summary>
        public static string Titulo(List<ContratoBloco> blocos, string padrao)
        {
            var t = blocos.FirstOrDefault(b => b.Tipo == "titulo");
            return t == null ? padrao : string.Concat(t.Trechos.Select(x => x.Texto)).Trim();
        }

        public static string Hash(string texto)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();

        public static string Moeda(decimal v) => v.ToString("C", PtBr);

        public static string DataExtenso(DateTime d) => d.ToString("d 'de' MMMM 'de' yyyy", PtBr);

        // ================================================================
        // Valor por extenso (reais e centavos)
        // ================================================================

        private static readonly string[] Unidades =
            { "", "um", "dois", "três", "quatro", "cinco", "seis", "sete", "oito", "nove", "dez", "onze", "doze",
              "treze", "quatorze", "quinze", "dezesseis", "dezessete", "dezoito", "dezenove" };
        private static readonly string[] Dezenas =
            { "", "", "vinte", "trinta", "quarenta", "cinquenta", "sessenta", "setenta", "oitenta", "noventa" };
        private static readonly string[] Centenas =
            { "", "cento", "duzentos", "trezentos", "quatrocentos", "quinhentos", "seiscentos", "setecentos", "oitocentos", "novecentos" };

        public static string Extenso(decimal valor)
        {
            valor = Math.Round(Math.Abs(valor), 2);
            var reais = (long)Math.Floor(valor);
            var centavos = (int)Math.Round((valor - reais) * 100);

            var partes = new List<string>();
            if (reais > 0)
            {
                var texto = Inteiro(reais);
                // "um milhao de reais", "dois milhoes de reais": de antes de reais em milhao/bilhao redondo.
                var de = reais >= 1_000_000 && reais % 1_000_000 == 0 ? " de" : "";
                partes.Add(texto + de + (reais == 1 ? " real" : " reais"));
            }
            if (centavos > 0) partes.Add(Inteiro(centavos) + (centavos == 1 ? " centavo" : " centavos"));
            return partes.Count == 0 ? "zero real" : string.Join(" e ", partes);
        }

        private static string Inteiro(long n)
        {
            if (n == 0) return "zero";
            var grupos = new List<(int Valor, string Singular, string Plural)>();
            var escalas = new[] { ("", ""), ("mil", "mil"), ("milhão", "milhões"), ("bilhão", "bilhões") };
            var i = 0;
            while (n > 0 && i < escalas.Length)
            {
                grupos.Add(((int)(n % 1000), escalas[i].Item1, escalas[i].Item2));
                n /= 1000;
                i++;
            }

            // Do maior grupo para o menor. Liga com "e" quando o grupo seguinte e
            // menor que 100 ou centena redonda: "mil e vinte", "mil e novecentos",
            // mas "mil duzentos e trinta".
            var sb = new StringBuilder();
            for (var g = grupos.Count - 1; g >= 0; g--)
            {
                var (v, sing, plur) = grupos[g];
                if (v == 0) continue;
                var txt = g == 1 && v == 1 ? "mil" : Centena(v) + (g == 0 ? "" : " " + (v == 1 ? sing : plur));
                if (sb.Length > 0) sb.Append(v < 100 || v % 100 == 0 ? " e " : " ");
                sb.Append(txt);
            }
            return sb.ToString();
        }

        private static string Centena(int n)
        {
            if (n == 100) return "cem";
            var c = n / 100;
            var resto = n % 100;
            var partes = new List<string>();
            if (c > 0) partes.Add(Centenas[c]);
            if (resto > 0)
            {
                if (resto < 20) partes.Add(Unidades[resto]);
                else
                {
                    var d = resto / 10;
                    var u = resto % 10;
                    partes.Add(u == 0 ? Dezenas[d] : $"{Dezenas[d]} e {Unidades[u]}");
                }
            }
            return string.Join(" e ", partes);
        }
    }
}
