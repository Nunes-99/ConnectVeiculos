using System.ComponentModel.DataAnnotations;

namespace ConnectVeiculos.Application.InputModels.Auth
{
    public class TrocarSenhaInputModel
    {
        [Required(ErrorMessage = "A senha atual e obrigatoria")]
        public string SenhaAtual { get; set; } = string.Empty;

        [Required(ErrorMessage = "A nova senha e obrigatoria")]
        [MinLength(6, ErrorMessage = "A nova senha deve ter no minimo 6 caracteres")]
        public string NovaSenha { get; set; } = string.Empty;

        [Required(ErrorMessage = "A confirmacao de senha e obrigatoria")]
        [Compare("NovaSenha", ErrorMessage = "As senhas nao conferem")]
        public string ConfirmarSenha { get; set; } = string.Empty;
    }
}
