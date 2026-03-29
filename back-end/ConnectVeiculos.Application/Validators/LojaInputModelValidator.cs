using ConnectVeiculos.Application.InputModels.Lojas;
using ConnectVeiculos.Core.Validators;
using FluentValidation;

namespace ConnectVeiculos.Application.Validators
{
    public class LojaInputModelValidator : AbstractValidator<LojaInputModel>
    {
        public LojaInputModelValidator()
        {
            RuleFor(x => x.LojNome)
                .NotEmpty().WithMessage("O nome da loja e obrigatorio.")
                .MaximumLength(100).WithMessage("O nome da loja deve ter no maximo 100 caracteres.");

            RuleFor(x => x.LojCNPJ)
                .Must(cnpj => string.IsNullOrEmpty(cnpj) || CnpjValidator.IsValid(cnpj))
                .WithMessage("CNPJ invalido.");

            RuleFor(x => x.LojEmail)
                .EmailAddress().When(x => !string.IsNullOrEmpty(x.LojEmail))
                .WithMessage("E-mail da loja invalido.");

            RuleFor(x => x.LojCEP)
                .Matches(@"^\d{5}-?\d{3}$").When(x => !string.IsNullOrEmpty(x.LojCEP))
                .WithMessage("CEP invalido. Use o formato 00000-000.");

            RuleFor(x => x.LojEstado)
                .Length(2).When(x => !string.IsNullOrEmpty(x.LojEstado))
                .WithMessage("O estado deve ter 2 caracteres (UF).");

            RuleFor(x => x.LojTel1)
                .Matches(@"^[\d\s\-\(\)]+$").When(x => !string.IsNullOrEmpty(x.LojTel1))
                .WithMessage("Telefone 1 invalido.");

            RuleFor(x => x.LojTel2)
                .Matches(@"^[\d\s\-\(\)]+$").When(x => !string.IsNullOrEmpty(x.LojTel2))
                .WithMessage("Telefone 2 invalido.");

            RuleFor(x => x.LojWhatsApp)
                .Matches(@"^[\d\s\-\(\)]+$").When(x => !string.IsNullOrEmpty(x.LojWhatsApp))
                .WithMessage("WhatsApp invalido.");
        }
    }
}
