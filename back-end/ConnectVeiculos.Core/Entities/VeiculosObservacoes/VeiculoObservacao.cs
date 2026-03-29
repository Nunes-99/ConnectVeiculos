using ConnectVeiculos.Core.Entities.Observacoes;
using ConnectVeiculos.Core.Entities.Veiculos;

namespace ConnectVeiculos.Core.Entities.VeiculosObservacoes
{
    public class VeiculoObservacao
    {
        public int VeiObsId { get; private set; }
        public int R_VeiId { get; private set; }
        public int R_ObsId { get; private set; }

        // Navigation Properties
        public Veiculo Veiculo { get; private set; }
        public Observacao Observacao { get; private set; }

        public VeiculoObservacao() { }

        public VeiculoObservacao(int veiObsId, int rVeiId, int rObsId)
        {
            SetProperties(veiObsId, rVeiId, rObsId);
        }

        public void SetProperties(int veiObsId, int rVeiId, int rObsId)
        {
            VeiObsId = veiObsId;
            R_VeiId = rVeiId;
            R_ObsId = rObsId;
        }
    }
}
