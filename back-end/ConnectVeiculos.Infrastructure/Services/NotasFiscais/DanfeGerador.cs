using System.Globalization;
using System.Xml.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ConnectVeiculos.Infrastructure.Services.NotasFiscais
{
    /// <summary>
    /// DANFE (retrato, A4) a partir do XML autorizado (nfeProc). Nota de
    /// veiculo tem um item so, entao cabe sempre em uma folha.
    /// </summary>
    public static class DanfeGerador
    {
        private static readonly XNamespace Ns = "http://www.portalfiscal.inf.br/nfe";
        private static readonly CultureInfo PtBr = new("pt-BR");

        static DanfeGerador()
        {
            // Licenca Community: gratuita para empresas com receita anual abaixo de US$ 1 milhao.
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public static byte[] Gerar(string xmlProc, bool cancelada = false)
            => Documento(xmlProc, cancelada).GeneratePdf();

        /// <summary>PNG da folha (conferencia visual e testes).</summary>
        public static byte[] GerarImagem(string xmlProc, bool cancelada = false)
            => Documento(xmlProc, cancelada).GenerateImages(new ImageGenerationSettings { RasterDpi = 110 }).First();

        private static IDocument Documento(string xmlProc, bool cancelada)
        {
            var doc = XDocument.Parse(xmlProc);
            var inf = doc.Descendants(Ns + "infNFe").First();
            var ide = inf.Element(Ns + "ide")!;
            var emit = inf.Element(Ns + "emit")!;
            var dest = inf.Element(Ns + "dest");
            var tot = inf.Element(Ns + "total")!.Element(Ns + "ICMSTot")!;
            var prot = doc.Descendants(Ns + "infProt").FirstOrDefault();
            var chave = ((string?)inf.Attribute("Id") ?? "").Replace("NFe", "");
            var homologacao = V(ide, "tpAmb") == "2";

            return Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18);
                page.DefaultTextStyle(t => t.FontSize(7));

                if (homologacao || cancelada)
                {
                    page.Background().AlignCenter().AlignMiddle().Rotate(-30)
                        .Text(cancelada ? "NF-e CANCELADA" : "SEM VALOR FISCAL")
                        .FontSize(54).Bold().FontColor(Colors.Red.Lighten3);
                }

                page.Content().Column(col =>
                {
                    col.Spacing(4);

                    // Canhoto
                    col.Item().Border(0.6f).Row(r =>
                    {
                        r.RelativeItem(4).Padding(3).Column(c =>
                        {
                            c.Item().Text($"RECEBEMOS DE {V(emit, "xNome")} O PRODUTO CONSTANTE DA NOTA FISCAL INDICADA AO LADO").FontSize(6);
                            c.Item().PaddingTop(10).Row(rr =>
                            {
                                rr.RelativeItem().Text("DATA DE RECEBIMENTO").FontSize(5);
                                rr.RelativeItem(2).Text("IDENTIFICAÇÃO E ASSINATURA DO RECEBEDOR").FontSize(5);
                            });
                        });
                        r.RelativeItem(1).BorderLeft(0.6f).Padding(3).AlignCenter().Column(c =>
                        {
                            c.Item().Text("NF-e").Bold().FontSize(10);
                            c.Item().Text($"Nº {Numero(V(ide, "nNF"))}").Bold();
                            c.Item().Text($"SÉRIE {V(ide, "serie")}");
                        });
                    });

                    // Cabecalho: emitente | DANFE | codigo de barras
                    col.Item().Border(0.6f).Row(r =>
                    {
                        r.RelativeItem(4).Padding(4).Column(c =>
                        {
                            c.Item().Text(V(emit, "xNome")).Bold().FontSize(9);
                            var end = emit.Element(Ns + "enderEmit");
                            c.Item().Text($"{V(end, "xLgr")}, {V(end, "nro")} {V(end, "xCpl")}");
                            c.Item().Text($"{V(end, "xBairro")} - {V(end, "xMun")}/{V(end, "UF")} - CEP {Cep(V(end, "CEP"))}");
                            if (V(end, "fone") is { Length: > 0 } fone) c.Item().Text($"Fone: {fone}");
                        });
                        r.RelativeItem(2).BorderLeft(0.6f).BorderRight(0.6f).Padding(4).AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().Text("DANFE").Bold().FontSize(12);
                            c.Item().AlignCenter().Text("Documento Auxiliar da Nota Fiscal Eletrônica").FontSize(6);
                            c.Item().PaddingTop(3).AlignCenter().Text($"{(V(ide, "tpNF") == "0" ? "0 - ENTRADA" : "1 - SAÍDA")}").Bold();
                            c.Item().AlignCenter().Text($"Nº {Numero(V(ide, "nNF"))}").Bold();
                            c.Item().AlignCenter().Text($"SÉRIE {V(ide, "serie")}   FOLHA 1/1");
                        });
                        r.RelativeItem(5).Padding(4).Column(c =>
                        {
                            c.Item().Height(32).Element(e => CodigoBarras(e, chave));
                            c.Item().PaddingTop(3).Text("CHAVE DE ACESSO").FontSize(5);
                            c.Item().Text(ChaveFormatada(chave)).Bold().FontSize(8);
                            c.Item().PaddingTop(2).Text("Consulta de autenticidade no portal nacional da NF-e www.nfe.fazenda.gov.br/portal ou no site da Sefaz autorizadora").FontSize(5);
                        });
                    });

                    col.Item().Row(r =>
                    {
                        Campo(r.RelativeItem(3), "NATUREZA DA OPERAÇÃO", V(ide, "natOp"));
                        Campo(r.RelativeItem(2), "PROTOCOLO DE AUTORIZAÇÃO DE USO",
                            prot == null ? "-" : $"{V(prot, "nProt")} - {DataHora(V(prot, "dhRecbto"))}");
                    });
                    col.Item().Row(r =>
                    {
                        Campo(r.RelativeItem(), "INSCRIÇÃO ESTADUAL", V(emit, "IE"));
                        Campo(r.RelativeItem(), "CNPJ", Documento(V(emit, "CNPJ")));
                    });

                    // Destinatario
                    Titulo(col, "DESTINATÁRIO / REMETENTE");
                    var endDest = dest?.Element(Ns + "enderDest");
                    col.Item().Row(r =>
                    {
                        Campo(r.RelativeItem(4), "NOME / RAZÃO SOCIAL", V(dest, "xNome"));
                        Campo(r.RelativeItem(2), "CNPJ / CPF", Documento(V(dest, "CNPJ") is { Length: > 0 } c ? c : V(dest, "CPF")));
                        Campo(r.RelativeItem(1.3f), "DATA DA EMISSÃO", Data(V(ide, "dhEmi")));
                    });
                    col.Item().Row(r =>
                    {
                        Campo(r.RelativeItem(4), "ENDEREÇO", $"{V(endDest, "xLgr")}, {V(endDest, "nro")} {V(endDest, "xCpl")}");
                        Campo(r.RelativeItem(2), "BAIRRO / DISTRITO", V(endDest, "xBairro"));
                        Campo(r.RelativeItem(1.3f), "CEP", Cep(V(endDest, "CEP")));
                    });
                    col.Item().Row(r =>
                    {
                        Campo(r.RelativeItem(4), "MUNICÍPIO", V(endDest, "xMun"));
                        Campo(r.RelativeItem(1), "UF", V(endDest, "UF"));
                        Campo(r.RelativeItem(2.3f), "INSCRIÇÃO ESTADUAL", V(dest, "IE"));
                    });

                    // Calculo do imposto
                    Titulo(col, "CÁLCULO DO IMPOSTO");
                    col.Item().Row(r =>
                    {
                        Campo(r.RelativeItem(), "BASE DE CÁLC. DO ICMS", Valor(V(tot, "vBC")));
                        Campo(r.RelativeItem(), "VALOR DO ICMS", Valor(V(tot, "vICMS")));
                        Campo(r.RelativeItem(), "V. TOTAL PRODUTOS", Valor(V(tot, "vProd")));
                        Campo(r.RelativeItem(), "VALOR DO FRETE", Valor(V(tot, "vFrete")));
                        Campo(r.RelativeItem(), "VALOR TOTAL DA NOTA", Valor(V(tot, "vNF")), negrito: true);
                    });

                    Titulo(col, "TRANSPORTADOR / VOLUMES TRANSPORTADOS");
                    col.Item().Row(r => Campo(r.RelativeItem(), "MODALIDADE DO FRETE",
                        V(inf.Element(Ns + "transp"), "modFrete") == "9" ? "9 - Sem ocorrência de transporte" : V(inf.Element(Ns + "transp"), "modFrete")));

                    // Produtos
                    Titulo(col, "DADOS DO PRODUTO / SERVIÇO");
                    col.Item().Border(0.6f).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(34); c.RelativeColumn(); c.ConstantColumn(40); c.ConstantColumn(26);
                            c.ConstantColumn(26); c.ConstantColumn(20); c.ConstantColumn(24); c.ConstantColumn(52);
                            c.ConstantColumn(52); c.ConstantColumn(44); c.ConstantColumn(40); c.ConstantColumn(26);
                        });
                        foreach (var h in new[] { "CÓDIGO", "DESCRIÇÃO DO PRODUTO", "NCM/SH", "CST", "CFOP", "UN", "QTD", "V. UNIT.", "V. TOTAL", "BC ICMS", "V. ICMS", "ALÍQ." })
                            t.Cell().BorderBottom(0.6f).Padding(2).Text(h).FontSize(5).Bold();

                        foreach (var det in inf.Elements(Ns + "det"))
                        {
                            var prod = det.Element(Ns + "prod")!;
                            var icms = det.Element(Ns + "imposto")?.Element(Ns + "ICMS")?.Elements().FirstOrDefault();
                            var cst = V(icms, "CSOSN") is { Length: > 0 } csosn ? csosn : V(icms, "orig") + V(icms, "CST");
                            foreach (var v in new[]
                            {
                                V(prod, "cProd"), V(prod, "xProd"), V(prod, "NCM"), cst, V(prod, "CFOP"), V(prod, "uCom"),
                                Quantidade(V(prod, "qCom")), Valor(V(prod, "vUnCom")), Valor(V(prod, "vProd")),
                                Valor(V(icms, "vBC")), Valor(V(icms, "vICMS")), Valor(V(icms, "pICMS"))
                            })
                                t.Cell().Padding(2).Text(v).FontSize(6);
                        }
                    });

                    // Dados adicionais
                    Titulo(col, "DADOS ADICIONAIS");
                    col.Item().Border(0.6f).MinHeight(70).Padding(3).Column(c =>
                    {
                        c.Item().Text("INFORMAÇÕES COMPLEMENTARES").FontSize(5);
                        c.Item().Text(V(inf.Element(Ns + "infAdic"), "infCpl"));
                        if (homologacao) c.Item().PaddingTop(4).Text("NF-e emitida em ambiente de homologação - sem valor fiscal.").Bold();
                    });
                });

                page.Footer().AlignRight().Text("Emitido pelo Gericar").FontSize(5).FontColor(Colors.Grey.Medium);
            }));
        }

        private static void CodigoBarras(IContainer e, string chave)
        {
            if (chave.Length != 44)
            {
                e.Text("");
                return;
            }
            var larguras = Code128C.Larguras(chave);
            var total = larguras.Sum();
            e.Row(r =>
            {
                var barra = true;
                foreach (var w in larguras)
                {
                    var item = r.RelativeItem(w);
                    if (barra) item.Background(Colors.Black);
                    barra = !barra;
                }
            });
        }

        private static void Titulo(ColumnDescriptor col, string texto)
            => col.Item().PaddingTop(2).Text(texto).FontSize(6).Bold();

        private static void Campo(IContainer c, string rotulo, string valor, bool negrito = false)
            => c.Border(0.6f).Padding(2).Column(x =>
            {
                x.Item().Text(rotulo).FontSize(5);
                var t = x.Item().Text(string.IsNullOrWhiteSpace(valor) ? " " : valor).FontSize(7.5f);
                if (negrito) t.Bold();
            });

        private static string V(XElement? e, string nome) => e?.Element(Ns + nome)?.Value?.Trim() ?? "";

        private static string Numero(string n)
            => int.TryParse(n, out var v) ? v.ToString("000\\.000\\.000", CultureInfo.InvariantCulture) : n;

        public static string ChaveFormatada(string chave)
            => string.Join(" ", Enumerable.Range(0, (chave.Length + 3) / 4).Select(i => chave.Substring(i * 4, Math.Min(4, chave.Length - i * 4))));

        private static string Documento(string d) => d.Length switch
        {
            14 => $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}",
            11 => $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}",
            _ => d
        };

        private static string Cep(string c) => c.Length == 8 ? $"{c[..5]}-{c[5..]}" : c;

        private static string Valor(string v)
            => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d.ToString("N2", PtBr) : v;

        private static string Quantidade(string v)
            => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d.ToString("0.####", PtBr) : v;

        private static string Data(string v)
            => DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd/MM/yyyy") : v;

        private static string DataHora(string v)
            => DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd/MM/yyyy HH:mm:ss") : v;
    }
}
