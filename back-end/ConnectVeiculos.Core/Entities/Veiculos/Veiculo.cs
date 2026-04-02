using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.VeiculosCaracteristicas;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Entities.VeiculosObservacoes;
using ConnectVeiculos.Core.Exceptions;

namespace ConnectVeiculos.Core.Entities.Veiculos
{
    public class Veiculo
    {
        public int VeiId { get; private set; }
        public int R_LojId { get; private set; }
        public int R_CatId { get; private set; }
        public string VeiMarca { get; private set; }
        public string VeiModelo { get; private set; }
        public short VeiAno { get; private set; }
        public string VeiPlaca { get; private set; }
        public string VeiChassi { get; private set; }
        public string VeiCor { get; private set; }
        public int VeiKm { get; private set; }
        public decimal VeiPreco { get; private set; }
        public DateTime VeiDtEntrada { get; private set; }
        public string VeiSts { get; private set; }
        public string VeiSitSts { get; private set; }
        public decimal VeiPrecoCompra { get; private set; }
        public string VeiObservacao { get; private set; }
        public bool VeiPostadoInsta { get; private set; }
        public bool VeiPostadoFace { get; private set; }
        public DateTime? VeiDtPostagemInsta { get; private set; }
        public DateTime? VeiDtPostagemFace { get; private set; }
        public string VeiOpcionais { get; private set; }
        public string VeiDonoAtual { get; private set; }
        public string VeiDonoCelular { get; private set; }

        // Navigation Properties
        public Loja Loja { get; private set; }
        public Categoria Categoria { get; private set; }
        public List<VeiculoCaracteristica> Caracteristicas { get; private set; }
        public List<VeiculoObservacao> Observacoes { get; private set; }
        public List<VeiculoImagem> Imagens { get; private set; }

        public Veiculo()
        {
            Caracteristicas = new List<VeiculoCaracteristica>();
            Observacoes = new List<VeiculoObservacao>();
            Imagens = new List<VeiculoImagem>();
        }

        public Veiculo(int veiId, int rLojId, int rCatId, string veiMarca, string veiModelo,
            short veiAno, string veiPlaca, string veiChassi, string veiCor, int veiKm,
            decimal veiPreco, DateTime veiDtEntrada, string veiSts, string veiSitSts, decimal veiPrecoCompra,
            string veiObservacao = null, string veiDonoAtual = null, string veiDonoCelular = null, string veiOpcionais = null)
        {
            Caracteristicas = new List<VeiculoCaracteristica>();
            Observacoes = new List<VeiculoObservacao>();
            Imagens = new List<VeiculoImagem>();

            SetProperties(veiId, rLojId, rCatId, veiMarca, veiModelo, veiAno, veiPlaca, veiChassi,
                veiCor, veiKm, veiPreco, veiDtEntrada, veiSts, veiSitSts, veiPrecoCompra, veiObservacao,
                veiDonoAtual, veiDonoCelular, veiOpcionais);
        }

        public void SetProperties(int veiId, int rLojId, int rCatId, string veiMarca, string veiModelo,
            short veiAno, string veiPlaca, string veiChassi, string veiCor, int veiKm,
            decimal veiPreco, DateTime veiDtEntrada, string veiSts, string veiSitSts, decimal veiPrecoCompra,
            string veiObservacao = null, string veiDonoAtual = null, string veiDonoCelular = null, string veiOpcionais = null)
        {
            VeiId = veiId;
            R_LojId = rLojId;
            R_CatId = rCatId;
            VeiMarca = veiMarca;
            VeiModelo = veiModelo;
            VeiAno = veiAno;
            VeiPlaca = veiPlaca;
            VeiChassi = veiChassi;
            VeiCor = veiCor;
            VeiKm = veiKm;
            VeiPreco = veiPreco;
            VeiDtEntrada = veiDtEntrada;
            VeiSts = veiSts;
            VeiSitSts = veiSitSts;
            VeiPrecoCompra = veiPrecoCompra;
            VeiObservacao = veiObservacao;
            VeiDonoAtual = veiDonoAtual;
            VeiDonoCelular = veiDonoCelular;
            VeiOpcionais = veiOpcionais;

            Validate();
        }

        private void Validate()
        {
            if (R_LojId <= 0)
                throw new VeiculoException("A loja é obrigatória.");

            if (R_CatId <= 0)
                throw new VeiculoException("A categoria é obrigatória.");

            if (!string.IsNullOrWhiteSpace(VeiMarca) && VeiMarca.Length > 100)
                throw new VeiculoException("A marca deve ter no máximo 100 caracteres.");

            if (!string.IsNullOrWhiteSpace(VeiModelo) && VeiModelo.Length > 150)
                throw new VeiculoException("O modelo deve ter no máximo 150 caracteres.");

            if (!string.IsNullOrWhiteSpace(VeiPlaca) && VeiPlaca.Length > 10)
                throw new VeiculoException("A placa deve ter no máximo 10 caracteres.");

            if (!string.IsNullOrWhiteSpace(VeiChassi) && VeiChassi.Length > 20)
                throw new VeiculoException("O chassi deve ter no máximo 20 caracteres.");
        }

        public void AlterarStatus(string novoStatus)
        {
            VeiSts = novoStatus;
        }

        public void MarcarPostadoInstagram(bool postado)
        {
            VeiPostadoInsta = postado;
            VeiDtPostagemInsta = postado ? DateTime.Now : null;
        }

        public void MarcarPostadoFacebook(bool postado)
        {
            VeiPostadoFace = postado;
            VeiDtPostagemFace = postado ? DateTime.Now : null;
        }

        public void AddCaracteristica(VeiculoCaracteristica caracteristica)
        {
            Caracteristicas.Add(caracteristica);
        }

        public void AddObservacao(VeiculoObservacao observacao)
        {
            Observacoes.Add(observacao);
        }

        public void AddImagem(VeiculoImagem imagem)
        {
            Imagens.Add(imagem);
        }
    }
}
