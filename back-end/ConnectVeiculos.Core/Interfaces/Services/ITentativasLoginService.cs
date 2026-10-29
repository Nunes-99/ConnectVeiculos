namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Freio de forca bruta por CONTA, complementar ao rate limiter por IP.
    ///
    /// O limiter do pipeline particiona por IP e nao consegue ver o e-mail
    /// (o corpo da request ainda nao foi lido). Numa revenda atras de NAT todos
    /// os funcionarios saem pelo mesmo IP, entao um limite baixo ali punia o
    /// colega que so errou a senha. Aqui, com o e-mail em maos, o bloqueio segue
    /// a conta.
    ///
    /// Com <paramref name="ip"/>, o bloqueio curto vale para conta + IP: antes
    /// era so por conta, e qualquer um que soubesse o e-mail do admin (o da loja
    /// aparece no catalogo) o mantinha trancado errando a senha 5 vezes a cada
    /// 15 min. Um teto alto por conta, somando todos os IPs, segura a forca
    /// bruta distribuida.
    /// </summary>
    public interface ITentativasLoginService
    {
        /// <summary>Segundos restantes de bloqueio, ou null se a conta esta liberada.</summary>
        int? SegundosBloqueioRestantes(string email, string? ip = null);

        /// <summary>Contabiliza uma tentativa malsucedida.</summary>
        void RegistrarFalha(string email, string? ip = null);

        /// <summary>Zera o contador — chamar apos login bem-sucedido.</summary>
        void LimparFalhas(string email, string? ip = null);
    }
}
