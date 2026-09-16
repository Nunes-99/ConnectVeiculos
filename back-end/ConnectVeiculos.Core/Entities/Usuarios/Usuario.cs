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

        /// <summary>
        /// Marca que a senha atual foi gerada pelo sistema e mandada por e-mail,
        /// entao um terceiro a conhece: ela vale para o primeiro acesso e nada
        /// mais. Enquanto estiver ligado, o sistema exige a troca antes de
        /// liberar qualquer tela.
        /// </summary>
        public bool UsuTrocarSenha { get; private set; }

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
            // Senha escolhida pelo proprio usuario: a exigencia deixa de existir.
            UsuTrocarSenha = false;
        }

        /// <summary>
        /// Usado no cadastro, quando a senha e gerada pelo sistema e enviada por
        /// e-mail.
        /// </summary>
        public void ExigirTrocaDeSenha()
        {
            UsuTrocarSenha = true;
        }
    }
}
