using System.ComponentModel.DataAnnotations;

namespace ConnectVeiculos.Application.InputModels.RecuperacaoSenha
{
    public class RedefinirSenhaInputModel
    {
        [Required(ErrorMessage = "O token e obrigatorio")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "A nova senha e obrigatoria")]
        [MinLength(6, ErrorMessage = "A senha deve ter no minimo 6 caracteres")]
        public string NovaSenha { get; set; } = string.Empty;

        [Required(ErrorMessage = "A confirmacao de senha e obrigatoria")]
        [Compare("NovaSenha", ErrorMessage = "As senhas nao conferem")]
        public string ConfirmarSenha { get; set; } = string.Empty;
    }
}
