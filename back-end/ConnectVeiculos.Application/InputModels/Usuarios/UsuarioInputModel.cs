using ConnectVeiculos.Application.Exceptions;

namespace ConnectVeiculos.Application.InputModels.Usuarios
{
    public class UsuarioInputModel
    {
        public int UsuId { get; set; }
        public int R_LojId { get; set; }
        public int R_AcsId { get; set; }
        public string UsuNome { get; set; }
        public string UsuCPF { get; set; }
        public string UsuRG { get; set; }
        public string UsuEmail { get; set; }
        public string UsuSenha { get; set; }
        public string UsuFuncao { get; set; }
        public bool UsuSts { get; set; }

        public UsuarioInputModel() { }

        public UsuarioInputModel(int usuId, string usuNome, string usuCPF, string usuRG,
            string usuEmail, string usuSenha, string usuFuncao, bool usuSts)
        {
            UsuId = usuId;
            UsuNome = usuNome;
            UsuCPF = usuCPF;
            UsuRG = usuRG;
            UsuEmail = usuEmail;
            UsuSenha = usuSenha;
            UsuFuncao = usuFuncao;
            UsuSts = usuSts;

            Validate();
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(UsuNome))
                throw new InputModelException("O nome do usuário é obrigatório.");

            if (string.IsNullOrWhiteSpace(UsuEmail))
                throw new InputModelException("O e-mail do usuário é obrigatório.");
        }
    }
}
