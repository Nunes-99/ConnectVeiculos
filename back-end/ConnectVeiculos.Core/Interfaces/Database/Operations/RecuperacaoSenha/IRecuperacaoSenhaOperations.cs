using ConnectVeiculos.Core.Entities.RecuperacaoSenha;

namespace ConnectVeiculos.Core.Interfaces.Database.Operations.RecuperacaoSenha
{
    public interface IRecuperacaoSenhaOperations
    {
        Task<int> InserirAsync(Entities.RecuperacaoSenha.RecuperacaoSenha recuperacao);
        Task<Entities.RecuperacaoSenha.RecuperacaoSenha?> ObterPorTokenAsync(string token);
        Task AtualizarAsync(Entities.RecuperacaoSenha.RecuperacaoSenha recuperacao);
        Task InvalidarTokensAnterioresAsync(int usuarioId);
    }
}
