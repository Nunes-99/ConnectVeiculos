using ConnectVeiculos.Application.Exceptions;

namespace ConnectVeiculos.Application.InputModels.Lojas
{
    public class LojaInputModel
    {
        public int LojId { get; set; }
        public string LojNome { get; set; }
        public string LojLogradouro { get; set; }
        public string LojNumero { get; set; }
        public string LojBairro { get; set; }
        public string LojCidade { get; set; }
        public string LojEstado { get; set; }
        public string LojCEP { get; set; }
        public string LojComplemento { get; set; }
        public string LojEmail { get; set; }
        public string LojTel1 { get; set; }
        public string LojTel2 { get; set; }
        public string LojWhatsApp { get; set; }
        public string LojImg { get; set; }
        public string LojCNPJ { get; set; }
        public string LojIE { get; set; }
        public bool LojSts { get; set; }
        public string LojCorPrimaria { get; set; }
        public string LojCorSecundaria { get; set; }
        public string LojInstagram { get; set; }
        public string LojFacebook { get; set; }
        public string LojSlug { get; set; }

        public LojaInputModel() { }

        public LojaInputModel(int lojId, string lojNome, string lojLogradouro, string lojNumero,
            string lojBairro, string lojCidade, string lojEstado, string lojCEP,
            string lojComplemento, string lojEmail, string lojTel1, string lojTel2,
            string lojImg, string lojCNPJ, string lojIE, bool lojSts)
        {
            LojId = lojId;
            LojNome = lojNome;
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
            LojImg = lojImg;
            LojCNPJ = lojCNPJ;
            LojIE = lojIE;
            LojSts = lojSts;

            Validate();
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(LojNome))
                throw new InputModelException("O nome da loja é obrigatório.");
        }
    }
}
