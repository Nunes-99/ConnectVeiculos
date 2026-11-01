using System.Security.Cryptography.X509Certificates;

namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Certificado digital A1 (e-CNPJ) da loja: um so por tenant, usado pelo
    /// Renave e pela NF-e. O arquivo e a senha ficam cifrados na
    /// ConfiguracaoSistema; nada disso sai pela API.
    /// </summary>
    public interface ICertificadoDigitalService
    {
        Task<CertificadoDigitalInfo> ObterInfoAsync(CancellationToken ct = default);

        /// <summary>Valida (abre com a senha, tem chave privada, nao venceu) e guarda.</summary>
        Task<CertificadoDigitalInfo> SalvarAsync(byte[] pfx, string senha, CancellationToken ct = default);

        Task<CertificadoDigitalInfo> RemoverAsync(CancellationToken ct = default);

        /// <summary>Abre o certificado guardado, ou null se nao houver. Quem chama descarta.</summary>
        Task<X509Certificate2?> CarregarAsync(CancellationToken ct = default);
    }

    public class CertificadoDigitalInfo
    {
        public bool Cadastrado { get; set; }
        public string? Titular { get; set; }
        public string? Cnpj { get; set; }
        public DateTime? Validade { get; set; }
        public bool Valido => Cadastrado && Validade > DateTime.UtcNow;
    }

    /// <summary>Certificado ausente, ilegivel ou invalido, com texto para a tela.</summary>
    public class CertificadoDigitalException : Exception
    {
        public CertificadoDigitalException(string mensagem) : base(mensagem) { }
    }
}
