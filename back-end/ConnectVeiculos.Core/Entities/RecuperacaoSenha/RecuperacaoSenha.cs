namespace ConnectVeiculos.Core.Entities.RecuperacaoSenha
{
    public class RecuperacaoSenha
    {
        public int RecId { get; private set; }
        public int RecUsuId { get; private set; }
        public string RecToken { get; private set; }
        public DateTime RecDataCriacao { get; private set; }
        public DateTime RecDataExpiracao { get; private set; }
        public bool RecUtilizado { get; private set; }

        public RecuperacaoSenha() { }

        public RecuperacaoSenha(int recUsuId, string recToken, DateTime recDataExpiracao)
        {
            RecUsuId = recUsuId;
            RecToken = recToken;
            RecDataCriacao = DateTime.Now;
            RecDataExpiracao = recDataExpiracao;
            RecUtilizado = false;
        }

        public bool IsValido()
        {
            return !RecUtilizado && DateTime.Now < RecDataExpiracao;
        }

        public void MarcarComoUtilizado()
        {
            RecUtilizado = true;
        }
    }
}
