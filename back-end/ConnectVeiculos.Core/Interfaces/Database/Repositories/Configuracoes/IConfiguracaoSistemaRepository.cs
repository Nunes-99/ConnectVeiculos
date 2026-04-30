using ConnectVeiculos.Core.Entities.Configuracoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes
{
    public interface IConfiguracaoSistemaRepository
    {
        Task<string> GetValorAsync(string chave);
        Task SetValorAsync(string chave, string valor);
    }
}
