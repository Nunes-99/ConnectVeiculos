namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Publica um veiculo recem-cadastrado nas plataformas externas, mas so
    /// depois que as fotos dele existirem.
    ///
    /// O cadastro sao duas requisicoes: primeiro o veiculo, depois as imagens
    /// (o upload precisa do id). Publicar dentro do cadastro, como era feito,
    /// pegava o veiculo sem nenhuma foto: o Instagram desistia em
    /// "sem imagens" e o Facebook postava so o texto.
    /// </summary>
    public interface IPublicacaoAutomaticaService
    {
        /// <summary>
        /// Aguarda as fotos chegarem e entao publica. Roda em background: nao
        /// prende a resposta do cadastro e nunca lanca pra quem chamou.
        /// </summary>
        Task PublicarNovoVeiculoAsync(int veiculoId);
    }
}
