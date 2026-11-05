namespace ConnectVeiculos.Infrastructure.Services.NotasFiscais
{
    /// <summary>
    /// Codigo de barras Code 128 subconjunto C (pares de digitos), o usado na
    /// chave de acesso do DANFE. Devolve as larguras alternadas barra/espaco
    /// em modulos, comecando por barra.
    /// </summary>
    public static class Code128C
    {
        // Tabela padrao do Code 128: larguras de barra/espaco de cada simbolo (0 a 106).
        public static readonly string[] Padroes =
        {
            "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
            "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
            "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
            "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
            "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
            "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
            "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
            "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
            "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
            "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
            "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
        };

        private const int StartC = 105;
        private const int Stop = 106;

        /// <summary>Simbolos (start, dados, checksum, stop) para uma sequencia de digitos de tamanho par.</summary>
        public static List<int> Simbolos(string digitos)
        {
            if (string.IsNullOrEmpty(digitos) || digitos.Length % 2 != 0 || !digitos.All(char.IsDigit))
                throw new ArgumentException("Code 128C exige quantidade par de dígitos.", nameof(digitos));

            var simbolos = new List<int> { StartC };
            for (var i = 0; i < digitos.Length; i += 2)
                simbolos.Add((digitos[i] - '0') * 10 + (digitos[i + 1] - '0'));

            var soma = StartC;
            for (var i = 1; i < simbolos.Count; i++) soma += simbolos[i] * i;
            simbolos.Add(soma % 103);
            simbolos.Add(Stop);
            return simbolos;
        }

        /// <summary>Larguras em modulos, alternando barra e espaco, a partir de uma barra.</summary>
        public static List<int> Larguras(string digitos)
            => Simbolos(digitos).SelectMany(s => Padroes[s].Select(c => c - '0')).ToList();
    }
}
