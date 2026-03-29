namespace ConnectVeiculos.Core.Interfaces.Database.Operations.Vendas
{
    public interface IVendaOperations
    {
        Task<dynamic> ConsultarVisualizacaoVendas(string pesquisa, int? lojaId, string inicio, string intervalo);
        Task<dynamic> ConsultarManutencaoVenda(int id);
    }
}
