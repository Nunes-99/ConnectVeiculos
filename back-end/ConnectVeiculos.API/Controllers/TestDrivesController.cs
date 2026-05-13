using ConnectVeiculos.Core.Entities.TestDrives;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestDrivesController : ControllerBase
    {
        private readonly ConnectVeiculosDbContext _context;

        public TestDrivesController(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        // POST publico - agendar test drive
        [HttpPost]
        public async Task<IActionResult> Agendar([FromBody] AgendarTestDriveRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NomeCliente) || string.IsNullOrWhiteSpace(request.Telefone))
                return BadRequest("Nome e telefone sao obrigatorios.");
            if (request.DataAgendamento < DateTime.Today)
                return BadRequest("Data de agendamento não pode ser no passado.");
            if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
                return BadRequest("E-mail invalido.");

            var testDrive = new TestDrive(0, request.VeiculoId, request.LojaId, request.NomeCliente,
                request.Telefone, request.WhatsApp, request.Email, request.DataAgendamento, request.Horario, request.Observacao, "P");

            _context.TestDrives.Add(testDrive);
            await _context.SaveChangesAsync();

            return Ok(new { id = testDrive.TdrId, mensagem = "Test drive agendado com sucesso!" });
        }

        // GET - listar test drives (autenticado)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Listar([FromQuery] int? lojaId = null, [FromQuery] string? status = null)
        {
            var query = _context.TestDrives.AsQueryable();
            if (lojaId.HasValue) query = query.Where(t => t.R_LojId == lojaId.Value);
            if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.TdrStatus == status);
            var result = await query.OrderByDescending(t => t.TdrDataAgendamento)
                .Select(t => new {
                    t.TdrId, t.R_VeiId, t.R_LojId,
                    t.TdrNomeCliente, t.TdrTelefone, t.TdrWhatsApp, t.TdrEmail,
                    t.TdrDataAgendamento, t.TdrHorario, t.TdrObservacao,
                    t.TdrStatus, t.TdrDtCriacao,
                    VeiculoNome = _context.Veiculos
                        .Where(v => v.VeiId == t.R_VeiId)
                        .Select(v => v.VeiMarca + " " + v.VeiModelo + " " + v.VeiAno)
                        .FirstOrDefault() ?? ""
                }).ToListAsync();
            return Ok(result);
        }

        // PUT - atualizar status. Dispara notificacao WhatsApp se aplicavel.
        [HttpPut("{id}/status")]
        [Authorize]
        public async Task<IActionResult> AtualizarStatus(
            int id,
            [FromBody] AtualizarStatusRequest request,
            [FromServices] ITestDriveNotificacaoService notificacao)
        {
            var testDrive = await _context.TestDrives.FindAsync(id);
            if (testDrive == null) return NotFound();
            testDrive.AlterarStatus(request.Status);
            await _context.SaveChangesAsync();

            // Notifica cliente via WhatsApp apenas pra Confirmacao (C) e Cancelamento (X).
            // Realizado (R) e Pendente (P) nao geram mensagem.
            TestDriveNotificacaoResult? notif = null;
            if (request.Status == "C")
                notif = await notificacao.NotificarConfirmacaoAsync(testDrive);
            else if (request.Status == "X")
                notif = await notificacao.NotificarCancelamentoAsync(testDrive);

            return Ok(new
            {
                statusAtualizado = true,
                notificacao = notif == null
                    ? new { aplicavel = false, enviada = false, motivo = "nao-aplicavel", erro = (string?)null }
                    : new { aplicavel = true, enviada = notif.Enviada, motivo = notif.Motivo, erro = notif.MensagemErro }
            });
        }
    }

    public class AgendarTestDriveRequest
    {
        public int VeiculoId { get; set; }
        public int? LojaId { get; set; }
        public string NomeCliente { get; set; }
        public string Telefone { get; set; }
        public string WhatsApp { get; set; }
        public string Email { get; set; }
        public DateTime DataAgendamento { get; set; }
        public string Horario { get; set; }
        public string Observacao { get; set; }
    }

    public class AtualizarStatusRequest
    {
        public string Status { get; set; }
    }
}
