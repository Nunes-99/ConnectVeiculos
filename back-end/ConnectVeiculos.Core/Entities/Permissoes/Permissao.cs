using ConnectVeiculos.Core.Entities.Acessos;
using ConnectVeiculos.Core.Entities.Usuarios;

namespace ConnectVeiculos.Core.Entities.Permissoes
{
    public class Permissao
    {
        public int UsuAcsId { get; private set; }
        public int R_UsuId { get; private set; }
        public int R_AcsId { get; private set; }
        public string AcsTp { get; private set; }

        // Navigation Properties
        public Usuario Usuario { get; private set; }
        public Acesso Acesso { get; private set; }

        public Permissao() { }

        public Permissao(int usuAcsId, int rUsuId, int rAcsId, string acsTp)
        {
            SetProperties(usuAcsId, rUsuId, rAcsId, acsTp);
        }

        public void SetProperties(int usuAcsId, int rUsuId, int rAcsId, string acsTp)
        {
            UsuAcsId = usuAcsId;
            R_UsuId = rUsuId;
            R_AcsId = rAcsId;
            AcsTp = acsTp;
        }
    }
}
