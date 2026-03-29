using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Usuarios;

namespace ConnectVeiculos.Core.Entities.LojasUsuarios
{
    public class LojaUsuario
    {
        public int LojUsuId { get; private set; }
        public int R_UsuId { get; private set; }
        public int R_LojId { get; private set; }
        public string UsuAcs { get; private set; }

        // Navigation Properties
        public Usuario Usuario { get; private set; }
        public Loja Loja { get; private set; }

        public LojaUsuario() { }

        public LojaUsuario(int lojUsuId, int rUsuId, int rLojId, string usuAcs)
        {
            SetProperties(lojUsuId, rUsuId, rLojId, usuAcs);
        }

        public void SetProperties(int lojUsuId, int rUsuId, int rLojId, string usuAcs)
        {
            LojUsuId = lojUsuId;
            R_UsuId = rUsuId;
            R_LojId = rLojId;
            UsuAcs = usuAcs;
        }
    }
}
