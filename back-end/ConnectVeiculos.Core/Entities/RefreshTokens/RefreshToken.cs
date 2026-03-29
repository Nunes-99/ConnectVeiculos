using ConnectVeiculos.Core.Entities.Usuarios;

namespace ConnectVeiculos.Core.Entities.RefreshTokens
{
    /// <summary>
    /// Entidade para refresh tokens JWT
    /// </summary>
    public class RefreshToken
    {
        public int RefId { get; private set; }
        public int R_UsuId { get; private set; }
        public string RefToken { get; private set; }
        public string RefJwtId { get; private set; }
        public DateTime RefCriadoEm { get; private set; }
        public DateTime RefExpiraEm { get; private set; }
        public bool RefUsado { get; private set; }
        public bool RefRevogado { get; private set; }
        public string RefSubstituidoPor { get; private set; }

        // Navigation Properties
        public Usuario Usuario { get; private set; }

        public RefreshToken() { }

        public RefreshToken(int refId, int rUsuId, string refToken, string refJwtId,
            DateTime refCriadoEm, DateTime refExpiraEm, bool refUsado, bool refRevogado, string refSubstituidoPor)
        {
            RefId = refId;
            R_UsuId = rUsuId;
            RefToken = refToken;
            RefJwtId = refJwtId;
            RefCriadoEm = refCriadoEm;
            RefExpiraEm = refExpiraEm;
            RefUsado = refUsado;
            RefRevogado = refRevogado;
            RefSubstituidoPor = refSubstituidoPor;
        }

        public static RefreshToken Criar(int usuarioId, string jwtId, int diasValidade = 7)
        {
            return new RefreshToken(
                0,
                usuarioId,
                Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                jwtId,
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(diasValidade),
                false,
                false,
                null
            );
        }

        public bool IsAtivo => !RefUsado && !RefRevogado && DateTime.UtcNow <= RefExpiraEm;

        public void MarcarComoUsado(string novoToken = null)
        {
            RefUsado = true;
            RefSubstituidoPor = novoToken;
        }

        public void Revogar()
        {
            RefRevogado = true;
        }
    }
}
