using ConnectVeiculos.Core.Interfaces.Common;

namespace ConnectVeiculos.Core.Entities.Common
{
    /// <summary>
    /// Classe base para entidades que suportam exclusao logica
    /// </summary>
    public abstract class SoftDeletableEntity : ISoftDeletable
    {
        public bool Excluido { get; protected set; }
        public DateTime? ExcluidoEm { get; protected set; }

        public virtual void Excluir()
        {
            Excluido = true;
            ExcluidoEm = DateTime.UtcNow;
        }

        public virtual void Restaurar()
        {
            Excluido = false;
            ExcluidoEm = null;
        }
    }
}
