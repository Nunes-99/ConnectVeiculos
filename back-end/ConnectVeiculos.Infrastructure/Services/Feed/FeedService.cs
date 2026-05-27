using System.Text;
using System.Xml.Linq;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Services.Facebook;
using ConnectVeiculos.Infrastructure.Services.Google;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Feed
{
    public class FeedService : IFeedService
    {
        private readonly ILojaRepository _lojaRepository;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly GoogleMerchantSettings _googleSettings;
        private readonly FacebookCatalogSettings _facebookSettings;
        private readonly ILogger<FeedService> _logger;

        public FeedService(
            ILojaRepository lojaRepository,
            IVeiculoRepository veiculoRepository,
            IVeiculoImagemRepository imagemRepository,
            IOptions<GoogleMerchantSettings> googleSettings,
            IOptions<FacebookCatalogSettings> facebookSettings,
            ILogger<FeedService> logger)
        {
            _lojaRepository = lojaRepository;
            _veiculoRepository = veiculoRepository;
            _imagemRepository = imagemRepository;
            _googleSettings = googleSettings.Value;
            _facebookSettings = facebookSettings.Value;
            _logger = logger;
        }

        public async Task<string> GerarFeedFacebookAsync()
        {
            var veiculos = await _veiculoRepository.GetAllAsync();
            var lojas = await _lojaRepository.GetAllAsync();
            var fallback = NormalizeBaseUrl(_facebookSettings?.PublicSiteUrl);

            var sb = new StringBuilder();
            // Schema Facebook Vehicle Inventory (formato TSV achatado, v2 simplificado).
            // Doc: https://developers.facebook.com/docs/marketing-api/catalog/reference#vehicle
            //
            // Apenas campos obrigatorios mais comuns. Versao anterior incluia opcionais
            // (vin, fuel_type, transmission, dealer_*) que podem rejeitar com valores
            // OTHER ou formato fora do esperado. image_link em vez de image.url
            // porque varias docs indicam o plain. condition separado de state_of_vehicle.
            sb.AppendLine(string.Join("\t",
                "vehicle_id",
                "title",
                "description",
                "url",
                "make",
                "model",
                "year",
                "mileage.value",
                "mileage.unit",
                "image_link",
                "address.addr1",
                "address.city",
                "address.region",
                "address.country",
                "address.postal_code",
                "price",
                "state_of_vehicle",
                "body_style",
                "availability",
                "condition",
                "exterior_color"
            ));

            foreach (var v in veiculos.Where(v => v.VeiSts == "D"))
            {
                var loja = lojas.FirstOrDefault(l => l.LojId == v.R_LojId);
                var baseUrl = NormalizeBaseUrl(loja?.LojUrlCatalogo) ?? fallback;
                if (baseUrl == null)
                {
                    _logger.LogWarning(
                        "Feed Facebook: veiculo {VeiculoId} omitido - nem LojUrlCatalogo (loja {LojaId}: '{Url}') nem FacebookCatalogSettings.PublicSiteUrl ('{Fallback}') sao URLs validas.",
                        v.VeiId, v.R_LojId, loja?.LojUrlCatalogo, _facebookSettings?.PublicSiteUrl);
                    continue;
                }

                var imagens = await _imagemRepository.GetByVeiculoIdAsync(v.VeiId);
                var imagemPrincipal = imagens.Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem).FirstOrDefault();
                var imageUrl = imagemPrincipal != null
                    ? $"{baseUrl}/api/imagens/file?path={Uri.EscapeDataString(imagemPrincipal.ImgCaminho)}"
                    : "";

                // Sem imagem o Facebook rejeita o produto no upload. Logamos e pulamos.
                if (string.IsNullOrEmpty(imageUrl))
                {
                    _logger.LogWarning(
                        "Feed Facebook: veiculo {VeiculoId} ({Marca} {Modelo}) omitido - sem imagem principal cadastrada.",
                        v.VeiId, v.VeiMarca, v.VeiModelo);
                    continue;
                }

                var slug = loja?.LojSlug ?? v.R_LojId.ToString();
                var url = $"{baseUrl}/catalogo/{slug}/veiculo/{v.VeiId}";

                // Title max 65 chars; trunca com sufixo curto se ultrapassar.
                var title = $"{v.VeiMarca} {v.VeiModelo} {v.VeiAno}";
                if (title.Length > 65) title = title.Substring(0, 62) + "...";

                var description = $"{v.VeiMarca} {v.VeiModelo} {v.VeiAno}, {v.VeiCor}, {v.VeiKm:N0} km";
                if (!string.IsNullOrWhiteSpace(v.VeiObservacao))
                    description += ". " + v.VeiObservacao;
                if (description.Length > 5000) description = description.Substring(0, 4997) + "...";

                // Endereco completo monta com Logradouro + Numero + Bairro.
                var endereco = string.Join(", ", new[]
                {
                    loja?.LojLogradouro,
                    loja?.LojNumero,
                    loja?.LojBairro
                }.Where(s => !string.IsNullOrWhiteSpace(s)));

                sb.AppendLine(string.Join("\t",
                    Tsv(v.VeiId.ToString()),
                    Tsv(title),
                    Tsv(description),
                    Tsv(url),
                    Tsv(v.VeiMarca),
                    Tsv(v.VeiModelo),
                    Tsv(v.VeiAno.ToString()),
                    Tsv(v.VeiKm.ToString()),
                    "KM",
                    Tsv(imageUrl),
                    Tsv(string.IsNullOrWhiteSpace(endereco) ? (loja?.LojCidade ?? "") : endereco),
                    Tsv(loja?.LojCidade ?? ""),
                    Tsv(loja?.LojEstado ?? ""),
                    "BR",
                    Tsv(SanitizarCEP(loja?.LojCEP)),
                    $"{v.VeiPreco:F2} BRL",
                    "USED",
                    InferirBodyStyle(v.VeiModelo),
                    "AVAILABLE",
                    "USED",
                    Tsv(v.VeiCor ?? "")
                ));
            }

            return sb.ToString();
        }

        // Mapeia modelo do veiculo para body_style enumerado pela Meta.
        // Valores aceitos: CONVERTIBLE, COUPE, HATCHBACK, MINIVAN, TRUCK, SUV,
        // SEDAN, VAN, WAGON, OTHER. Heuristica simples por palavra-chave;
        // quando nao reconhece, devolve OTHER (Meta aceita).
        private static string InferirBodyStyle(string? modelo)
        {
            if (string.IsNullOrWhiteSpace(modelo)) return "OTHER";
            var m = modelo.ToUpperInvariant();
            if (m.Contains("SUV") || m.Contains("TRACKER") || m.Contains("DUSTER") ||
                m.Contains("ECOSPORT") || m.Contains("HRV") || m.Contains("HR-V") ||
                m.Contains("KICKS") || m.Contains("RENEGADE") || m.Contains("COMPASS") ||
                m.Contains("CRETA") || m.Contains("T-CROSS") || m.Contains("TCROSS"))
                return "SUV";
            if (m.Contains("PICK") || m.Contains("HILUX") || m.Contains("S10") ||
                m.Contains("STRADA") || m.Contains("MONTANA") || m.Contains("SAVEIRO") ||
                m.Contains("RAM ") || m.Contains("FRONTIER") || m.Contains("AMAROK") ||
                m.Contains("RANGER") || m.Contains("L200"))
                return "TRUCK";
            if (m.Contains("MINIVAN") || m.Contains("SHARAN") || m.Contains("CARAVAN") ||
                m.Contains("DOBLO"))
                return "MINIVAN";
            if (m.Contains("VAN") || m.Contains("SPRINTER") || m.Contains("DUCATO") ||
                m.Contains("MASTER"))
                return "VAN";
            if (m.Contains("WAGON") || m.Contains("PERUA"))
                return "WAGON";
            if (m.Contains("COUPE") || m.Contains("CABRIO") || m.Contains("CONVERSIVEL"))
                return "CONVERTIBLE";
            if (m.Contains("HATCH") || m.Contains("HB20") || m.Contains("GOL") ||
                m.Contains(" KA") || m.Contains("KA SE") || m.Contains("FOX") ||
                m.Contains("FIESTA") || m.Contains("UNO") || m.Contains("PALIO") ||
                m.Contains("CELTA") || m.Contains("ONIX") && !m.Contains("PLUS") ||
                m.Contains("YARIS HATCH") || m.Contains("POLO"))
                return "HATCHBACK";
            // Default sedan pra modelos populares conhecidos
            if (m.Contains("COROLLA") || m.Contains("CIVIC") || m.Contains("JETTA") ||
                m.Contains("CRUZE") || m.Contains("VIRTUS") || m.Contains("VOYAGE") ||
                m.Contains("PRISMA") || m.Contains("LOGAN") || m.Contains("HB20S") ||
                m.Contains("ONIX PLUS") || m.Contains("CITY") || m.Contains("VERSA") ||
                m.Contains("SENTRA"))
                return "SEDAN";
            return "OTHER";
        }

        // Sanitiza valor pra coluna TSV: remove tabs/quebras de linha que
        // quebrariam o parser do Facebook (1 produto por linha).
        private static string Tsv(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return raw.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
        }

        // CEP do Facebook deve ser numerico puro (sem hifen). Brasileiro "13322-372"
        // vira "13322372". Strings vazias/null mantem vazio.
        private static string SanitizarCEP(string? cep)
        {
            if (string.IsNullOrWhiteSpace(cep)) return "";
            return new string(cep.Where(char.IsDigit).ToArray());
        }

        public async Task<string> GerarFeedGoogleAsync()
        {
            var veiculos = await _veiculoRepository.GetAllAsync();
            var lojas = await _lojaRepository.GetAllAsync();
            var primeiraLoja = lojas.FirstOrDefault();
            var fallback = NormalizeBaseUrl(_googleSettings?.PublicSiteUrl);

            XNamespace g = "http://base.google.com/ns/1.0";
            XNamespace atom = "http://www.w3.org/2005/Atom";

            var items = new List<XElement>();

            foreach (var v in veiculos.Where(v => v.VeiSts == "D"))
            {
                var loja = lojas.FirstOrDefault(l => l.LojId == v.R_LojId);
                var baseUrl = NormalizeBaseUrl(loja?.LojUrlCatalogo) ?? fallback;
                if (baseUrl == null)
                {
                    _logger.LogWarning(
                        "Feed Google: veiculo {VeiculoId} omitido - nem LojUrlCatalogo (loja {LojaId}: '{Url}') nem GoogleMerchantSettings.PublicSiteUrl ('{Fallback}') sao URLs validas.",
                        v.VeiId, v.R_LojId, loja?.LojUrlCatalogo, _googleSettings?.PublicSiteUrl);
                    continue;
                }

                var imagens = await _imagemRepository.GetByVeiculoIdAsync(v.VeiId);
                var imagemPrincipal = imagens.Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem).FirstOrDefault();
                var imageUrl = imagemPrincipal != null
                    ? $"{baseUrl}/api/imagens/file?path={Uri.EscapeDataString(imagemPrincipal.ImgCaminho)}"
                    : "";
                var slug = loja?.LojSlug ?? v.R_LojId.ToString();
                var link = $"{baseUrl}/catalogo/{slug}/veiculo/{v.VeiId}";

                var item = new XElement("item",
                    new XElement(g + "id", v.VeiId),
                    new XElement("title", $"{v.VeiMarca} {v.VeiModelo} {v.VeiAno}"),
                    new XElement("description", $"{v.VeiMarca} {v.VeiModelo} {v.VeiAno}, {v.VeiCor}, {v.VeiKm:N0} km. {loja?.LojNome}"),
                    new XElement("link", link),
                    new XElement(g + "image_link", imageUrl),
                    new XElement(g + "condition", "used"),
                    new XElement(g + "price", $"{v.VeiPreco:F2} BRL"),
                    new XElement(g + "availability", "in_stock"),
                    new XElement(g + "brand", v.VeiMarca),
                    new XElement(g + "product_type", "Vehicles & Parts > Vehicles > Cars"),
                    new XElement(g + "custom_label_0", v.VeiAno.ToString()),
                    new XElement(g + "custom_label_1", v.VeiKm.ToString()),
                    new XElement(g + "custom_label_2", v.VeiCor ?? "")
                );

                items.Add(item);
            }

            var channelLink = NormalizeBaseUrl(primeiraLoja?.LojUrlCatalogo) ?? fallback ?? "";

            var rss = new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "g", g),
                new XElement("channel",
                    new XElement("title", primeiraLoja?.LojNome ?? "Catalogo de Veiculos"),
                    new XElement("link", channelLink),
                    new XElement("description", "Catalogo de veiculos disponiveis"),
                    items
                )
            );

            var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), rss);
            return doc.ToString();
        }

        // Sanitiza LojUrlCatalogo / PublicSiteUrl antes de usar no feed.
        // Aceita "https://site.com", "site.com" (https auto), "http://localhost:5219".
        // Rejeita vazio, "http:", "http:/", "http://" e qualquer URI sem host -
        // valores assim eram a causa de links tipo "http:/catalogo/..." quebrarem
        // a importacao no Merchant Center ("Dominios incompativeis").
        private static string? NormalizeBaseUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var trimmed = raw.Trim().TrimEnd('/');

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                if (!Uri.TryCreate("https://" + trimmed, UriKind.Absolute, out uri))
                    return null;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            if (string.IsNullOrWhiteSpace(uri.Host)) return null;

            var port = uri.IsDefaultPort ? "" : ":" + uri.Port;
            return $"{uri.Scheme}://{uri.Host}{port}";
        }
    }
}
