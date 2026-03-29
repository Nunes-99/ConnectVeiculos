namespace ConnectVeiculos.Core.Interfaces.Database.Operations.Lojas
{
    public interface ILojaOperations
    {
        Task<dynamic> ConsultarVisualizacaoLojas(string pesquisa, string inicio, string intervalo);
        Task<dynamic> ConsultarManutencaoLoja(int id);
    }
}
