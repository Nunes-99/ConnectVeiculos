namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Botao "Publicar no Instagram / Facebook" da lista de veiculos. O post com
    /// capa e varias fotos leva mais que os 60s do nginx: o painel mostrava erro
    /// enquanto o post saia, e quem clicava de novo publicava em dobro. Entao o
    /// clique so confere o que da na hora (<see cref="PrepararAsync"/>), a
    /// publicacao roda em segundo plano (<see cref="PublicarAsync"/>) e o painel
    /// acompanha pela <see cref="Situacao"/>.
    /// </summary>
    public interface IPostManualService
    {
        /// <summary>Confere e reserva o carro. Falha volta com o motivo para o lojista.</summary>
        Task<PostManualSituacao> PrepararAsync(int veiculoId, string rede);

        /// <summary>Publica de fato (segundo plano) e registra a publicacao.</summary>
        Task PublicarAsync(int veiculoId, string rede, string? modeloLegenda);

        /// <summary>Andamento da ultima publicacao manual deste carro nesta rede.</summary>
        PostManualSituacao? Situacao(int veiculoId, string rede);
    }

    public class PostManualSituacao
    {
        public const string Publicando = "publicando";
        public const string Publicado = "publicado";
        public const string Falhou = "falhou";

        public string Estado { get; set; } = Publicando;
        public string Mensagem { get; set; } = "";
        public string? Url { get; set; }
        public DateTime Em { get; set; } = DateTime.UtcNow;
    }
}
