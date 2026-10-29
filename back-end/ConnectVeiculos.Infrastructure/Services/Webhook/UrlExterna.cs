using System.Net;
using System.Net.Sockets;

namespace ConnectVeiculos.Infrastructure.Services.Webhook
{
    /// <summary>
    /// Webhook de saida e' uma URL que o admin da loja escolhe e que o servidor
    /// chama. Sem filtro, ela podia apontar para dentro da propria VM
    /// (http://backend-cliente:8080, 127.0.0.1, 169.254.169.254 da nuvem) e o
    /// servidor fazia a requisicao por ele. So passa HTTPS para endereco publico.
    /// </summary>
    public static class UrlExterna
    {
        public static async Task<bool> EhSeguraAsync(string? url, CancellationToken ct = default)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return false;

            IPAddress[] enderecos;
            try
            {
                enderecos = IPAddress.TryParse(uri.Host, out var ip)
                    ? new[] { ip }
                    : await Dns.GetHostAddressesAsync(uri.Host, ct);
            }
            catch (SocketException)
            {
                return false;
            }

            return enderecos.Length > 0 && enderecos.All(EhPublico);
        }

        public static bool EhPublico(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)
                || ip.Equals(IPAddress.Broadcast) || ip.Equals(IPAddress.IPv6None))
                return false;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                return !(b[0] == 10
                         || b[0] == 0
                         || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                         || (b[0] == 192 && b[1] == 168)
                         || (b[0] == 169 && b[1] == 254)
                         || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                         || b[0] >= 224);
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var b = ip.GetAddressBytes();
                return !(ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast
                         || (b[0] & 0xFE) == 0xFC); // fc00::/7, rede privada
            }

            return false;
        }
    }
}
