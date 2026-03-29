using ConnectVeiculos.Core.Exceptions;

namespace ConnectVeiculos.Core.Entities.Observacoes
{
    public class Observacao
    {
        public int ObsId { get; private set; }
        public string ObsNome { get; private set; }
        public bool ObsSts { get; private set; }

        public Observacao() { }

        public Observacao(int obsId, string obsNome, bool obsSts)
        {
            SetProperties(obsId, obsNome, obsSts);
        }

        public void SetProperties(int obsId, string obsNome, bool obsSts)
        {
            ObsId = obsId;
            ObsNome = obsNome;
            ObsSts = obsSts;

            Validate();
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(ObsNome))
                throw new DomainException("O nome da observação é obrigatório.");

            if (ObsNome.Length > 1000)
                throw new DomainException("O nome da observação deve ter no máximo 1000 caracteres.");
        }

        public void AlterarStatus(bool novoStatus)
        {
            ObsSts = novoStatus;
        }
    }
}
