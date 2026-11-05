using System.Globalization;
using System.Security;
using System.Text;
using System.Xml.Linq;
using ConnectVeiculos.Core.Webmotors;

namespace ConnectVeiculos.Infrastructure.Services.Webmotors
{
    /// <summary>
    /// Monta e le os envelopes SOAP da Webmotors. Os .asmx dela sao ASP.NET
    /// classico: a ordem dos elementos tem de seguir o WSDL, por isso o XML e'
    /// montado a mao em vez de serializado.
    /// </summary>
    public static class WebmotorsSoap
    {
        public const string NsLogin = "www.webmotors.com.br/wsLoginSistemaRevendedor";
        public const string NsEstoque = "www.webmotors.com.br/wsEstoqueRevendedorWebMotors";
        public const string ServicoLogin = "wsLoginSistemaRevendedor.asmx";
        public const string ServicoEstoque = "wsEstoqueRevendedorWebMotors.asmx";

        public const string Sucesso = "500";
        public const string LoginRecusado = "400";
        public const string SessaoInvalida = "401";

        /// <summary>Motivos de exclusao (manual, formato F05).</summary>
        public const int MotivoVendiNaWebmotors = 1;
        public const int MotivoVendiNaLoja = 2;
        public const int MotivoOutros = 3;

        public const int LimiteObservacao = 500;

        private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

        public static string Envelope(string ns, string operacao, params (string Nome, object? Valor)[] campos) =>
            Envelope(ns, operacao, Campos(campos));

        /// <summary>Parametros simples em sequencia (nulos ficam de fora).</summary>
        public static string Campos(params (string Nome, object? Valor)[] campos)
        {
            var corpo = new StringBuilder();
            foreach (var (nome, valor) in campos)
                corpo.Append(Elemento(nome, valor));
            return corpo.ToString();
        }

        public static string Envelope(string ns, string operacao, string corpo) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
            $"<{operacao} xmlns=\"{ns}\">{corpo}</{operacao}>" +
            "</soap:Body></soap:Envelope>";

        private static string Elemento(string nome, object? valor) =>
            valor == null ? "" : $"<{nome}>{SecurityElement.Escape(Formatar(valor))}</{nome}>";

        private static string Formatar(object valor) => valor switch
        {
            decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
            byte[] b => Convert.ToBase64String(b),
            bool b => b ? "S" : "N",
            DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => valor.ToString() ?? ""
        };

        /// <summary>Elemento Anuncio de IncluirCarro/AlterarCarro, na ordem do WSDL.</summary>
        public static string Anuncio(long codigoAnuncio, long modalidadeId, WebmotorsAnuncioDados d,
            int km, string placa, decimal preco, string? observacao)
        {
            var sb = new StringBuilder("<pAnuncio>");
            void Add(string nome, object? valor) => sb.Append(Elemento(nome, valor));

            Add("CodigoAnuncio", codigoAnuncio);
            Add("CodigoModalidade", modalidadeId);
            Add("TipoAnuncio", "U");
            Add("CodigoMarca", d.MarcaId);
            Add("CodigoModelo", d.ModeloId);
            Add("CodigoVersao", d.VersaoId);
            Add("AnoDoModelo", d.AnoModelo);
            Add("AnoFabricacao", d.AnoFabricacao);
            Add("Km", km);
            Add("Placa", placa);
            Add("CodigoCambio", d.CambioId);
            Add("NrPortas", d.Portas);
            Add("CodigoCor", d.CorId);
            Add("CodigoCombustivel", d.CombustivelId);
            Add("Blindado", d.Blindado);
            Add("AdaptadoDeficientesFisicos", d.AdaptadoPcd);
            Add("UnicoDono", d.UnicoDono);
            Add("Alienado", d.Alienado);
            Add("IpvaPago", d.IpvaPago);
            Add("NaoAceitaTroca", d.NaoAceitaTroca);
            Add("RevisadoOficinaAgendaDoCarro", false);
            Add("RevisoesEmConcessionaria", d.RevisoesEmConcessionaria);
            Add("GarantiaDeFabrica", d.GarantiaDeFabrica);
            Add("Licenciado", d.Licenciado);
            Add("Leilao", false);
            // PrecoReal e' o preco para outros revendedores (manual: "PrecoRevenda",
            // so aparece no Correio WebMotors). Zero = nao oferece.
            Add("PrecoReal", 0m);
            Add("PrecoVenda", preco);
            Add("Observacao", Observacao(observacao));
            if (d.Opcionais.Count > 0)
            {
                sb.Append("<Opcional>");
                foreach (var o in d.Opcionais.Distinct())
                    sb.Append($"<OpcionalWM><CodigoOpcional>{o.ToString(CultureInfo.InvariantCulture)}</CodigoOpcional></OpcionalWM>");
                sb.Append("</Opcional>");
            }
            sb.Append("</pAnuncio>");
            return sb.ToString();
        }

        public static string? Observacao(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var limpo = texto.Replace("\r", "").Trim();
            return limpo.Length <= LimiteObservacao ? limpo : limpo[..(LimiteObservacao - 3)].TrimEnd() + "...";
        }

        /// <summary>Conteudo de &lt;{operacao}Result&gt;, ou erro se veio SOAP Fault.</summary>
        public static XElement Resultado(string xml, string operacao)
        {
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (Exception) { throw new WebmotorsException("A Webmotors respondeu algo que não é XML. Tente de novo em alguns minutos."); }

            var fault = doc.Descendants(Soap + "Fault").FirstOrDefault();
            if (fault != null)
            {
                var texto = fault.Element("faultstring")?.Value ?? "erro desconhecido";
                throw new WebmotorsException($"A Webmotors recusou a chamada: {Curto(texto)}");
            }

            return doc.Descendants().FirstOrDefault(e => e.Name.LocalName == operacao + "Result")
                   ?? throw new WebmotorsException("Resposta da Webmotors sem resultado.");
        }

        public static string? Campo(XElement e, string nome) =>
            e.Elements().FirstOrDefault(x => x.Name.LocalName == nome)?.Value;

        public static long Numero(XElement e, string nome) =>
            decimal.TryParse(Campo(e, nome), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? (long)v : 0;

        public static IEnumerable<XElement> Itens(XElement resultado, string tipo) =>
            resultado.Descendants().Where(x => x.Name.LocalName == tipo);

        /// <summary>CodigoRetorno de um objeto ou do primeiro item de uma lista.</summary>
        public static string Retorno(XElement e) =>
            Campo(e, "CodigoRetorno") ?? e.Descendants().FirstOrDefault(x => x.Name.LocalName == "CodigoRetorno")?.Value ?? "";

        public static string MensagemDoRetorno(string codigo, string acao) => codigo switch
        {
            LoginRecusado => "A Webmotors não aceitou o CNPJ, o e-mail ou a senha do usuário de integração.",
            SessaoInvalida => "A sessão com a Webmotors expirou. Tente de novo.",
            "" => $"A Webmotors não confirmou {acao}.",
            _ => $"A Webmotors recusou {acao} (código {codigo}). Confira os dados do anúncio."
        };

        private static string Curto(string s)
        {
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            // A primeira linha do faultstring do .asmx e' a util; o resto e' stack.
            var corte = s.IndexOf(" ---> ", StringComparison.Ordinal);
            if (corte > 0) s = s[..corte];
            return s.Length > 240 ? s[..240] + "..." : s;
        }
    }
}
