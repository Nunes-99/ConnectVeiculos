using ConnectVeiculos.Core.Exceptions;

namespace ConnectVeiculos.Core.Entities.Categorias
{
    public class Categoria
    {
        public int CatId { get; private set; }
        public string CatNome { get; private set; }
        public string CatDesc { get; private set; }
        public bool CatSts { get; private set; }

        public Categoria() { }

        public Categoria(int catId, string catNome, string catDesc, bool catSts)
        {
            SetProperties(catId, catNome, catDesc, catSts);
        }

        public void SetProperties(int catId, string catNome, string catDesc, bool catSts)
        {
            CatId = catId;
            CatNome = catNome;
            CatDesc = catDesc;
            CatSts = catSts;

            Validate();
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(CatNome))
                throw new DomainException("O nome da categoria é obrigatório.");

            if (CatNome.Length > 100)
                throw new DomainException("O nome da categoria deve ter no máximo 100 caracteres.");
        }

        public void AlterarStatus(bool novoStatus)
        {
            CatSts = novoStatus;
        }
    }
}
