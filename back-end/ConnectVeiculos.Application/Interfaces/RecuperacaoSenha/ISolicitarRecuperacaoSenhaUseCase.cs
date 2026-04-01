using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;

namespace ConnectVeiculos.Application.Interfaces.RecuperacaoSenha
{
    public interface ISolicitarRecuperacaoSenhaUseCase
    {
        Task<string?> ExecutarAsync(SolicitarRecuperacaoInputModel input);
    }
}
