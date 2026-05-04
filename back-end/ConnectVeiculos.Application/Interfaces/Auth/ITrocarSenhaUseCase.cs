using ConnectVeiculos.Application.InputModels.Auth;

namespace ConnectVeiculos.Application.Interfaces.Auth
{
    public interface ITrocarSenhaUseCase
    {
        Task ExecutarAsync(int usuarioId, TrocarSenhaInputModel input);
    }
}
