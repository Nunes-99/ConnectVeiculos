using ConnectVeiculos.Core.Entities.Veiculos;

namespace ConnectVeiculos.Core.Entities.VeiculosImagens
{
    public class VeiculoImagem
    {
        public int ImgId { get; private set; }
        public int R_VeiId { get; private set; }
        public string ImgCaminho { get; private set; }
        public int ImgOrdem { get; private set; }
        public bool ImgSts { get; private set; }

        // Navigation Property
        public Veiculo Veiculo { get; private set; }

        public VeiculoImagem() { }

        public VeiculoImagem(int imgId, int rVeiId, string imgCaminho, int imgOrdem, bool imgSts)
        {
            SetProperties(imgId, rVeiId, imgCaminho, imgOrdem, imgSts);
        }

        public void SetProperties(int imgId, int rVeiId, string imgCaminho, int imgOrdem, bool imgSts)
        {
            ImgId = imgId;
            R_VeiId = rVeiId;
            ImgCaminho = imgCaminho;
            ImgOrdem = imgOrdem;
            ImgSts = imgSts;
        }

        public void AlterarStatus(bool novoStatus)
        {
            ImgSts = novoStatus;
        }
    }
}
