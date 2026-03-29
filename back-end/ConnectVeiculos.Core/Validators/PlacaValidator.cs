using System.Text.RegularExpressions;

namespace ConnectVeiculos.Core.Validators
{
    /// <summary>
    /// Validador de placas de veiculos brasileiros
    /// </summary>
    public static class PlacaValidator
    {
        // Padrao antigo: AAA-0000
        private static readonly Regex PlacaAntigaRegex = new Regex(
            @"^[A-Z]{3}-?\d{4}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Padrao Mercosul: AAA0A00
        private static readonly Regex PlacaMercosulRegex = new Regex(
            @"^[A-Z]{3}\d[A-Z]\d{2}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Valida uma placa de veiculo brasileiro (formato antigo ou Mercosul)
        /// </summary>
        /// <param name="placa">Placa a ser validada</param>
        /// <returns>True se a placa for valida</returns>
        public static bool IsValid(string placa)
        {
            if (string.IsNullOrWhiteSpace(placa))
                return false;

            // Remove espacos e converte para maiusculo
            placa = placa.Trim().ToUpperInvariant();

            // Valida formato antigo ou Mercosul
            return PlacaAntigaRegex.IsMatch(placa) || PlacaMercosulRegex.IsMatch(placa);
        }

        /// <summary>
        /// Identifica o tipo da placa
        /// </summary>
        public static PlacaTipo GetTipo(string placa)
        {
            if (string.IsNullOrWhiteSpace(placa))
                return PlacaTipo.Invalida;

            placa = placa.Trim().ToUpperInvariant();

            if (PlacaMercosulRegex.IsMatch(placa))
                return PlacaTipo.Mercosul;

            if (PlacaAntigaRegex.IsMatch(placa))
                return PlacaTipo.Antiga;

            return PlacaTipo.Invalida;
        }

        /// <summary>
        /// Formata a placa para o padrao correto
        /// </summary>
        public static string Format(string placa)
        {
            if (string.IsNullOrWhiteSpace(placa))
                return placa;

            // Remove espacos e hifen
            placa = placa.Replace("-", "").Replace(" ", "").ToUpperInvariant();

            var tipo = GetTipo(placa);

            return tipo switch
            {
                PlacaTipo.Antiga => $"{placa.Substring(0, 3)}-{placa.Substring(3, 4)}",
                PlacaTipo.Mercosul => placa, // Mercosul nao usa hifen
                _ => placa
            };
        }
    }

    public enum PlacaTipo
    {
        Invalida,
        Antiga,
        Mercosul
    }
}
