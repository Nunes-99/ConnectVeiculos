using System.ComponentModel.DataAnnotations;

namespace ConnectVeiculos.Application.InputModels.RecuperacaoSenha
{
    public class SolicitarRecuperacaoInputModel
    {
        [Required(ErrorMessage = "O e-mail e obrigatorio")]
        [EmailAddress(ErrorMessage = "E-mail invalido")]
        public string Email { get; set; } = string.Empty;
    }
}
