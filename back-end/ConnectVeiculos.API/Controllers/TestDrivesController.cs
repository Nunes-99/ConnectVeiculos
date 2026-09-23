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
            if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
                return BadRequest("E-mail invalido.");

            var recusa = ExpedienteTestDrive.MotivoRecusa(
                request.DataAgendamento, request.Horario,
                await HorariosOcupadosAsync(request.DataAgendamento, request.LojaId),
                ExpedienteTestDrive.AgoraEmBrasilia());
            if (recusa != null)
                return BadRequest(recusa);

            var testDrive = new TestDrive(0, request.VeiculoId, request.LojaId, request.NomeCliente,
                request.Telefone, request.WhatsApp, request.Email, request.DataAgendamento, request.Horario, request.Observacao, "P");

            _context.TestDrives.Add(testDrive);
            await _context.SaveChangesAsync();

            return Ok(new { id = testDrive.TdrId, mensagem = "Test drive agendado com sucesso!" });
        }

        // GET publico - horarios livres de uma data, para o formulario do catalogo.
        // Antes o catalogo chamava o GET autenticado abaixo: para o visitante comum
        // ele respondia 401 e os filtros de horario ocupado e ja passado nunca rodavam.
        // Aqui so saem os horarios, nada de nome ou telefone de quem agendou.
        [HttpGet("horarios")]
        public async Task<IActionResult> HorariosLivres([FromQuery] DateTime data, [FromQuery] int? lojaId = null)
        {
            var livres = ExpedienteTestDrive.HorariosLivres(
                data, await HorariosOcupadosAsync(data, lojaId), ExpedienteTestDrive.AgoraEmBrasilia());
            return Ok(livres);
        }

        private async Task<List<string?>> HorariosOcupadosAsync(DateTime data, int? lojaId)
        {
            var dia = data.Date;
            var query = _context.TestDrives
                .Where(t => t.TdrStatus != "X" && t.TdrDataAgendamento >= dia && t.TdrDataAgendamento < dia.AddDays(1));
            if (lojaId.HasValue) query = query.Where(t => t.R_LojId == lojaId.Value);
            return await query.Select(t => (string?)t.TdrHorario).ToListAsync();
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

        /// <summary>
        /// Remarca data e horario de um agendamento que continua valendo.
        ///
        /// Um test drive ja realizado ou cancelado nao se remarca: o que
        /// aconteceu, aconteceu. Para esses, o caminho e agendar um novo.
        ///
        /// Quando o agendamento ja estava confirmado, o cliente e avisado da
        /// nova data — foi ele quem combinou a mudanca, e a confirmacao antiga
        /// que ele tem na mao passou a estar errada.
        /// </summary>
        [HttpPut("{id}/reagendar")]
        [Authorize]
        public async Task<IActionResult> Reagendar(
            int id,
            [FromBody] ReagendarTestDriveRequest request,
            [FromServices] ITestDriveNotificacaoService notificacao)
        {
            var testDrive = await _context.TestDrives.FindAsync(id);
            if (testDrive == null) return NotFound();

            if (testDrive.TdrStatus == "R")
                return BadRequest("Test drive ja realizado nao pode ser remarcado. Agende um novo.");
            if (testDrive.TdrStatus == "X")
                return BadRequest("Test drive cancelado nao pode ser remarcado. Agende um novo.");
            if (request.DataAgendamento.Date < DateTime.Today)
                return BadRequest("Data de agendamento nao pode ser no passado.");

            testDrive.Reagendar(request.DataAgendamento, request.Horario);
            await _context.SaveChangesAsync();

            TestDriveNotificacaoResult? notif = null;
            if (testDrive.TdrStatus == "C")
                notif = await notificacao.NotificarConfirmacaoAsync(testDrive);

            return Ok(new
            {
                remarcado = true,
                notificacao = notif == null
                    ? new { aplicavel = false, enviada = false, motivo = "nao-aplicavel", erro = (string?)null }
                    : new { aplicavel = true, enviada = notif.Enviada, motivo = notif.Motivo, erro = notif.MensagemErro }
            });
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

    public class ReagendarTestDriveRequest
    {
        public DateTime DataAgendamento { get; set; }
        /// <summary>Opcional: nem toda loja trabalha com hora marcada.</summary>
        public string? Horario { get; set; }
    }
}
