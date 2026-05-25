namespace ConnectVeiculos.Core.Entities.Integracoes
{
    // Status do ciclo de vida de uma integracao externa (ML, Facebook, Google, etc).
    public enum StatusIntegracao
    {
        Inativa = 0,
        AguardandoAutenticacao = 1,
        Ativa = 2,
        ComErro = 3
    }

    // Quando Status == ComErro, descreve a categoria da falha pra que a UI
    // mostre instrucao certa pro usuario (re-autenticar vs aguardar/retentar).
    public enum MotivoIntegracaoErro
    {
        Nenhum = 0,
        Autenticacao = 1, // refresh_token invalido, app revogado, conta desligada
        Conexao = 2       // timeouts, 5xx, rate limit
    }
}
