namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Freio de forca bruta por CONTA, complementar ao rate limiter por IP.
    ///
    /// O limiter do pipeline particiona por host+IP e nao consegue ver o e-mail
    /// (o corpo da request ainda nao foi lido). Numa revenda atras de NAT todos
    /// os funcionarios saem pelo mesmo IP, entao um limite baixo ali punia o
    /// colega que so errou a senha. Aqui, com o e-mail em maos, o bloqueio segue
    /// a conta — inclusive quando o atacante troca de IP.
    /// </summary>
    public interface ITentativasLoginService
    {
        /// <summary>Segundos restantes de bloqueio, ou null se a conta esta liberada.</summary>
        int? SegundosBloqueioRestantes(string email);

        /// <summary>Contabiliza uma tentativa malsucedida.</summary>
        void RegistrarFalha(string email);

        /// <summary>Zera o contador — chamar apos login bem-sucedido.</summary>
        void LimparFalhas(string email);
    }
}
