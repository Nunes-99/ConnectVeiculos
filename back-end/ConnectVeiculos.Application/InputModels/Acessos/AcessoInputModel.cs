using ConnectVeiculos.Application.Exceptions;

namespace ConnectVeiculos.Application.InputModels.Acessos
{
    public class AcessoInputModel
    {
        public int AcsId { get; set; }
        public string AcsNome { get; set; }
        public string AcsDesc { get; set; }
        public bool AcsSts { get; set; }

        public AcessoInputModel() { }

        public AcessoInputModel(int acsId, string acsNome, string acsDesc, bool acsSts)
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
                throw new InputModelException("O nome do acesso é obrigatório.");
        }
    }
}
