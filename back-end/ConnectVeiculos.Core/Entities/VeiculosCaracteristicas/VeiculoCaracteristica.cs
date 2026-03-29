using ConnectVeiculos.Core.Entities.Caracteristicas;
using ConnectVeiculos.Core.Entities.Veiculos;

namespace ConnectVeiculos.Core.Entities.VeiculosCaracteristicas
{
    public class VeiculoCaracteristica
    {
        public int VeiCarId { get; private set; }
        public int R_VeiId { get; private set; }
        public int R_CarId { get; private set; }

        // Navigation Properties
        public Veiculo Veiculo { get; private set; }
        public Caracteristica Caracteristica { get; private set; }

        public VeiculoCaracteristica() { }

        public VeiculoCaracteristica(int veiCarId, int rVeiId, int rCarId)
        {
            SetProperties(veiCarId, rVeiId, rCarId);
        }

        public void SetProperties(int veiCarId, int rVeiId, int rCarId)
        {
            VeiCarId = veiCarId;
            R_VeiId = rVeiId;
            R_CarId = rCarId;
        }
    }
}
