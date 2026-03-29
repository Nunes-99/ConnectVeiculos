using ConnectVeiculos.Core.Entities.Usuarios;

namespace ConnectVeiculos.Core.Entities.Notificacoes
{
    /// <summary>
    /// Entidade para notificacoes em tempo real
    /// </summary>
    public class Notificacao
    {
        public int NotId { get; private set; }
        public int R_UsuId { get; private set; }
        public string NotTitulo { get; private set; }
        public string NotMensagem { get; private set; }
        public string NotTipo { get; private set; }
        public string NotLink { get; private set; }
        public bool NotLida { get; private set; }
        public DateTime NotCriadaEm { get; private set; }
        public DateTime? NotLidaEm { get; private set; }

        // Navigation Properties
        public Usuario Usuario { get; private set; }

        public Notificacao() { }

        public Notificacao(int notId, int rUsuId, string notTitulo, string notMensagem,
            string notTipo, string notLink, bool notLida, DateTime notCriadaEm, DateTime? notLidaEm)
        {
            NotId = notId;
            R_UsuId = rUsuId;
            NotTitulo = notTitulo;
            NotMensagem = notMensagem;
            NotTipo = notTipo;
            NotLink = notLink;
            NotLida = notLida;
            NotCriadaEm = notCriadaEm;
            NotLidaEm = notLidaEm;
        }

        public static Notificacao Criar(int usuarioId, string titulo, string mensagem, NotificacaoTipo tipo, string link = null)
        {
            return new Notificacao(
                0,
                usuarioId,
                titulo,
                mensagem,
                tipo.ToString(),
                link,
                false,
                DateTime.UtcNow,
                null
            );
        }

        public void MarcarComoLida()
        {
            NotLida = true;
            NotLidaEm = DateTime.UtcNow;
        }
    }

    public enum NotificacaoTipo
    {
        NovaVenda,
        VeiculoReservado,
        EstoqueBaixo,
        NovoUsuario,
        AlteracaoPreco,
        Sistema
    }
}
