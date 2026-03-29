namespace ConnectVeiculos.Core.Interfaces.Common
{
    /// <summary>
    /// Interface para entidades que suportam exclusao logica (soft delete)
    /// </summary>
    public interface ISoftDeletable
    {
        /// <summary>
        /// Indica se o registro esta excluido logicamente
        /// </summary>
        bool Excluido { get; }

        /// <summary>
        /// Data e hora da exclusao logica
        /// </summary>
        DateTime? ExcluidoEm { get; }

        /// <summary>
        /// Marca o registro como excluido logicamente
        /// </summary>
        void Excluir();

        /// <summary>
        /// Restaura o registro excluido logicamente
        /// </summary>
        void Restaurar();
    }
}
