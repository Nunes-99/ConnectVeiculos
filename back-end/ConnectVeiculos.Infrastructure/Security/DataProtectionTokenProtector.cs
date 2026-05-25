using ConnectVeiculos.Core.Interfaces.Security;
using Microsoft.AspNetCore.DataProtection;

namespace ConnectVeiculos.Infrastructure.Security
{
    public sealed class DataProtectionTokenProtector : ITokenProtector
    {
        private readonly IDataProtector _protector;

        public DataProtectionTokenProtector(IDataProtectionProvider provider)
        {
            // Purpose "v1" facilita rotacao futura sem invalidar chaves antigas
            // (se trocar o purpose, todos os tokens cifrados ficam ilegiveis).
            _protector = provider.CreateProtector("ConnectVeiculos.Tokens.v1");
        }

        public string Protect(string plaintext) => _protector.Protect(plaintext);

        public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
    }
}
