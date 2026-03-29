namespace ConnectVeiculos.Core.Validators
{
    /// <summary>
    /// Validador de CPF brasileiro
    /// </summary>
    public static class CpfValidator
    {
        /// <summary>
        /// Valida um CPF brasileiro
        /// </summary>
        /// <param name="cpf">CPF a ser validado (com ou sem formatacao)</param>
        /// <returns>True se o CPF for valido</returns>
        public static bool IsValid(string cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return false;

            // Remove caracteres nao numericos
            cpf = new string(cpf.Where(char.IsDigit).ToArray());

            // CPF deve ter 11 digitos
            if (cpf.Length != 11)
                return false;

            // Verifica se todos os digitos sao iguais (ex: 111.111.111-11)
            if (cpf.Distinct().Count() == 1)
                return false;

            // Calcula o primeiro digito verificador
            int[] multiplicador1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int soma = 0;
            for (int i = 0; i < 9; i++)
                soma += int.Parse(cpf[i].ToString()) * multiplicador1[i];

            int resto = soma % 11;
            int digito1 = resto < 2 ? 0 : 11 - resto;

            if (int.Parse(cpf[9].ToString()) != digito1)
                return false;

            // Calcula o segundo digito verificador
            int[] multiplicador2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            soma = 0;
            for (int i = 0; i < 10; i++)
                soma += int.Parse(cpf[i].ToString()) * multiplicador2[i];

            resto = soma % 11;
            int digito2 = resto < 2 ? 0 : 11 - resto;

            return int.Parse(cpf[10].ToString()) == digito2;
        }

        /// <summary>
        /// Formata um CPF para o padrao XXX.XXX.XXX-XX
        /// </summary>
        public static string Format(string cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return cpf;

            cpf = new string(cpf.Where(char.IsDigit).ToArray());

            if (cpf.Length != 11)
                return cpf;

            return $"{cpf.Substring(0, 3)}.{cpf.Substring(3, 3)}.{cpf.Substring(6, 3)}-{cpf.Substring(9, 2)}";
        }

        /// <summary>
        /// Remove a formatacao do CPF
        /// </summary>
        public static string Unformat(string cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return cpf;

            return new string(cpf.Where(char.IsDigit).ToArray());
        }
    }
}
