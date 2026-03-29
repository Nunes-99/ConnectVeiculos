using ConnectVeiculos.Application.Exceptions;

namespace ConnectVeiculos.Application.InputModels.Categorias
{
    public class CategoriaInputModel
    {
        public int CatId { get; set; }
        public string CatNome { get; set; }
        public string CatDesc { get; set; }
        public bool CatSts { get; set; }

        public CategoriaInputModel() { }

        public CategoriaInputModel(int catId, string catNome, string catDesc, bool catSts)
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
                throw new InputModelException("O nome da categoria é obrigatório.");
        }
    }
}
