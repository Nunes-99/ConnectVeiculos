using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Exceptions;

namespace ConnectVeiculos.Core.Entities.Vendas
{
    public class Venda
    {
        public int VenId { get; private set; }
        public int R_VeiId { get; private set; }
        public int R_UsuId { get; private set; }
        public DateTime VenDtVenda { get; private set; }
        public string VenMarca { get; private set; }
        public string VenModelo { get; private set; }
        public short VenAno { get; private set; }
        public string VenChassi { get; private set; }
        public decimal VenValor { get; private set; }
        public decimal VenComissaoPorc { get; private set; }
        public decimal VenComissaoValor { get; private set; }

        // Dados do Comprador
        public string VenCompradorNome { get; private set; }
        public string VenCompradorCpf { get; private set; }
        public string VenCompradorTelefone { get; private set; }
        public string VenCompradorEmail { get; private set; }
        public string VenCompradorEndereco { get; private set; }

        // Forma de Pagamento e Status
        public string VenFormaPagamento { get; private set; }
        public string VenObservacao { get; private set; }
        public string VenStatus { get; private set; } // A = Ativa, E = Estornada
        public DateTime? VenDtEstorno { get; private set; }

        // Navigation Properties
        public Veiculo Veiculo { get; private set; }
        public Usuario Vendedor { get; private set; }

        public Venda() { }

        public Venda(int venId, int rVeiId, int rUsuId, DateTime venDtVenda, string venMarca,
            string venModelo, short venAno, string venChassi, decimal venValor,
            decimal venComissaoPorc, decimal venComissaoValor,
            string venCompradorNome = null, string venCompradorCpf = null,
            string venCompradorTelefone = null, string venCompradorEmail = null,
            string venCompradorEndereco = null, string venFormaPagamento = null,
            string venObservacao = null)
        {
            SetProperties(venId, rVeiId, rUsuId, venDtVenda, venMarca, venModelo, venAno,
                venChassi, venValor, venComissaoPorc, venComissaoValor,
                venCompradorNome, venCompradorCpf, venCompradorTelefone, venCompradorEmail, venCompradorEndereco,
                venFormaPagamento, venObservacao);
        }

        public void SetProperties(int venId, int rVeiId, int rUsuId, DateTime venDtVenda, string venMarca,
            string venModelo, short venAno, string venChassi, decimal venValor,
            decimal venComissaoPorc, decimal venComissaoValor,
            string venCompradorNome = null, string venCompradorCpf = null,
            string venCompradorTelefone = null, string venCompradorEmail = null,
            string venCompradorEndereco = null, string venFormaPagamento = null,
            string venObservacao = null)
        {
            VenId = venId;
            R_VeiId = rVeiId;
            R_UsuId = rUsuId;
            VenDtVenda = venDtVenda;
            VenMarca = venMarca;
            VenModelo = venModelo;
            VenAno = venAno;
            VenChassi = venChassi;
            VenValor = venValor;
            VenComissaoPorc = venComissaoPorc;
            VenComissaoValor = venComissaoValor;
            VenCompradorNome = venCompradorNome;
            VenCompradorCpf = venCompradorCpf;
            VenCompradorTelefone = venCompradorTelefone;
            VenCompradorEmail = venCompradorEmail;
            VenCompradorEndereco = venCompradorEndereco;
            VenFormaPagamento = venFormaPagamento;
            VenObservacao = venObservacao;
            VenStatus = "A"; // Ativa por padrao
            VenDtEstorno = null;

            Validate();
        }

        public void Estornar()
        {
            if (VenStatus == "E")
                throw new DomainException("Esta venda ja foi estornada.");

            VenStatus = "E";
            VenDtEstorno = DateTime.Now;
        }

        private void Validate()
        {
            if (R_VeiId <= 0)
                throw new DomainException("O veiculo e obrigatorio para a venda.");

            if (R_UsuId <= 0)
                throw new DomainException("O vendedor e obrigatorio para a venda.");

            if (VenValor < 0)
                throw new DomainException("O valor da venda nao pode ser negativo.");

            if (string.IsNullOrWhiteSpace(VenCompradorNome))
                throw new DomainException("O nome do comprador e obrigatorio.");
        }
    }
}
