using ConnectVeiculos.Application.InputModels.Veiculos;
using Microsoft.AspNetCore.Http;

namespace ConnectVeiculos.Application.Interfaces.Veiculos
{
    public interface IImportarVeiculosUseCase
    {
        /// <summary>
        /// Importa veiculos a partir de um arquivo CSV ou XML
        /// </summary>
        /// <param name="arquivo">Arquivo CSV ou XML</param>
        /// <param name="lojaId">ID da loja para associar os veiculos</param>
        /// <returns>Resultado da importacao com detalhes de sucesso/erro por linha</returns>
        Task<ImportacaoResultado> Execute(IFormFile arquivo, int lojaId);
    }
}
