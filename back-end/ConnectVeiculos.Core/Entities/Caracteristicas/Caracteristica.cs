using ConnectVeiculos.Core.Exceptions;

namespace ConnectVeiculos.Core.Entities.Caracteristicas
{
    public class Caracteristica
    {
        public int CarId { get; private set; }
        public string CarNome { get; private set; }
        public bool CarSts { get; private set; }

        public Caracteristica() { }

        public Caracteristica(int carId, string carNome, bool carSts)
        {
            SetProperties(carId, carNome, carSts);
        }

        public void SetProperties(int carId, string carNome, bool carSts)
        {
            CarId = carId;
            CarNome = carNome;
            CarSts = carSts;

            Validate();
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(CarNome))
                throw new DomainException("O nome da característica é obrigatório.");

            if (CarNome.Length > 100)
                throw new DomainException("O nome da característica deve ter no máximo 100 caracteres.");
        }

        public void AlterarStatus(bool novoStatus)
        {
            CarSts = novoStatus;
        }
    }
}
