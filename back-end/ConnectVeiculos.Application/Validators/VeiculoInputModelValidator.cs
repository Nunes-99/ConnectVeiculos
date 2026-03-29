using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Core.Validators;
using FluentValidation;

namespace ConnectVeiculos.Application.Validators
{
    public class VeiculoInputModelValidator : AbstractValidator<VeiculoInputModel>
    {
        public VeiculoInputModelValidator()
        {
            RuleFor(x => x.R_LojId)
                .GreaterThan(0).WithMessage("A loja e obrigatoria.");

            RuleFor(x => x.R_CatId)
                .GreaterThan(0).WithMessage("A categoria e obrigatoria.");

            RuleFor(x => x.VeiMarca)
                .NotEmpty().WithMessage("A marca e obrigatoria.")
                .MaximumLength(50).WithMessage("A marca deve ter no maximo 50 caracteres.");

            RuleFor(x => x.VeiModelo)
                .NotEmpty().WithMessage("O modelo e obrigatorio.")
                .MaximumLength(100).WithMessage("O modelo deve ter no maximo 100 caracteres.");

            RuleFor(x => x.VeiAno)
                .InclusiveBetween((short)1900, (short)(DateTime.Now.Year + 1))
                .WithMessage($"O ano deve estar entre 1900 e {DateTime.Now.Year + 1}.");

            RuleFor(x => x.VeiPlaca)
                .NotEmpty().WithMessage("A placa e obrigatoria.")
                .Must(PlacaValidator.IsValid).WithMessage("Placa invalida. Formatos aceitos: ABC-1234 ou ABC1D23 (Mercosul).");

            RuleFor(x => x.VeiChassi)
                .Must(chassi => string.IsNullOrEmpty(chassi) || ChassiValidator.IsValid(chassi))
                .WithMessage("Chassi invalido. Deve ter 17 caracteres alfanumericos.");

            RuleFor(x => x.VeiCor)
                .NotEmpty().WithMessage("A cor e obrigatoria.")
                .MaximumLength(30).WithMessage("A cor deve ter no maximo 30 caracteres.");

            RuleFor(x => x.VeiKm)
                .GreaterThanOrEqualTo(0).WithMessage("A quilometragem nao pode ser negativa.")
                .LessThan(10000000).WithMessage("Quilometragem invalida.");

            RuleFor(x => x.VeiPreco)
                .GreaterThan(0).WithMessage("O preco de venda deve ser maior que zero.");

            RuleFor(x => x.VeiPrecoCompra)
                .GreaterThanOrEqualTo(0).WithMessage("O preco de compra nao pode ser negativo.");

            RuleFor(x => x.VeiSts)
                .NotEmpty().WithMessage("O status e obrigatorio.")
                .Must(sts => new[] { "D", "V", "R", "I" }.Contains(sts))
                .WithMessage("Status invalido. Valores: D (Disponivel), V (Vendido), R (Reservado), I (Inativo).");
        }
    }
}
