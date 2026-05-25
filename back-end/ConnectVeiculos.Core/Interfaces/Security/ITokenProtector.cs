namespace ConnectVeiculos.Core.Interfaces.Security
{
    // Protege strings sensiveis (tokens OAuth, payloads de state) com criptografia simetrica.
    // Implementacao em Infrastructure usa Microsoft.AspNetCore.DataProtection.
    public interface ITokenProtector
    {
        string Protect(string plaintext);

        // Lanca CryptographicException se o ciphertext for invalido, adulterado ou
        // gerado por outra chave. Chamadores tratam como falha de borda.
        string Unprotect(string ciphertext);
    }
}
