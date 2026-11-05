using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Renave;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// Renave (estoque eletronico da Senatran/Serpro): configuracao da loja e
    /// as etapas de cada veiculo. Registrar entrada/saida tem efeito legal sobre
    /// o veiculo, por isso so Administrador e Gerente.
    /// </summary>
    [ApiController]
    [Route("api/renave")]
    [Authorize(Roles = "Administrador,Gerente")]
    [EnableRateLimiting("api")]
    public class RenaveController : ControllerBase
    {
        // Foto de documento assinado ou certificado: nada disso passa de alguns MB.
        private const long TamanhoMaximoArquivo = 8 * 1024 * 1024;

        private readonly IRenaveService _renave;

        public RenaveController(IRenaveService renave)
        {
            _renave = renave;
        }

        // ---------------- Configuracao ----------------

        [HttpGet("config")]
        public async Task<IActionResult> ObterConfig(CancellationToken ct)
            => Ok(await _renave.ObterConfigAsync(ct));

        [HttpPut("config")]
        public Task<IActionResult> SalvarConfig([FromBody] RenaveConfigInput input, CancellationToken ct)
            => Executar(() => _renave.SalvarConfigAsync(input, ct));

        [HttpPost("certificado")]
        [RequestSizeLimit(TamanhoMaximoArquivo)]
        public async Task<IActionResult> EnviarCertificado([FromForm] IFormFile arquivo, [FromForm] string senha, CancellationToken ct)
        {
            if (arquivo == null || arquivo.Length == 0) return BadRequest(new { error = "Envie o arquivo do certificado (.pfx ou .p12)." });
            var bytes = await LerAsync(arquivo, ct);
            return await Executar(() => _renave.SalvarCertificadoAsync(bytes, senha ?? "", ct));
        }

        [HttpDelete("certificado")]
        public Task<IActionResult> RemoverCertificado(CancellationToken ct)
            => Executar(() => _renave.RemoverCertificadoAsync(ct));

        [HttpPost("testar-conexao")]
        public Task<IActionResult> TestarConexao(CancellationToken ct)
            => Executar(() => _renave.TestarConexaoAsync(ct));

        [HttpGet("municipios")]
        public Task<IActionResult> Municipios([FromQuery] string uf, [FromQuery] string? nome, CancellationToken ct)
            => Executar(() => _renave.BuscarMunicipiosAsync(uf, nome, ct));

        // ---------------- Veiculos ----------------

        [HttpGet("veiculos/{veiculoId:int}")]
        public async Task<IActionResult> Situacao(int veiculoId, CancellationToken ct)
            => Ok(await _renave.ObterSituacaoAsync(veiculoId, ct));

        /// <summary>Situacao de varios veiculos de uma vez, para a lista do estoque.</summary>
        [HttpGet("veiculos")]
        public async Task<IActionResult> Situacoes([FromQuery] string ids, CancellationToken ct)
        {
            var lista = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0).Where(id => id > 0).Take(500);
            return Ok(await _renave.ObterSituacoesAsync(lista, ct));
        }

        [HttpPost("veiculos/{veiculoId:int}/atualizar")]
        public Task<IActionResult> Atualizar(int veiculoId, CancellationToken ct)
            => Executar(() => _renave.AtualizarSituacaoAsync(veiculoId, ct));

        [HttpPost("veiculos/{veiculoId:int}/aptidao")]
        public Task<IActionResult> Aptidao(int veiculoId, [FromBody] RenaveCrvInput input, CancellationToken ct)
            => Executar(() => _renave.ConsultarAptidaoAsync(veiculoId, input, ct));

        [HttpPost("veiculos/{veiculoId:int}/entrada")]
        public Task<IActionResult> Entrada(int veiculoId, [FromBody] RenaveEntradaInput input, CancellationToken ct)
            => Executar(() => _renave.SolicitarEntradaAsync(veiculoId, input, ct));

        [HttpPost("veiculos/{veiculoId:int}/assinatura-vendedor")]
        [RequestSizeLimit(TamanhoMaximoArquivo)]
        public async Task<IActionResult> AssinaturaVendedor(int veiculoId, [FromForm] string tipo, [FromForm] IFormFile foto, CancellationToken ct)
        {
            if (foto == null || foto.Length == 0) return BadRequest(new { error = "Envie a foto do documento assinado." });
            var bytes = await LerAsync(foto, ct);
            return await Executar(() => _renave.EnviarAssinaturaVendedorAsync(veiculoId, tipo, bytes, ct));
        }

        public class NotaFiscalInput
        {
            public string Evento { get; set; } = string.Empty;
            public string Chave { get; set; } = string.Empty;
        }

        [HttpPost("veiculos/{veiculoId:int}/nota-fiscal")]
        public Task<IActionResult> NotaFiscal(int veiculoId, [FromBody] NotaFiscalInput input, CancellationToken ct)
            => Executar(() => _renave.EnviarNotaFiscalAsync(veiculoId, input.Evento, input.Chave, ct));

        [HttpPost("veiculos/{veiculoId:int}/saida")]
        public Task<IActionResult> Saida(int veiculoId, [FromBody] RenaveSaidaInput input, CancellationToken ct)
            => Executar(() => _renave.SolicitarSaidaAsync(veiculoId, input, ct));

        [HttpGet("veiculos/{veiculoId:int}/atpv")]
        public async Task<IActionResult> Atpv(int veiculoId, CancellationToken ct)
        {
            try
            {
                var (pdf, numero) = await _renave.ObterAtpvAsync(veiculoId, ct);
                return File(pdf, "application/pdf", $"ATPVe-{(string.IsNullOrEmpty(numero) ? veiculoId.ToString() : numero)}.pdf");
            }
            catch (RenaveException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        [HttpGet("veiculos/{veiculoId:int}/termo-entrada")]
        public Task<IActionResult> TermoEntrada(int veiculoId, CancellationToken ct) => TermoAsync(veiculoId, false, ct);

        [HttpGet("veiculos/{veiculoId:int}/termo-saida")]
        public Task<IActionResult> TermoSaida(int veiculoId, CancellationToken ct) => TermoAsync(veiculoId, true, ct);

        private async Task<IActionResult> TermoAsync(int veiculoId, bool saida, CancellationToken ct)
        {
            try
            {
                var (pdf, numero) = await _renave.ObterTermoAsync(veiculoId, saida, ct);
                var nome = saida ? "Termo-saida" : "Termo-entrada";
                return File(pdf, "application/pdf", $"{nome}-{(string.IsNullOrEmpty(numero) ? veiculoId.ToString() : numero)}.pdf");
            }
            catch (RenaveException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        // ---------------- Estoque da loja no Renave ----------------

        /// <summary>Traz para o sistema o estoque que ja esta no Renave (nao faz nova entrada).</summary>
        [HttpPost("importar-estoque")]
        public Task<IActionResult> ImportarEstoque(CancellationToken ct)
            => Executar(() => _renave.ImportarEstoqueAsync(ct));

        [HttpGet("consulta")]
        public Task<IActionResult> Consultar([FromQuery] string? placa, [FromQuery] string? chassi, [FromQuery] string? renavam, CancellationToken ct)
            => Executar(() => _renave.ConsultarVeiculoAsync(placa, chassi, renavam, ct));

        // ---------------- Zero km e entregas ----------------

        [HttpGet("zero-km/pendentes")]
        public Task<IActionResult> ZeroKmPendentes(CancellationToken ct)
            => Executar(() => _renave.ListarZeroKmPendentesAsync(ct));

        [HttpPost("zero-km/entrada")]
        public Task<IActionResult> EntradaZeroKm([FromBody] RenaveEntradaZeroKmInput input, CancellationToken ct)
            => Executar(() => _renave.EntradaZeroKmAsync(input, ct));

        [HttpPost("zero-km/rejeicao")]
        public Task<IActionResult> RejeitarZeroKm([FromBody] RenaveRejeicaoZeroKmInput input, CancellationToken ct)
            => Executar(async () => { await _renave.RejeitarZeroKmAsync(input, ct); return new { ok = true }; });

        [HttpGet("entregas")]
        public Task<IActionResult> Entregas([FromQuery] string? estado, CancellationToken ct)
            => Executar(() => _renave.ListarEntregasAsync(estado, ct));

        [HttpPost("entregas/{idEntrega:long}/realizar")]
        public Task<IActionResult> RealizarEntrega(long idEntrega, CancellationToken ct)
            => Executar(() => _renave.RealizarEntregaAsync(idEntrega, ct));

        [HttpPost("entregas/{idEntrega:long}/cancelar")]
        public Task<IActionResult> CancelarEntrega(long idEntrega, CancellationToken ct)
            => Executar(() => _renave.CancelarEntregaAsync(idEntrega, ct));

        // ---------------- Homologacao ----------------

        [HttpPost("teste/veiculo")]
        public Task<IActionResult> CriarVeiculoTeste(CancellationToken ct)
            => Executar(() => _renave.CriarVeiculoTesteAsync(ct));

        [HttpPost("teste/zero-km")]
        public Task<IActionResult> CriarZeroKmTeste([FromQuery] bool acabado = true, CancellationToken ct = default)
            => Executar(() => _renave.CriarZeroKmTesteAsync(acabado, ct));

        [HttpPost("teste/veiculos/{veiculoId:int}/transferencia-detran")]
        public Task<IActionResult> SimularDetran(int veiculoId, CancellationToken ct)
            => Executar(() => _renave.SimularTransferenciaDetranAsync(veiculoId, ct));

        // ---------------- Auxiliares ----------------

        // Recusa do Renave ou de validacao vira 422 com o texto para a tela.
        private async Task<IActionResult> Executar<T>(Func<Task<T>> acao)
        {
            try
            {
                return Ok(await acao());
            }
            catch (RenaveException ex)
            {
                return UnprocessableEntity(new { error = ex.Message });
            }
        }

        private static async Task<byte[]> LerAsync(IFormFile arquivo, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await arquivo.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
    }
}
