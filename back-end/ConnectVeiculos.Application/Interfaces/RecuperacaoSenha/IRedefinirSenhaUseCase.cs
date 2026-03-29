using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;

namespace ConnectVeiculos.Application.Interfaces.RecuperacaoSenha
{
    public interface IRedefinirSenhaUseCase
    {
        Task ExecutarAsync(RedefinirSenhaInputModel input);
    }
}
