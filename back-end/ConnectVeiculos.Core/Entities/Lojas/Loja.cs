using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Exceptions;

namespace ConnectVeiculos.Core.Entities.Lojas
{
    public class Loja
    {
        public int LojId { get; private set; }
        public string LojNome { get; private set; }
        public string LojSlug { get; private set; }
        public string LojLogradouro { get; private set; }
        public string LojNumero { get; private set; }
        public string LojBairro { get; private set; }
        public string LojCidade { get; private set; }
        public string LojEstado { get; private set; }
        public string LojCEP { get; private set; }
        public string LojComplemento { get; private set; }
        public string LojEmail { get; private set; }
        public string LojTel1 { get; private set; }
        public string LojTel2 { get; private set; }
        public string LojWhatsApp { get; private set; }
        public string LojImg { get; private set; }
        public string LojCNPJ { get; private set; }
        public string LojIE { get; private set; }
        public bool LojSts { get; private set; }
        public string LojCorPrimaria { get; private set; }
        public string LojCorSecundaria { get; private set; }
        public string LojInstagram { get; private set; }
        public string LojFacebook { get; private set; }
        public string LojUrlCatalogo { get; private set; }
        public bool LojPadraoCatalogo { get; private set; }

        // ===== Personalizacao do catalogo publico =====
        // Ficam fora do SetProperties (que ja tem 24 parametros) e sao gravados
        // por SetPersonalizacao, para nao arrastar todos os chamadores atuais.
        public string LojTema { get; private set; }
        public string LojCorFundo { get; private set; }
        public string LojBannerImg { get; private set; }
        public string LojBannerTitulo { get; private set; }
        public string LojBannerSubtitulo { get; private set; }
        public string LojFavicon { get; private set; }
        public string LojHorario { get; private set; }
        public string LojSobre { get; private set; }
        public string LojLinkVenderCarro { get; private set; }
        public bool LojMostrarMarcas { get; private set; }
        public bool LojMostrarMapa { get; private set; }

        public Loja() { }

        public Loja(int lojId, string lojNome, string lojLogradouro, string lojNumero,
            string lojBairro, string lojCidade, string lojEstado, string lojCEP,
            string lojComplemento, string lojEmail, string lojTel1, string lojTel2,
            string lojWhatsApp, string lojImg, string lojCNPJ, string lojIE, bool lojSts,
            string lojCorPrimaria = null, string lojCorSecundaria = null,
            string lojInstagram = null, string lojFacebook = null,
            string lojSlug = null, string lojUrlCatalogo = null,
            bool lojPadraoCatalogo = false)
        {
            SetProperties(lojId, lojNome, lojLogradouro, lojNumero, lojBairro, lojCidade,
                lojEstado, lojCEP, lojComplemento, lojEmail, lojTel1, lojTel2, lojWhatsApp, lojImg,
                lojCNPJ, lojIE, lojSts, lojCorPrimaria, lojCorSecundaria, lojInstagram, lojFacebook, lojSlug, lojUrlCatalogo, lojPadraoCatalogo);
        }

        public void SetProperties(int lojId, string lojNome, string lojLogradouro, string lojNumero,
            string lojBairro, string lojCidade, string lojEstado, string lojCEP,
            string lojComplemento, string lojEmail, string lojTel1, string lojTel2,
            string lojWhatsApp, string lojImg, string lojCNPJ, string lojIE, bool lojSts,
            string lojCorPrimaria = null, string lojCorSecundaria = null,
            string lojInstagram = null, string lojFacebook = null,
            string lojSlug = null, string lojUrlCatalogo = null,
            bool lojPadraoCatalogo = false)
        {
            LojId = lojId;
            LojNome = lojNome;
            LojSlug = string.IsNullOrWhiteSpace(lojSlug) ? GenerateSlug(lojNome) : lojSlug;
            LojLogradouro = lojLogradouro;
            LojNumero = lojNumero;
            LojBairro = lojBairro;
            LojCidade = lojCidade;
            LojEstado = lojEstado;
            LojCEP = lojCEP;
            LojComplemento = lojComplemento;
            LojEmail = lojEmail;
            LojTel1 = lojTel1;
            LojTel2 = lojTel2;
            LojWhatsApp = lojWhatsApp;
            LojImg = lojImg;
            LojCNPJ = lojCNPJ;
            LojIE = lojIE;
            LojSts = lojSts;
            LojCorPrimaria = lojCorPrimaria ?? "#1a237e";
            LojCorSecundaria = lojCorSecundaria ?? "#25d366";
            LojInstagram = lojInstagram;
            LojFacebook = lojFacebook;
            LojUrlCatalogo = lojUrlCatalogo;
            LojPadraoCatalogo = lojPadraoCatalogo;

            Validate();
        }

        public void SetUrlCatalogo(string url)
        {
            LojUrlCatalogo = url;
        }

        /// <summary>
        /// Aparencia do catalogo publico desta loja. Tudo opcional: o catalogo
        /// tem padrao para cada item, entao a loja que nao personaliza nada
        /// continua funcionando igual.
        /// </summary>
        public void SetPersonalizacao(
            string tema = null, string corFundo = null,
            string bannerImg = null, string bannerTitulo = null, string bannerSubtitulo = null,
            string favicon = null, string horario = null, string sobre = null,
            string linkVenderCarro = null,
            bool mostrarMarcas = true, bool mostrarMapa = true)
        {
            // Qualquer valor diferente de "claro" cai no escuro, que e' o padrao
            // do catalogo — evita tema invalido vindo do banco quebrar a tela.
            LojTema = tema == "claro" ? "claro" : "escuro";
            LojCorFundo = corFundo;
            LojBannerImg = bannerImg;
            LojBannerTitulo = bannerTitulo;
            LojBannerSubtitulo = bannerSubtitulo;
            LojFavicon = favicon;
            LojHorario = horario;
            LojSobre = sobre;
            LojLinkVenderCarro = linkVenderCarro;
            LojMostrarMarcas = mostrarMarcas;
            LojMostrarMapa = mostrarMapa;

            ValidatePersonalizacao();
        }

        private void ValidatePersonalizacao()
        {
            if (!string.IsNullOrWhiteSpace(LojBannerTitulo) && LojBannerTitulo.Length > 80)
                throw new LojaException("O título do banner deve ter no máximo 80 caracteres.");

            if (!string.IsNullOrWhiteSpace(LojBannerSubtitulo) && LojBannerSubtitulo.Length > 160)
                throw new LojaException("O subtítulo do banner deve ter no máximo 160 caracteres.");

            if (!string.IsNullOrWhiteSpace(LojHorario) && LojHorario.Length > 120)
                throw new LojaException("O horário de atendimento deve ter no máximo 120 caracteres.");

            if (!string.IsNullOrWhiteSpace(LojSobre) && LojSobre.Length > 1000)
                throw new LojaException("O texto sobre a loja deve ter no máximo 1000 caracteres.");
        }

        public void DefinirComoPadraoCatalogo(bool padrao)
        {
            LojPadraoCatalogo = padrao;
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(LojNome))
                throw new LojaException("O nome da loja é obrigatório.");

            if (LojNome.Length > 200)
                throw new LojaException("O nome da loja deve ter no máximo 200 caracteres.");

            if (!string.IsNullOrWhiteSpace(LojEstado) && LojEstado.Length > 2)
                throw new LojaException("O estado deve ter no máximo 2 caracteres.");
        }

        public void AlterarStatus(bool novoStatus)
        {
            LojSts = novoStatus;
        }

        public static string GenerateSlug(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var slug = text.ToLower()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
                .Replace("ã", "a").Replace("õ", "o").Replace("â", "a").Replace("ê", "e").Replace("ô", "o")
                .Replace("ç", "c");
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"[\s-]+", "-").Trim('-');
            return slug;
        }
    }
}
