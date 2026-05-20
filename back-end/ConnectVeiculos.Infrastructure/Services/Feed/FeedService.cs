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
            sb.AppendLine("id\ttitle\tdescription\tavailability\tcondition\tprice\tlink\timage_link\tbrand\tvehicle_type\tyear\tmileage.value\tmileage.unit\tcolor\taddress.city\taddress.region");

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
                var slug = loja?.LojSlug ?? v.R_LojId.ToString();
                var link = $"{baseUrl}/catalogo/{slug}/veiculo/{v.VeiId}";

                sb.AppendLine(string.Join("\t",
                    v.VeiId,
                    $"{v.VeiMarca} {v.VeiModelo} {v.VeiAno}",
                    $"{v.VeiMarca} {v.VeiModelo} {v.VeiAno}, {v.VeiCor}, {v.VeiKm:N0} km",
                    "in stock",
                    "used",
                    $"{v.VeiPreco:F2} BRL",
                    link,
                    imageUrl,
                    v.VeiMarca,
                    "car",
                    v.VeiAno,
                    v.VeiKm,
                    "KM",
                    v.VeiCor ?? "",
                    loja?.LojCidade ?? "",
                    loja?.LojEstado ?? ""
                ));
            }

            return sb.ToString();
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
