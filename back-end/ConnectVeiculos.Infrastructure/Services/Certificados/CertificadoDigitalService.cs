using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.Certificados
{
    public class CertificadoDigitalService : ICertificadoDigitalService
    {
        private const string KeyPfx = "CERT_A1_PFX";
        private const string KeySenha = "CERT_A1_SENHA";
        private const string KeyTitular = "CERT_A1_TITULAR";
        private const string KeyCnpj = "CERT_A1_CNPJ";
        private const string KeyValidade = "CERT_A1_VALIDADE";

        private readonly IConfiguracaoSistemaRepository _config;
        private readonly ITokenProtector _protector;
        private readonly ILogger<CertificadoDigitalService> _logger;

        public CertificadoDigitalService(IConfiguracaoSistemaRepository config, ITokenProtector protector,
            ILogger<CertificadoDigitalService> logger)
        {
            _config = config;
            _protector = protector;
            _logger = logger;
        }

        public async Task<CertificadoDigitalInfo> ObterInfoAsync(CancellationToken ct = default)
        {
            var info = new CertificadoDigitalInfo
            {
                Cadastrado = !string.IsNullOrEmpty(await _config.GetValorAsync(KeyPfx)),
                Titular = Vazio(await _config.GetValorAsync(KeyTitular)),
                Cnpj = Vazio(await _config.GetValorAsync(KeyCnpj)),
            };
            if (DateTime.TryParse(await _config.GetValorAsync(KeyValidade), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var validade))
                info.Validade = validade;
            return info;
        }

        public async Task<CertificadoDigitalInfo> SalvarAsync(byte[] pfx, string senha, CancellationToken ct = default)
        {
            X509Certificate2 cert;
            try
            {
                cert = Abrir(pfx, senha ?? "");
            }
            catch (CryptographicException)
            {
                throw new CertificadoDigitalException("Não foi possível abrir o certificado. Confira o arquivo (.pfx/.p12) e a senha.");
            }

            using (cert)
            {
                if (!cert.HasPrivateKey)
                    throw new CertificadoDigitalException("O arquivo não tem a chave privada. Exporte o certificado A1 com a chave (.pfx).");
                if (cert.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
                    throw new CertificadoDigitalException($"Certificado vencido em {cert.NotAfter:dd/MM/yyyy}.");

                await _config.SetValorAsync(KeyPfx, _protector.Protect(Convert.ToBase64String(pfx)));
                await _config.SetValorAsync(KeySenha, _protector.Protect(senha ?? ""));
                await _config.SetValorAsync(KeyTitular, cert.GetNameInfo(X509NameType.SimpleName, false));
                await _config.SetValorAsync(KeyCnpj, CnpjDoTitular(cert.Subject) ?? "");
                await _config.SetValorAsync(KeyValidade, cert.NotAfter.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            }
            _logger.LogInformation("Certificado A1 da loja cadastrado");
            return await ObterInfoAsync(ct);
        }

        public async Task<CertificadoDigitalInfo> RemoverAsync(CancellationToken ct = default)
        {
            foreach (var k in new[] { KeyPfx, KeySenha, KeyTitular, KeyCnpj, KeyValidade })
                await _config.SetValorAsync(k, "");
            return await ObterInfoAsync(ct);
        }

        public async Task<X509Certificate2?> CarregarAsync(CancellationToken ct = default)
        {
            var cifrado = await _config.GetValorAsync(KeyPfx);
            if (string.IsNullOrEmpty(cifrado)) return null;
            try
            {
                var pfx = Convert.FromBase64String(_protector.Unprotect(cifrado));
                var senha = _protector.Unprotect(await _config.GetValorAsync(KeySenha) ?? "");
                return Abrir(pfx, senha);
            }
            catch (CryptographicException)
            {
                throw new CertificadoDigitalException("O certificado A1 guardado não abre mais. Envie o certificado de novo.");
            }
        }

        private static X509Certificate2 Abrir(byte[] pfx, string senha)
        {
            // Linux (servidor): chave so em memoria. No Windows o TLS do cliente
            // nao aceita chave efemera, entao la usa o armazenamento padrao.
            var flags = OperatingSystem.IsWindows()
                ? X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.Exportable
                : X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable;
            return new X509Certificate2(pfx, senha, flags);
        }

        /// <summary>e-CNPJ ICP-Brasil: o CN e' "RAZAO SOCIAL:12345678000199".</summary>
        public static string? CnpjDoTitular(string subject)
        {
            var m = Regex.Match(subject ?? "", @"CN=[^,]*:(\d{14})");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string? Vazio(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
    }
}
