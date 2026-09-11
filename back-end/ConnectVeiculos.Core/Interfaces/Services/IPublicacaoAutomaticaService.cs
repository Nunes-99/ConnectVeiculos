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

        /// <summary>
        /// Tira o veiculo das plataformas quando ele deixa de estar disponivel:
        /// encerra o anuncio do Mercado Livre, remove do catalogo do Facebook e do
        /// Google e carimba VENDIDO / RESERVADO no post da Page.
        ///
        /// Existe num lugar so porque ha dois caminhos pra um carro sair de
        /// circulacao — editar o veiculo e registrar a venda — e por muito tempo
        /// so o primeiro avisava as plataformas.
        /// </summary>
        Task MarcarVeiculoIndisponivelAsync(int veiculoId, string novoStatus);
    }
}
