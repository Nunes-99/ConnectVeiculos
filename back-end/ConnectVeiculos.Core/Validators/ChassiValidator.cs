using System.Text.RegularExpressions;

namespace ConnectVeiculos.Core.Validators
{
    /// <summary>
    /// Validador de chassi de veiculos (VIN - Vehicle Identification Number)
    /// </summary>
    public static class ChassiValidator
    {
        // Chassi deve ter 17 caracteres, sem I, O, Q
        private static readonly Regex ChassiRegex = new Regex(
            @"^[A-HJ-NPR-Z0-9]{17}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Pesos para calculo do digito verificador (posicao 9)
        private static readonly int[] Pesos = { 8, 7, 6, 5, 4, 3, 2, 10, 0, 9, 8, 7, 6, 5, 4, 3, 2 };

        // Transliteracao de letras para numeros
        private static readonly Dictionary<char, int> Transliteracao = new Dictionary<char, int>
        {
            {'A', 1}, {'B', 2}, {'C', 3}, {'D', 4}, {'E', 5}, {'F', 6}, {'G', 7}, {'H', 8},
            {'J', 1}, {'K', 2}, {'L', 3}, {'M', 4}, {'N', 5}, {'P', 7}, {'R', 9},
            {'S', 2}, {'T', 3}, {'U', 4}, {'V', 5}, {'W', 6}, {'X', 7}, {'Y', 8}, {'Z', 9}
        };

        /// <summary>
        /// Valida um chassi (VIN)
        /// </summary>
        /// <param name="chassi">Chassi a ser validado</param>
        /// <returns>True se o chassi for valido</returns>
        public static bool IsValid(string chassi)
        {
            if (string.IsNullOrWhiteSpace(chassi))
                return false;

            chassi = chassi.Trim().ToUpperInvariant();

            // Valida formato basico (17 caracteres, sem I, O, Q)
            if (!ChassiRegex.IsMatch(chassi))
                return false;

            // Valida digito verificador (posicao 9)
            return ValidateCheckDigit(chassi);
        }

        /// <summary>
        /// Valida o digito verificador do chassi
        /// </summary>
        private static bool ValidateCheckDigit(string chassi)
        {
            int soma = 0;

            for (int i = 0; i < 17; i++)
            {
                char c = chassi[i];
                int valor;

                if (char.IsDigit(c))
                {
                    valor = c - '0';
                }
                else if (Transliteracao.TryGetValue(c, out int trans))
                {
                    valor = trans;
                }
                else
                {
                    return false;
                }

                soma += valor * Pesos[i];
            }

            int resto = soma % 11;
            char digitoEsperado = resto == 10 ? 'X' : (char)('0' + resto);

            return chassi[8] == digitoEsperado;
        }

        /// <summary>
        /// Extrai informacoes do chassi
        /// </summary>
        public static ChassiInfo GetInfo(string chassi)
        {
            if (!IsValid(chassi))
                return null;

            chassi = chassi.ToUpperInvariant();

            return new ChassiInfo
            {
                WMI = chassi.Substring(0, 3), // World Manufacturer Identifier
                VDS = chassi.Substring(3, 6), // Vehicle Descriptor Section
                VIS = chassi.Substring(9, 8), // Vehicle Identifier Section
                AnoModelo = GetAnoModelo(chassi[9]),
                CodigoFabrica = chassi[10].ToString(),
                NumeroSequencial = chassi.Substring(11, 6)
            };
        }

        /// <summary>
        /// Obtem o ano do modelo a partir do caractere na posicao 10
        /// </summary>
        private static int GetAnoModelo(char c)
        {
            // Anos de 2010 a 2039 usam A-Y (exceto I, O, Q, U, Z)
            // Anos de 2001 a 2009 usam 1-9
            var anoMap = new Dictionary<char, int>
            {
                {'1', 2001}, {'2', 2002}, {'3', 2003}, {'4', 2004}, {'5', 2005},
                {'6', 2006}, {'7', 2007}, {'8', 2008}, {'9', 2009},
                {'A', 2010}, {'B', 2011}, {'C', 2012}, {'D', 2013}, {'E', 2014},
                {'F', 2015}, {'G', 2016}, {'H', 2017}, {'J', 2018}, {'K', 2019},
                {'L', 2020}, {'M', 2021}, {'N', 2022}, {'P', 2023}, {'R', 2024},
                {'S', 2025}, {'T', 2026}, {'V', 2027}, {'W', 2028}, {'X', 2029}, {'Y', 2030}
            };

            return anoMap.TryGetValue(c, out int ano) ? ano : 0;
        }
    }

    public class ChassiInfo
    {
        public string WMI { get; set; } // Identificador do fabricante mundial
        public string VDS { get; set; } // Secao de descricao do veiculo
        public string VIS { get; set; } // Secao de identificacao do veiculo
        public int AnoModelo { get; set; }
        public string CodigoFabrica { get; set; }
        public string NumeroSequencial { get; set; }
    }
}
