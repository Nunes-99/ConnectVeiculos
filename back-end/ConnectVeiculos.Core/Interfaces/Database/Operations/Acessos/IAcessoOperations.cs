namespace ConnectVeiculos.Core.Interfaces.Database.Operations.Acessos
{
    public interface IAcessoOperations
    {
        Task<dynamic> ConsultarVisualizacaoAcessos(string pesquisa, string inicio, string intervalo);
        Task<dynamic> ConsultarManutencaoAcesso(int id);
    }
}
