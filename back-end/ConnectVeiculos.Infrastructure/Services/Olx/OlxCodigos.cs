using System.Globalization;
using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Napista;
using ConnectVeiculos.Core.Olx;
using ConnectVeiculos.Infrastructure.Services.Napista;

namespace ConnectVeiculos.Infrastructure.Services.Olx
{
    /// <summary>
    /// Codigos fixos da OLX para carros (developers.olx.com.br, categoria 2020)
    /// e o palpite a partir do cadastro (FIPE). Codigos que a OLX recusa hoje
    /// (combustivel 4, opcional 2, tipo 1 e 4, direcao 4) ficam de fora.
    /// </summary>
    public static class OlxCodigos
    {
        public const int CategoriaCarros = 2020;

        private static List<OlxItem> L(params (string Id, string Nome)[] itens) =>
            itens.Select(i => new OlxItem { Id = i.Id, Nome = i.Nome }).ToList();

        public static OlxAtributos Atributos() => new()
        {
            Combustiveis = L(("1", "Gasolina"), ("2", "Álcool"), ("3", "Flex"), ("5", "Diesel"), ("6", "Híbrido"), ("7", "Elétrico")),
            Cambios = L(("1", "Manual"), ("2", "Automático"), ("3", "Semi-automático"), ("4", "Automatizado")),
            Cores = L(("1", "Preto"), ("2", "Branco"), ("3", "Prata"), ("4", "Vermelho"), ("5", "Cinza"), ("6", "Azul"),
                ("7", "Amarelo"), ("8", "Verde"), ("9", "Laranja"), ("10", "Outra")),
            Portas = L(("1", "2 portas"), ("3", "3 portas"), ("2", "4 portas")),
            Tipos = L(("9", "Hatch"), ("8", "Sedã"), ("5", "SUV"), ("3", "Pick-up"), ("7", "Van/Utilitário"), ("12", "Perua"),
                ("11", "Cupê"), ("2", "Conversível"), ("6", "Buggy"), ("10", "Caminhão leve")),
            Direcoes = L(("1", "Hidráulica"), ("2", "Elétrica"), ("5", "Eletro-hidráulica"), ("3", "Mecânica")),
            Potencias = L(("1", "1.0"), ("2", "1.2"), ("3", "1.3"), ("4", "1.4"), ("5", "1.5"), ("6", "1.6"), ("7", "1.7"),
                ("8", "1.8"), ("9", "1.9"), ("10", "2.0 a 2.9"), ("11", "3.0 a 3.9"), ("12", "4.0 ou mais")),
            Opcionais = L(("1", "Ar condicionado"), ("3", "Vidro elétrico"), ("4", "Trava elétrica"), ("5", "Airbag"),
                ("6", "Alarme"), ("7", "Som"), ("8", "Sensor de ré"), ("9", "Câmera de ré"), ("10", "Blindado"),
                ("11", "Bancos de couro"), ("12", "Computador de bordo"), ("13", "Conexão USB"), ("14", "Volante multifuncional"),
                ("15", "Interface bluetooth"), ("16", "Navegador GPS"), ("17", "Controle automático de velocidade"),
                ("18", "Rodas de liga leve"), ("19", "Teto solar"), ("20", "Tração 4x4"))
        };

        /// <summary>Sinonimos do texto livre de opcionais do cadastro -> codigo da OLX.</summary>
        private static readonly (string Termo, string Codigo)[] OpcionaisSinonimos =
        {
            ("AR CONDICIONADO", "1"), ("AR-CONDICIONADO", "1"), ("VIDRO ELETRICO", "3"), ("VIDROS ELETRICOS", "3"),
            ("TRAVA ELETRICA", "4"), ("TRAVAS ELETRICAS", "4"), ("AIRBAG", "5"), ("ALARME", "6"), ("SOM", "7"),
            ("MULTIMIDIA", "7"), ("SENSOR DE RE", "8"), ("SENSOR DE ESTACIONAMENTO", "8"), ("CAMERA DE RE", "9"),
            ("BLINDADO", "10"), ("BANCO DE COURO", "11"), ("BANCOS DE COURO", "11"), ("COMPUTADOR DE BORDO", "12"),
            ("USB", "13"), ("VOLANTE MULTIFUNCIONAL", "14"), ("BLUETOOTH", "15"), ("GPS", "16"), ("NAVEGADOR", "16"),
            ("PILOTO AUTOMATICO", "17"), ("CONTROLE DE CRUZEIRO", "17"), ("CONTROLE AUTOMATICO DE VELOCIDADE", "17"),
            ("RODAS DE LIGA", "18"), ("RODA DE LIGA", "18"), ("TETO SOLAR", "19"), ("4X4", "20"), ("TRACAO 4X4", "20")
        };

        private static string N(string? texto) => Webmotors.WebmotorsSugestao.Normalizar(texto);

        private static List<NapistaItem> ComoNapista(IEnumerable<OlxItem> itens) =>
            itens.Select(i => new NapistaItem { Id = i.Id, Nome = i.Nome }).ToList();

        private static OlxItem? ComoOlx(NapistaItem? i) => i == null ? null : new OlxItem { Id = i.Id, Nome = i.Nome };

        public static OlxItem? Marca(string? veiMarca, IEnumerable<OlxItem> marcas) => ComoOlx(NapistaSugestao.Marca(veiMarca, ComoNapista(marcas)));
        public static OlxItem? Modelo(string? veiModelo, IEnumerable<OlxItem> modelos) => ComoOlx(NapistaSugestao.Modelo(veiModelo, ComoNapista(modelos)));

        public static OlxItem? Versao(string? veiModelo, OlxItem? modelo, IEnumerable<OlxItem> versoes) =>
            ComoOlx(NapistaSugestao.Versao(veiModelo, modelo == null ? null : new NapistaItem { Id = modelo.Id, Nome = modelo.Nome }, ComoNapista(versoes)));

        public static string? Combustivel(string? texto) => NapistaSugestao.Combustivel(texto) switch
        {
            "GASOLINE" => "1",
            "ALCOHOL" => "2",
            "FLEX" => "3",
            "DIESEL" => "5",
            "HYBRID" => "6",
            "ELECTRIC" => "7",
            _ => null
        };

        public static string? Cambio(string? texto)
        {
            var t = " " + N(texto) + " ";
            if (t.Contains(" AUTOMATIZADO ") || t.Contains(" AUTOMATIZADA ")) return "4";
            return NapistaSugestao.Cambio(texto) switch { "AUTOMATIC" => "2", "MANUAL" => "1", _ => null };
        }

        /// <summary>A OLX nao tem 5 portas: hatch de 5 portas entra como 4.</summary>
        public static string? Portas(string? texto) => NapistaSugestao.Portas(texto) switch
        {
            2 => "1",
            3 => "3",
            4 or 5 => "2",
            _ => null
        };

        public static string? Cor(string? veiCor) => NapistaSugestao.Cor(veiCor, ComoNapista(Atributos().Cores));

        /// <summary>"1.0", "1.6", "2.0" no nome do modelo.</summary>
        public static string? Potencia(string? texto)
        {
            var m = Regex.Match(" " + N(texto) + " ", @" ([1-9])\.([0-9])[0-9]* ");
            if (!m.Success) return null;
            var inteiro = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var decimo = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return inteiro switch
            {
                1 => decimo switch { 0 => "1", 2 => "2", 3 => "3", 4 => "4", 5 => "5", 6 => "6", 7 => "7", 8 => "8", 9 => "9", _ => null },
                2 => "10",
                3 => "11",
                _ => "12"
            };
        }

        public static List<string> Opcionais(string? veiOpcionais)
        {
            var t = " " + N(veiOpcionais) + " ";
            if (t.Trim() == "") return new();
            return OpcionaisSinonimos.Where(s => t.Contains(" " + N(s.Termo) + " ")).Select(s => s.Codigo)
                .Distinct().OrderBy(c => int.Parse(c, CultureInfo.InvariantCulture)).ToList();
        }

        /// <summary>A OLX aceita o ano a partir de 1980; antes disso, faixas de 5 anos.</summary>
        public static string AnoOlx(int ano) => ano >= 1980 ? ano.ToString(CultureInfo.InvariantCulture)
            : ano > 1975 ? "1975" : ano > 1970 ? "1970" : ano > 1965 ? "1965" : ano > 1960 ? "1960" : ano > 1955 ? "1955" : "1950";

        /// <summary>Erros de validacao da OLX (errors[].messages[].category) em portugues.</summary>
        public static string Traduzir(string categoria) => categoria switch
        {
            "UNDEFINED_AD_ID" => "anúncio sem identificação",
            "NO_IMAGE" => "o anúncio precisa de pelo menos uma foto",
            "NO_REGION" => "o CEP da loja não é válido para a OLX",
            "ERROR_FUEL_4_DEPRECATED" => "combustível não aceito (escolha outro)",
            "ERROR_FINANCIAL_INVALID" => "situação financeira inválida",
            "ERROR_CAR_FEATURE_2_INVALID" => "opcional não aceito",
            "ERROR_CAR_TYPE_1_OR_4_INVALID" => "tipo de carro não aceito (escolha outro)",
            "ERROR_VEHICLE_TAG_INVALID" => "placa inválida",
            "INVALID_PLATE" => "a OLX não reconheceu a placa",
            "ERROR_VEHICLE_BRAND_INVALID" => "marca inválida (escolha de novo na lista)",
            "ERROR_VEHICLE_MODEL_INVALID" => "modelo inválido (escolha de novo na lista)",
            "ERROR_VEHICLE_VERSION_INVALID" => "versão inválida (escolha de novo na lista)",
            "ERROR_VEHICLE_BRAND_MODEL_VERSION_INVALID" => "marca, modelo e versão não combinam (escolha de novo na lista)",
            "ERROR_VIDEOS_URL_INVALID" => "endereço de vídeo inválido",
            "ERROR_IMAGE_TOO_SMALL" => "foto pequena demais",
            "ERROR_DOWNLOADING_IMAGE" => "a OLX não conseguiu baixar uma das fotos",
            "ERROR_UPLOADING_IMAGE" => "a OLX não conseguiu processar uma das fotos",
            _ => categoria
        };

        /// <summary>statusCode da importacao (diferente de 0) em portugues.</summary>
        public static string StatusImportacao(int codigo) => codigo switch
        {
            -1 => "A OLX teve um erro inesperado. Tente de novo em alguns minutos.",
            -2 => "A OLX bloqueou temporariamente por excesso de envios. Tente de novo em alguns minutos.",
            -3 => "Nenhum anúncio para enviar.",
            -4 => "A OLX recusou o anúncio.",
            -5 => "A importação da OLX está fora do ar. Tente de novo em alguns minutos.",
            -6 => "A conta da OLX não tem permissão para integração: é preciso um plano profissional (Essencial, Plus ou Premium Empresa).",
            -7 or -8 => "O plano da OLX da loja não tem mais anúncios disponíveis.",
            _ => $"A OLX recusou o envio (código {codigo})."
        };
    }
}
