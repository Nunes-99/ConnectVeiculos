namespace ConnectVeiculos.Core.Interfaces.Database.Operations.Categorias
{
    public interface ICategoriaOperations
    {
        Task<dynamic> ConsultarVisualizacaoCategorias(string pesquisa, string inicio, string intervalo);
        Task<dynamic> ConsultarManutencaoCategoria(int id);
    }
}
