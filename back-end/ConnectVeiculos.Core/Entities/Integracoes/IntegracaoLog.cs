namespace ConnectVeiculos.Core.Entities.Integracoes
{
    public enum NivelIntegracaoLog
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    // Log estruturado de operacoes de integracao externa. Append-only —
    // jamais atualizar (audit trail). Codigo em kebab-case para facilitar
    // filtragem (ex: "oauth.callback.sucesso", "oauth.refresh.invalid-grant",
    // "item.publicar.erro").
    public class IntegracaoLog
    {
        public int IlgId { get; set; }
        public NivelIntegracaoLog IlgNivel { get; set; }
        public string IlgCodigo { get; set; }
        public string IlgMensagem { get; set; }
        // JSON livre com contexto adicional (status code, body de erro, IDs etc).
        // Nullable — eventos simples gravam só codigo + mensagem.
        public string IlgMetadadosJson { get; set; }
        public int? IlgUsuarioId { get; set; }
        public DateTime IlgCriadoEm { get; set; }
    }
}
