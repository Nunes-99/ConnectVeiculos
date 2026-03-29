using ConnectVeiculos.Application.InputModels.Vendas;
using ConnectVeiculos.Core.Validators;
using FluentValidation;

namespace ConnectVeiculos.Application.Validators
{
    public class VendaInputModelValidator : AbstractValidator<VendaInputModel>
    {
        public VendaInputModelValidator()
        {
            RuleFor(x => x.R_VeiId)
                .GreaterThan(0).WithMessage("O veiculo e obrigatorio.");

            RuleFor(x => x.R_UsuId)
                .GreaterThan(0).WithMessage("O vendedor e obrigatorio.");

            RuleFor(x => x.VenDtVenda)
                .NotEmpty().WithMessage("A data da venda e obrigatoria.")
                .LessThanOrEqualTo(DateTime.Now.AddDays(1)).WithMessage("A data da venda nao pode ser futura.");

            RuleFor(x => x.VenValor)
                .GreaterThan(0).WithMessage("O valor da venda deve ser maior que zero.");

            RuleFor(x => x.VenComissaoPorc)
                .InclusiveBetween(0, 100).WithMessage("A comissao deve estar entre 0% e 100%.");

            // Dados do Comprador
            RuleFor(x => x.VenCompradorNome)
                .NotEmpty().WithMessage("O nome do comprador e obrigatorio.")
                .MaximumLength(100).WithMessage("O nome do comprador deve ter no maximo 100 caracteres.");

            RuleFor(x => x.VenCompradorCpf)
                .Must(cpf => string.IsNullOrEmpty(cpf) || CpfValidator.IsValid(cpf) || CnpjValidator.IsValid(cpf))
                .WithMessage("CPF/CNPJ do comprador invalido.");

            RuleFor(x => x.VenCompradorTelefone)
                .MaximumLength(20).WithMessage("O telefone deve ter no maximo 20 caracteres.")
                .Matches(@"^[\d\s\-\(\)]+$").When(x => !string.IsNullOrEmpty(x.VenCompradorTelefone))
                .WithMessage("Telefone invalido.");

            RuleFor(x => x.VenCompradorEmail)
                .EmailAddress().When(x => !string.IsNullOrEmpty(x.VenCompradorEmail))
                .WithMessage("E-mail do comprador invalido.");

            RuleFor(x => x.VenFormaPagamento)
                .NotEmpty().WithMessage("A forma de pagamento e obrigatoria.")
                .Must(fp => new[] { "DINHEIRO", "PIX", "CARTAO_CREDITO", "CARTAO_DEBITO", "FINANCIAMENTO", "CONSORCIO", "TROCA", "MISTO" }.Contains(fp))
                .WithMessage("Forma de pagamento invalida.");
        }
    }
}
