using ConnectVeiculos.Core.Entities.Notificacoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Notificacoes
{
    public interface INotificacaoRepository
    {
        Task<IEnumerable<Notificacao>> GetByUsuarioIdAsync(int usuarioId, bool apenasNaoLidas = false);
        Task<int> GetCountNaoLidasAsync(int usuarioId);
        Task<Notificacao> AddAsync(Notificacao notificacao);
        Task MarcarComoLidaAsync(int notificacaoId);
        Task MarcarTodasComoLidasAsync(int usuarioId);
        Task DeleteAntigasLidasAsync(DateTime dataLimite);
    }
}
