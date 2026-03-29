using ConnectVeiculos.Core.Exceptions;

namespace ConnectVeiculos.Core.Entities.Usuarios
{
    public class Usuario
    {
        public int UsuId { get; private set; }
        public string UsuNome { get; private set; }
        public string UsuCPF { get; private set; }
        public string UsuRG { get; private set; }
        public string UsuEmail { get; private set; }
        public string UsuSenha { get; private set; }
        public string UsuFuncao { get; private set; }
        public bool UsuSts { get; private set; }

        public Usuario() { }

        public Usuario(int usuId, string usuNome, string usuCPF, string usuRG,
            string usuEmail, string usuSenha, string usuFuncao, bool usuSts)
        {
            SetProperties(usuId, usuNome, usuCPF, usuRG, usuEmail, usuSenha, usuFuncao, usuSts);
        }

        public void SetProperties(int usuId, string usuNome, string usuCPF, string usuRG,
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
                throw new UsuarioException("O nome do usuário é obrigatório.");

            if (UsuNome.Length > 200)
                throw new UsuarioException("O nome do usuário deve ter no máximo 200 caracteres.");

            if (string.IsNullOrWhiteSpace(UsuEmail))
                throw new UsuarioException("O e-mail do usuário é obrigatório.");

            if (UsuEmail.Length > 255)
                throw new UsuarioException("O e-mail do usuário deve ter no máximo 255 caracteres.");

            if (string.IsNullOrWhiteSpace(UsuSenha))
                throw new UsuarioException("A senha do usuário é obrigatória.");
        }

        public void AlterarStatus(bool novoStatus)
        {
            UsuSts = novoStatus;
        }

        public void AlterarSenha(string novaSenha)
        {
            if (string.IsNullOrWhiteSpace(novaSenha))
                throw new UsuarioException("A nova senha é obrigatória.");

            UsuSenha = novaSenha;
        }
    }
}
