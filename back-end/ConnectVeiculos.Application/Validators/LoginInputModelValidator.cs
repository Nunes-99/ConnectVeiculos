using ConnectVeiculos.Application.InputModels.Auth;
using FluentValidation;

namespace ConnectVeiculos.Application.Validators
{
    public class LoginInputModelValidator : AbstractValidator<LoginInputModel>
    {
        public LoginInputModelValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("O e-mail e obrigatorio.")
                .EmailAddress().WithMessage("E-mail invalido.");

            RuleFor(x => x.Senha)
                .NotEmpty().WithMessage("A senha e obrigatoria.")
                .MinimumLength(6).WithMessage("A senha deve ter no minimo 6 caracteres.");
        }
    }
}
