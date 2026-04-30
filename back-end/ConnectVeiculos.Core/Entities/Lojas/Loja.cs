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

        public Loja() { }

        public Loja(int lojId, string lojNome, string lojLogradouro, string lojNumero,
            string lojBairro, string lojCidade, string lojEstado, string lojCEP,
            string lojComplemento, string lojEmail, string lojTel1, string lojTel2,
            string lojWhatsApp, string lojImg, string lojCNPJ, string lojIE, bool lojSts,
            string lojCorPrimaria = null, string lojCorSecundaria = null,
            string lojInstagram = null, string lojFacebook = null,
            string lojSlug = null, string lojUrlCatalogo = null)
        {
            SetProperties(lojId, lojNome, lojLogradouro, lojNumero, lojBairro, lojCidade,
                lojEstado, lojCEP, lojComplemento, lojEmail, lojTel1, lojTel2, lojWhatsApp, lojImg,
                lojCNPJ, lojIE, lojSts, lojCorPrimaria, lojCorSecundaria, lojInstagram, lojFacebook, lojSlug, lojUrlCatalogo);
        }

        public void SetProperties(int lojId, string lojNome, string lojLogradouro, string lojNumero,
            string lojBairro, string lojCidade, string lojEstado, string lojCEP,
            string lojComplemento, string lojEmail, string lojTel1, string lojTel2,
            string lojWhatsApp, string lojImg, string lojCNPJ, string lojIE, bool lojSts,
            string lojCorPrimaria = null, string lojCorSecundaria = null,
            string lojInstagram = null, string lojFacebook = null,
            string lojSlug = null, string lojUrlCatalogo = null)
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

            Validate();
        }

        public void SetUrlCatalogo(string url)
        {
            LojUrlCatalogo = url;
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
