namespace ConnectVeiculos.Core.Entities.TestDrives
{
    /// <summary>
    /// Quando o visitante do catalogo pode agendar um test drive.
    ///
    /// Antes os horarios eram uma lista fixa no Angular, de 08:00 a 17:00 todos os
    /// dias, e o cliente conseguia marcar domingo (loja fechada) ou sabado as 17:00
    /// (a loja fecha ao meio-dia). A regra mora aqui para valer tambem no POST
    /// publico — senao bastava chamar a API direto para furar o expediente.
    ///
    /// Vale so para o agendamento publico. A loja, pelo painel, pode remarcar
    /// para fora do expediente quando combinar isso com o cliente.
    /// </summary>
    public static class ExpedienteTestDrive
    {
        private static readonly string[] Horarios =
            { "08:00", "09:00", "10:00", "11:00", "13:00", "14:00", "15:00", "16:00", "17:00" };

        // Sabado a loja fecha ao meio-dia: o ultimo test drive comeca as 11:00.
        private const int SabadoFechaAs = 12;

        // O Brasil nao tem horario de verao desde 2019, entao -3 fixo e' exato e nao
        // depende do tzdata do container (que roda em UTC).
        private static readonly TimeSpan FusoBrasilia = TimeSpan.FromHours(-3);

        public static DateTime AgoraEmBrasilia() => DateTime.UtcNow + FusoBrasilia;

        /// <summary>Horarios que a loja atende naquele dia da semana. Domingo: nenhum.</summary>
        public static IReadOnlyList<string> HorariosDoDia(DayOfWeek dia) => dia switch
        {
            DayOfWeek.Sunday => Array.Empty<string>(),
            DayOfWeek.Saturday => Horarios.Where(h => Hora(h) < SabadoFechaAs).ToArray(),
            _ => Horarios
        };

        /// <summary>
        /// Horarios ainda livres numa data: tira os fora do expediente, os ja
        /// ocupados e, se a data for hoje, os que ja passaram.
        /// </summary>
        public static IReadOnlyList<string> HorariosLivres(DateTime data, IEnumerable<string?> ocupados, DateTime agora)
        {
            var dia = data.Date;
            if (dia < agora.Date) return Array.Empty<string>();

            var tomados = new HashSet<string>(ocupados.Where(o => !string.IsNullOrWhiteSpace(o))!);

            return HorariosDoDia(dia.DayOfWeek)
                .Where(h => !tomados.Contains(h))
                .Where(h => dia > agora.Date || dia.AddHours(Hora(h)).AddMinutes(Minuto(h)) > agora)
                .ToArray();
        }

        /// <summary>Motivo para recusar o agendamento, ou null se ele pode ser aceito.</summary>
        public static string? MotivoRecusa(DateTime data, string? horario, IEnumerable<string?> ocupados, DateTime agora)
        {
            var dia = data.Date;
            if (dia < agora.Date) return "Data de agendamento não pode ser no passado.";
            if (dia.DayOfWeek == DayOfWeek.Sunday) return "A loja não abre aos domingos.";
            if (string.IsNullOrWhiteSpace(horario)) return "Escolha um horário.";

            if (!HorariosDoDia(dia.DayOfWeek).Contains(horario))
                return dia.DayOfWeek == DayOfWeek.Saturday
                    ? "Aos sábados a loja atende até o meio-dia."
                    : "Horário fora do expediente da loja.";

            if (!HorariosLivres(dia, ocupados, agora).Contains(horario))
                return "Esse horário não está mais disponível. Escolha outro.";

            return null;
        }

        private static int Hora(string h) => int.Parse(h[..2]);
        private static int Minuto(string h) => int.Parse(h[3..5]);
    }
}
