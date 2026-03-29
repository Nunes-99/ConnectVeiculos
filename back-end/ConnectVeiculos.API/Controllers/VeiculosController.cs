using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// Alias para evitar conflito com o InputModel de busca
using BuscaAvancadaInputModel = ConnectVeiculos.Application.InputModels.Veiculos.BuscaAvancadaVeiculoInputModel;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Controller para gerenciamento de veiculos
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class VeiculosController : ControllerBase
    {
        #region GET

        /// <summary>
        /// Lista todos os veiculos com paginacao e filtros
        /// </summary>
        /// <param name="consultarVeiculosUseCase">Use case de consulta injetado</param>
        /// <param name="pesquisa">Texto para busca por marca, modelo ou placa</param>
        /// <param name="lojaId">ID da loja para filtrar</param>
        /// <param name="inicio">Indice inicial para paginacao</param>
        /// <param name="intervalo">Quantidade de registros por pagina</param>
        /// <returns>Lista de veiculos</returns>
        /// <response code="200">Lista de veiculos retornada com sucesso</response>
        /// <response code="401">Usuario nao autenticado</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConsultarVeiculos(
            [FromServices] IConsultarVeiculosUseCase consultarVeiculosUseCase,
            [FromQuery] string pesquisa = "",
            [FromQuery] int? lojaId = null,
            [FromQuery] string inicio = "0",
            [FromQuery] string intervalo = "50")
        {
            var veiculos = await consultarVeiculosUseCase.Execute(pesquisa, lojaId, inicio, intervalo);
            return Ok(veiculos);
        }

        /// <summary>
        /// Lista veiculos com paginacao
        /// </summary>
        [HttpGet("paged")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConsultarVeiculosPaginado(
            [FromServices] IConsultarVeiculosPaginadoUseCase consultarVeiculosPaginadoUseCase,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] int? lojaId = null)
        {
            var result = await consultarVeiculosPaginadoUseCase.Execute(page, pageSize, search, lojaId);
            return Ok(result);
        }

        /// <summary>
        /// Busca avancada de veiculos com multiplos filtros
        /// </summary>
        /// <param name="buscaAvancadaVeiculosUseCase">Use case de busca avancada injetado</param>
        /// <param name="input">Parametros de busca</param>
        /// <returns>Lista paginada de veiculos encontrados</returns>
        /// <response code="200">Busca realizada com sucesso</response>
        /// <response code="401">Usuario nao autenticado</response>
        /// <remarks>
        /// Exemplo de uso:
        ///
        /// GET /api/veiculos/busca-avancada?texto=toyota&amp;anoMinimo=2020&amp;precoMaximo=100000
        ///
        /// Filtros disponiveis:
        /// - texto: Busca livre em marca, modelo, placa, chassi e cor
        /// - marca: Filtro por marca especifica
        /// - modelo: Filtro por modelo especifico
        /// - anoMinimo/anoMaximo: Faixa de ano
        /// - precoMinimo/precoMaximo: Faixa de preco
        /// - kmMaximo: Quilometragem maxima
        /// - cor: Cor do veiculo
        /// - lojaId: ID da loja
        /// - categoriaId: ID da categoria
        /// - status: A (Ativo), I (Inativo), V (Vendido)
        /// - situacao: Situacao do veiculo
        /// - caracteristicasIds: Lista de IDs de caracteristicas
        /// - ordenarPor: preco, ano, km, dataentrada, marca
        /// - direcao: asc ou desc
        /// </remarks>
        [HttpGet("busca-avancada")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> BuscaAvancada(
            [FromServices] IBuscaAvancadaVeiculosUseCase buscaAvancadaVeiculosUseCase,
            [FromQuery] BuscaAvancadaVeiculoInputModel input)
        {
            var result = await buscaAvancadaVeiculosUseCase.Execute(input);
            return Ok(result);
        }

        /// <summary>
        /// Consulta um veiculo pelo ID
        /// </summary>
        /// <param name="consultarVeiculoPorIdUseCase">Use case de consulta injetado</param>
        /// <param name="id">ID do veiculo</param>
        /// <returns>Dados do veiculo</returns>
        /// <response code="200">Veiculo encontrado</response>
        /// <response code="401">Usuario nao autenticado</response>
        /// <response code="404">Veiculo nao encontrado</response>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ConsultarVeiculoPorId(
            [FromServices] IConsultarVeiculoPorIdUseCase consultarVeiculoPorIdUseCase,
            int id)
        {
            var veiculo = await consultarVeiculoPorIdUseCase.Execute(id);

            if (veiculo == null)
                return NotFound();

            return Ok(veiculo);
        }

        #endregion

        #region POST

        /// <summary>
        /// Cadastra um novo veiculo
        /// </summary>
        /// <param name="cadastrarVeiculoUseCase">Use case de cadastro injetado</param>
        /// <param name="inputModel">Dados do veiculo a ser cadastrado</param>
        /// <returns>ID do veiculo criado</returns>
        /// <response code="201">Veiculo criado com sucesso</response>
        /// <response code="400">Dados invalidos</response>
        /// <response code="401">Usuario nao autenticado</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CadastrarVeiculo(
            [FromServices] ICadastrarVeiculoUseCase cadastrarVeiculoUseCase,
            [FromBody] VeiculoInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados do veiculo nao informados.");

            var id = await cadastrarVeiculoUseCase.Execute(inputModel);
            return CreatedAtAction(nameof(ConsultarVeiculoPorId), new { id }, new { id });
        }

        /// <summary>
        /// Importa veiculos a partir de arquivo CSV ou XML
        /// </summary>
        /// <param name="importarVeiculosUseCase">Use case de importacao injetado</param>
        /// <param name="arquivo">Arquivo CSV ou XML com os veiculos</param>
        /// <param name="lojaId">ID da loja para associar os veiculos</param>
        /// <returns>Resultado da importacao com detalhes por linha</returns>
        /// <response code="200">Importacao processada (ver detalhes no retorno)</response>
        /// <response code="400">Arquivo invalido ou nao enviado</response>
        /// <response code="401">Usuario nao autenticado</response>
        /// <remarks>
        /// Formatos aceitos: CSV e XML
        ///
        /// Colunas do CSV (separador ; ou ,):
        /// marca, modelo, ano, placa, chassi, cor, km, preco, precoCompra, categoria, dataEntrada, status, situacao
        ///
        /// Estrutura XML:
        /// &lt;veiculos&gt;
        ///   &lt;veiculo&gt;
        ///     &lt;marca&gt;Toyota&lt;/marca&gt;
        ///     &lt;modelo&gt;Corolla&lt;/modelo&gt;
        ///     ...
        ///   &lt;/veiculo&gt;
        /// &lt;/veiculos&gt;
        /// </remarks>
        [HttpPost("importar")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ImportarVeiculos(
            [FromServices] IImportarVeiculosUseCase importarVeiculosUseCase,
            [FromForm] IFormFile arquivo,
            [FromForm] int lojaId)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Arquivo nao enviado ou vazio.");

            if (lojaId <= 0)
                return BadRequest("ID da loja e obrigatorio.");

            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            if (extensao != ".csv" && extensao != ".xml")
                return BadRequest("Formato de arquivo nao suportado. Use CSV ou XML.");

            var resultado = await importarVeiculosUseCase.Execute(arquivo, lojaId);
            return Ok(resultado);
        }

        #endregion

        #region PUT

        /// <summary>
        /// Atualiza um veiculo existente
        /// </summary>
        /// <param name="atualizarVeiculoUseCase">Use case de atualizacao injetado</param>
        /// <param name="id">ID do veiculo a ser atualizado</param>
        /// <param name="inputModel">Novos dados do veiculo</param>
        /// <returns>Sem conteudo</returns>
        /// <response code="204">Veiculo atualizado com sucesso</response>
        /// <response code="400">Dados invalidos</response>
        /// <response code="401">Usuario nao autenticado</response>
        /// <response code="404">Veiculo nao encontrado</response>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AtualizarVeiculo(
            [FromServices] IAtualizarVeiculoUseCase atualizarVeiculoUseCase,
            int id,
            [FromBody] VeiculoInputModel inputModel)
        {
            if (inputModel == null)
                return BadRequest("Dados do veiculo nao informados.");

            inputModel.VeiId = id;
            await atualizarVeiculoUseCase.Execute(inputModel);
            return NoContent();
        }

        [HttpPut("{id}/social-status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AtualizarStatusSocial(
            int id,
            [FromBody] AtualizarSocialRequest request,
            [FromServices] ConnectVeiculos.Infrastructure.Database.EntityFramework.ConnectVeiculosDbContext context)
        {
            var veiculo = await context.Veiculos.FindAsync(id);
            if (veiculo == null) return NotFound();

            if (request.Rede == "instagram")
                veiculo.MarcarPostadoInstagram(request.Postado);
            else if (request.Rede == "facebook")
                veiculo.MarcarPostadoFacebook(request.Postado);

            await context.SaveChangesAsync();
            return NoContent();
        }

        #endregion

        #region DELETE

        /// <summary>
        /// Inativa um veiculo (exclusao logica)
        /// </summary>
        /// <param name="inativarVeiculoUseCase">Use case de inativacao injetado</param>
        /// <param name="id">ID do veiculo a ser inativado</param>
        /// <returns>Sem conteudo</returns>
        /// <response code="204">Veiculo inativado com sucesso</response>
        /// <response code="401">Usuario nao autenticado</response>
        /// <response code="404">Veiculo nao encontrado</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> InativarVeiculo(
            [FromServices] IInativarVeiculoUseCase inativarVeiculoUseCase,
            int id)
        {
            await inativarVeiculoUseCase.Execute(id);
            return NoContent();
        }

        #endregion
    }

    public class AtualizarSocialRequest
    {
        public string Rede { get; set; }
        public bool Postado { get; set; }
    }
}
