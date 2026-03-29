using ConnectVeiculos.Core.Exceptions;

namespace ConnectVeiculos.Core.Entities.Acessos
{
    public class Acesso
    {
        public int AcsId { get; private set; }
        public string AcsNome { get; private set; }
        public string AcsDesc { get; private set; }
        public bool AcsSts { get; private set; }

        public Acesso() { }

        public Acesso(int acsId, string acsNome, string acsDesc, bool acsSts)
        {
            SetProperties(acsId, acsNome, acsDesc, acsSts);
        }

        public void SetProperties(int acsId, string acsNome, string acsDesc, bool acsSts)
        {
            AcsId = acsId;
            AcsNome = acsNome;
            AcsDesc = acsDesc;
            AcsSts = acsSts;

            Validate();
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(AcsNome))
                throw new DomainException("O nome do acesso é obrigatório.");

            if (AcsNome.Length > 100)
                throw new DomainException("O nome do acesso deve ter no máximo 100 caracteres.");
        }

        public void AlterarStatus(bool novoStatus)
        {
            AcsSts = novoStatus;
        }
    }
}
