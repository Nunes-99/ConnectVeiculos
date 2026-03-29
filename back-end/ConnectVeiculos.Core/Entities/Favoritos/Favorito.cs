namespace ConnectVeiculos.Core.Entities.Favoritos
{
    public class Favorito
    {
        public int FavId { get; private set; }
        public int R_VeiId { get; private set; }
        public string FavEmail { get; private set; }
        public string FavNome { get; private set; }
        public string FavTelefone { get; private set; }
        public DateTime FavDtCriacao { get; private set; }

        public Favorito() { }

        public Favorito(int favId, int rVeiId, string favEmail, string favNome, string favTelefone)
        {
            FavId = favId;
            R_VeiId = rVeiId;
            FavEmail = favEmail;
            FavNome = favNome;
            FavTelefone = favTelefone;
            FavDtCriacao = DateTime.Now;
        }
    }
}
