using ConnectVeiculos.Core.Webmotors;

namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Anuncios no estoque da loja na Webmotors (WebService SOAP "Integracao
    /// Revendedor"). Cada loja informa o usuario de integracao dela (CNPJ,
    /// e-mail e senha que a Webmotors fornece); as chamadas saem do servidor,
    /// cujo IP a Webmotors libera.
    /// </summary>
    public interface IWebmotorsService
    {
        Task<WebmotorsStatus> StatusAsync();
        Task<WebmotorsStatus> ConectarAsync(WebmotorsConectarInput input);
        Task<WebmotorsStatus> EscolherModalidadeAsync(long modalidadeId);
        Task DesconectarAsync();

        /// <summary>Tabelas: marcas, cores, cambios, combustiveis, portas, opcionais.</summary>
        Task<IReadOnlyList<WebmotorsItem>> TabelaAsync(string tabela);
        Task<IReadOnlyList<WebmotorsItem>> ModelosAsync(long marcaId);
        Task<IReadOnlyList<WebmotorsVersao>> VersoesAsync(long modeloId, int? ano);

        Task<WebmotorsVeiculoInfo> ObterVeiculoAsync(int veiculoId);

        /// <summary>Salva os codigos e inclui (ou altera) o anuncio, com fotos.</summary>
        Task<WebmotorsVeiculoInfo> PublicarAsync(int veiculoId, WebmotorsAnuncioDados dados);

        Task<WebmotorsVeiculoInfo> RemoverAsync(int veiculoId);

        /// <summary>
        /// Acerta o anuncio com o estado atual do veiculo: altera se continua
        /// disponivel, exclui se saiu, inclui de novo se voltou. Nao faz nada
        /// para veiculo que nunca foi anunciado. Chamado depois de editar,
        /// vender, estornar ou inativar.
        /// </summary>
        Task SincronizarVeiculoAsync(int veiculoId);
    }
}
