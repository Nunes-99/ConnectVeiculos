using ConnectVeiculos.Application.InputModels.Auth;
using ConnectVeiculos.Application.ViewModels.Auth;

namespace ConnectVeiculos.Application.Interfaces.Auth
{
    public interface ILoginUseCase
    {
        Task<LoginViewModel?> Execute(LoginInputModel input);
    }
}
