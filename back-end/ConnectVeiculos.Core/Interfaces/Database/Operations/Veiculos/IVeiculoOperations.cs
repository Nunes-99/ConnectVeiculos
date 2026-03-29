namespace ConnectVeiculos.Core.Interfaces.Database.Operations.Veiculos
{
    public interface IVeiculoOperations
    {
        Task<dynamic> ConsultarVisualizacaoVeiculos(string pesquisa, int? lojaId, string inicio, string intervalo);
        Task<dynamic> ConsultarManutencaoVeiculo(int id);
        Task<dynamic> ConsultarVeiculoCompleto(int id);
    }
}
