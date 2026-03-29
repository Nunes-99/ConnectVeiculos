using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Entities.Veiculos;

namespace ConnectVeiculos.Core.Entities.HistoricoPrecos
{
    /// <summary>
    /// Entidade para registro de historico de precos dos veiculos
    /// </summary>
    public class HistoricoPreco
    {
        public int HisId { get; private set; }
        public int R_VeiId { get; private set; }
        public int R_UsuId { get; private set; }
        public decimal HisPrecoAnterior { get; private set; }
        public decimal HisPrecoNovo { get; private set; }
        public DateTime HisDataAlteracao { get; private set; }
        public string HisMotivo { get; private set; }

        // Navigation Properties
        public Veiculo Veiculo { get; private set; }
        public Usuario Usuario { get; private set; }

        public HistoricoPreco() { }

        public HistoricoPreco(int hisId, int rVeiId, int rUsuId, decimal hisPrecoAnterior,
            decimal hisPrecoNovo, DateTime hisDataAlteracao, string hisMotivo)
        {
            HisId = hisId;
            R_VeiId = rVeiId;
            R_UsuId = rUsuId;
            HisPrecoAnterior = hisPrecoAnterior;
            HisPrecoNovo = hisPrecoNovo;
            HisDataAlteracao = hisDataAlteracao;
            HisMotivo = hisMotivo;
        }

        public static HistoricoPreco Criar(int veiculoId, int usuarioId, decimal precoAnterior, decimal precoNovo, string motivo = null)
        {
            return new HistoricoPreco(0, veiculoId, usuarioId, precoAnterior, precoNovo, DateTime.UtcNow, motivo);
        }
    }
}
