namespace ConnectVeiculos.Core.Interfaces.Tenancy
{
    /// <summary>
    /// Dispara trabalho em segundo plano preservando o tenant da request atual.
    ///
    /// Existe porque o padrao ingenuo — <c>_ = Task.Run(() => _servico.FazAlgo())</c>
    /// — captura servicos Scoped da request (DbContext incluso). Isso da dois
    /// problemas reais, ja observados em log:
    ///
    ///   1. Race: o Task.Run comeca antes da request terminar e dois fluxos usam
    ///      o mesmo DbContext ("A second operation was started on this context
    ///      instance before a previous operation completed").
    ///   2. Escopo descartado: se a request terminar primeiro, o DbContext ja foi
    ///      disposto e o trabalho morre silenciosamente.
    ///
    /// A implementacao captura a identidade do tenant enquanto o escopo da request
    /// ainda esta vivo e roda o trabalho num escopo de DI novo, com o
    /// <see cref="ITenantContext"/> ja resolvido para aquele tenant.
    /// </summary>
    public interface ITenantBackgroundRunner
    {
        /// <summary>
        /// Resolve <typeparamref name="TService"/> num escopo novo (do tenant atual)
        /// e executa <paramref name="trabalho"/>. Nao bloqueia o chamador e nunca
        /// propaga excecao — falha vira log, igual ao fire-and-forget que substitui.
        /// </summary>
        void Enqueue<TService>(Func<TService, Task> trabalho, string descricao) where TService : notnull;
    }
}
