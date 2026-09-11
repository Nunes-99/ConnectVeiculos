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

        /// <summary>
        /// Devolve o veiculo pras plataformas quando ele volta a ficar disponivel —
        /// hoje so acontece ao estornar uma venda. Sem isso o carro voltava pro
        /// estoque mas continuava fora do ar: o anuncio do Mercado Livre ficava
        /// encerrado e o post do Facebook seguia carimbado de VENDIDO.
        ///
        /// Nao republica no Instagram: um post novo do mesmo carro apareceria
        /// duplicado no perfil, e o antigo nao pode ser editado nem deve sumir.
        /// </summary>
        Task ReativarVeiculoAsync(int veiculoId);

        /// <summary>
        /// Reescreve a legenda do post do Facebook com os dados atuais. Chamado
        /// quando o veiculo publicado muda de preco: o valor fica escrito na
        /// legenda, entao sem isso o post anuncia um preco que nao vale mais.
        /// </summary>
        Task AtualizarPostDoVeiculoAsync(int veiculoId, string statusVeiculo);
    }
}
