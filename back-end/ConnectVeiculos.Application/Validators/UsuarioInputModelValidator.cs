using ConnectVeiculos.Application.InputModels.Usuarios;
using ConnectVeiculos.Core.Validators;
using FluentValidation;

namespace ConnectVeiculos.Application.Validators
{
    public class UsuarioInputModelValidator : AbstractValidator<UsuarioInputModel>
    {
        public UsuarioInputModelValidator()
        {
            RuleFor(x => x.R_LojId)
                .GreaterThan(0).WithMessage("Selecione uma loja.");

            RuleFor(x => x.R_AcsId)
                .GreaterThan(0).WithMessage("Selecione um acesso.");

            RuleFor(x => x.UsuNome)
                .NotEmpty().WithMessage("O nome e obrigatorio.")
                .MinimumLength(3).WithMessage("O nome deve ter no minimo 3 caracteres.")
                .MaximumLength(100).WithMessage("O nome deve ter no maximo 100 caracteres.");

            RuleFor(x => x.UsuEmail)
                .NotEmpty().WithMessage("O e-mail e obrigatorio.")
                .EmailAddress().WithMessage("E-mail invalido.")
                .MaximumLength(100).WithMessage("O e-mail deve ter no maximo 100 caracteres.");

            RuleFor(x => x.UsuCPF)
                .Must(cpf => string.IsNullOrEmpty(cpf) || CpfValidator.IsValid(cpf))
                .WithMessage("CPF invalido.");

            RuleFor(x => x.UsuSenha)
                .MinimumLength(6).WithMessage("A senha deve ter no minimo 6 caracteres.")
                .When(x => !string.IsNullOrEmpty(x.UsuSenha));

            RuleFor(x => x.UsuFuncao)
                .NotEmpty().WithMessage("A funcao e obrigatoria.")
                .Must(funcao => new[] { "Administrador", "Gerente", "Vendedor", "Visualizador" }.Contains(funcao))
                .WithMessage("Funcao invalida. Valores permitidos: Administrador, Gerente, Vendedor, Visualizador.");
        }
    }
}
