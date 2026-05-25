namespace ConnectVeiculos.Core.Entities.Integracoes
{
    // Estado completo da integracao com Mercado Livre para o tenant atual.
    // Substitui o conjunto de chaves ML_* na tabela Configuracoes; abriga
    // tokens cifrados, status do ciclo de vida e contadores de observabilidade.
    // Singleton por tenant (1 registro por base SQLite).
    public class IntegracaoMercadoLivre
    {
        public int IntId { get; set; }

        public StatusIntegracao IntStatus { get; set; }
        public MotivoIntegracaoErro IntMotivoErro { get; set; }
        public int IntFalhasConsecutivasSync { get; set; }

        // Tokens armazenados cifrados pelo ITokenProtector (DataProtection).
        // String opaca — nao usar fora do MercadoLivreService.
        public string IntAccessTokenCifrado { get; set; }
        public string IntRefreshTokenCifrado { get; set; }
        public DateTime? IntAccessTokenExpiraEm { get; set; }

        // Identidade da conta ML conectada (best-effort — preenchidos pelo
        // callback ou pela primeira chamada a /users/me).
        public string IntSellerId { get; set; }
        public string IntMlNickname { get; set; }
        public string IntMlEmail { get; set; }

        public DateTime? IntAutenticadaEm { get; set; }
        public DateTime IntCriadaEm { get; set; }
        public DateTime? IntAtualizadaEm { get; set; }
    }
}
