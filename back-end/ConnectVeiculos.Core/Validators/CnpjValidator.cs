namespace ConnectVeiculos.Core.Validators
{
    /// <summary>
    /// Validador de CNPJ brasileiro
    /// </summary>
    public static class CnpjValidator
    {
        /// <summary>
        /// Valida um CNPJ brasileiro
        /// </summary>
        /// <param name="cnpj">CNPJ a ser validado (com ou sem formatacao)</param>
        /// <returns>True se o CNPJ for valido</returns>
        public static bool IsValid(string cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
                return false;

            // Remove caracteres nao numericos
            cnpj = new string(cnpj.Where(char.IsDigit).ToArray());

            // CNPJ deve ter 14 digitos
            if (cnpj.Length != 14)
                return false;

            // Verifica se todos os digitos sao iguais
            if (cnpj.Distinct().Count() == 1)
                return false;

            // Calcula o primeiro digito verificador
            int[] multiplicador1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int soma = 0;
            for (int i = 0; i < 12; i++)
                soma += int.Parse(cnpj[i].ToString()) * multiplicador1[i];

            int resto = soma % 11;
            int digito1 = resto < 2 ? 0 : 11 - resto;

            if (int.Parse(cnpj[12].ToString()) != digito1)
                return false;

            // Calcula o segundo digito verificador
            int[] multiplicador2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            soma = 0;
            for (int i = 0; i < 13; i++)
                soma += int.Parse(cnpj[i].ToString()) * multiplicador2[i];

            resto = soma % 11;
            int digito2 = resto < 2 ? 0 : 11 - resto;

            return int.Parse(cnpj[13].ToString()) == digito2;
        }

        /// <summary>
        /// Formata um CNPJ para o padrao XX.XXX.XXX/XXXX-XX
        /// </summary>
        public static string Format(string cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
                return cnpj;

            cnpj = new string(cnpj.Where(char.IsDigit).ToArray());

            if (cnpj.Length != 14)
                return cnpj;

            return $"{cnpj.Substring(0, 2)}.{cnpj.Substring(2, 3)}.{cnpj.Substring(5, 3)}/{cnpj.Substring(8, 4)}-{cnpj.Substring(12, 2)}";
        }

        /// <summary>
        /// Remove a formatacao do CNPJ
        /// </summary>
        public static string Unformat(string cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
                return cnpj;

            return new string(cnpj.Where(char.IsDigit).ToArray());
        }
    }
}
