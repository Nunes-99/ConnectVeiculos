using ConnectVeiculos.Core.Contratos;
using ConnectVeiculos.Core.Entities.Contratos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ConnectVeiculos.Infrastructure.Services.Contratos
{
    /// <summary>
    /// PDF do contrato (A4): o texto, as assinaturas e uma folha final com o
    /// registro de cada assinatura (data, IP, navegador, como confirmou a
    /// identidade) e o SHA-256 do texto, que aparece tambem no rodape.
    /// </summary>
    public static class ContratoPdf
    {
        private static readonly TimeZoneInfo Brasilia = ObterFuso();

        static ContratoPdf()
        {
            // Licenca Community: gratuita para empresas com receita anual abaixo de US$ 1 milhao.
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public static byte[] Gerar(Contrato contrato, string loja)
        {
            var blocos = ContratoTexto.Blocos(contrato.CtrConteudo);
            var signatarios = contrato.Signatarios.OrderBy(s => s.CsgPapel == ContratoSignatario.PapelLoja ? 1 : 0).ToList();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    Pagina(page, contrato);
                    page.Content().Column(col =>
                    {
                        col.Spacing(8);
                        if (contrato.CtrStatus == Contrato.StatusCancelado)
                            col.Item().Background(Colors.Red.Lighten4).Padding(6).Text("CONTRATO CANCELADO").Bold().FontColor(Colors.Red.Darken2);

                        foreach (var b in blocos) Bloco(col, b);

                        col.Item().PaddingTop(24).Row(row =>
                        {
                            row.Spacing(24);
                            foreach (var s in signatarios) row.RelativeItem().Element(c => Assinatura(c, s, contrato.CtrTipo));
                        });
                    });
                });

                container.Page(page =>
                {
                    Pagina(page, contrato);
                    page.Content().Column(col =>
                    {
                        col.Spacing(6);
                        col.Item().Text("Registro das assinaturas eletrônicas").FontSize(13).Bold();
                        col.Item().Text(t =>
                        {
                            t.Span("Documento nº ").FontColor(Colors.Grey.Darken1);
                            t.Span(contrato.CtrId.ToString()).Bold();
                            t.Span($" · {contrato.CtrTitulo} · emitido por {loja} em {Hora(contrato.CtrDtCriacao)}").FontColor(Colors.Grey.Darken1);
                        });
                        col.Item().Text(t =>
                        {
                            t.Span("SHA-256 do texto: ").FontColor(Colors.Grey.Darken1);
                            t.Span(contrato.CtrHash).FontSize(8);
                        });
                        col.Item().Text("Qualquer alteração no texto muda esse código. Assinatura eletrônica nos termos da Lei 14.063/2020 e da MP 2.200-2/2001 (art. 10, § 2º), aceita pelas partes.")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);

                        foreach (var s in signatarios)
                        {
                            col.Item().PaddingTop(6).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
                            {
                                c.Spacing(2);
                                c.Item().Text($"{Papel(s, contrato.CtrTipo)}: {s.CsgNomeAssinado ?? s.CsgNome}").Bold();
                                Linha(c, "CPF/CNPJ", ContratoService.FormatarDocumento(s.CsgDocumentoAssinado ?? s.CsgDocumento));
                                Linha(c, "E-mail", s.CsgEmail);
                                if (!s.Assinou)
                                {
                                    Linha(c, "Situação", "aguardando assinatura");
                                    return;
                                }
                                Linha(c, "Assinado em", Hora(s.CsgDtAssinatura!.Value));
                                Linha(c, "Como", Metodo(s));
                                Linha(c, "Aberto em", s.CsgDtVisualizacao.HasValue ? Hora(s.CsgDtVisualizacao.Value) : null);
                                Linha(c, "IP", s.CsgIp);
                                Linha(c, "Navegador", s.CsgNavegador);
                            });
                        }
                    });
                });
            }).GeneratePdf();
        }

        private static void Pagina(PageDescriptor page, Contrato contrato)
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontSize(10).LineHeight(1.35f));
            page.Footer().Row(r =>
            {
                r.RelativeItem().Text($"Documento nº {contrato.CtrId} · SHA-256 {contrato.CtrHash[..16]}…").FontSize(7).FontColor(Colors.Grey.Darken1);
                r.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1));
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }

        private static void Bloco(ColumnDescriptor col, ContratoBloco b)
        {
            var item = b.Tipo switch
            {
                "titulo" => col.Item().PaddingBottom(6).AlignCenter(),
                "secao" => col.Item().PaddingTop(4),
                _ => col.Item()
            };
            item.Text(t =>
            {
                if (b.Tipo == "paragrafo") t.Justify();
                foreach (var tr in b.Trechos)
                {
                    var span = t.Span(tr.Texto);
                    if (b.Tipo == "titulo") span.FontSize(13).Bold();
                    else if (b.Tipo == "secao") span.FontSize(10.5f).Bold();
                    else if (tr.Negrito) span.Bold();
                    if (tr.Faltando) span.FontColor(Colors.Grey.Darken1);
                }
            });
        }

        private static void Assinatura(IContainer c, ContratoSignatario s, string tipo)
        {
            c.Column(col =>
            {
                var img = Imagem(s.CsgImagem);
                if (img != null) col.Item().Height(55).AlignCenter().AlignBottom().Image(img).FitArea();
                else col.Item().Height(55);
                col.Item().BorderTop(0.7f).PaddingTop(3).AlignCenter().Text(s.CsgNomeAssinado ?? s.CsgNome).Bold().FontSize(9);
                col.Item().AlignCenter().Text(Papel(s, tipo)).FontSize(8).FontColor(Colors.Grey.Darken1);
                col.Item().AlignCenter().Text(s.Assinou
                    ? $"Assinado eletronicamente em {Hora(s.CsgDtAssinatura!.Value)}"
                    : "Aguardando assinatura").FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        }

        private static void Linha(ColumnDescriptor c, string rotulo, string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return;
            c.Item().Text(t =>
            {
                t.Span(rotulo + ": ").FontColor(Colors.Grey.Darken1).FontSize(8.5f);
                t.Span(valor).FontSize(8.5f);
            });
        }

        public static string Papel(ContratoSignatario s, string tipo)
            => s.CsgPapel == ContratoSignatario.PapelLoja ? "Pela loja" : TiposContrato.PapelCliente(tipo);

        private static string Metodo(ContratoSignatario s) => s.CsgMetodo switch
        {
            ContratoSignatario.MetodoCodigoEmail => $"pelo link, com código enviado para {s.CsgEmail}",
            ContratoSignatario.MetodoPresencial => $"na loja, conferido por {s.CsgConferente}",
            ContratoSignatario.MetodoUsuario => $"no sistema, pelo usuário {s.CsgConferente}",
            _ => s.CsgMetodo ?? ""
        };

        private static byte[]? Imagem(string? dataUrl)
        {
            if (string.IsNullOrEmpty(dataUrl)) return null;
            var virgula = dataUrl.IndexOf(',');
            try { return Convert.FromBase64String(virgula >= 0 ? dataUrl[(virgula + 1)..] : dataUrl); }
            catch (FormatException) { return null; }
        }

        public static string Hora(DateTime utc)
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Brasilia).ToString("dd/MM/yyyy HH:mm:ss") + " (Brasília)";

        private static TimeZoneInfo ObterFuso()
        {
            foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
        }
    }
}
