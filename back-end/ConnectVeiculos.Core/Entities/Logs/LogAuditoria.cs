namespace ConnectVeiculos.Core.Entities.Logs
{
    public class LogAuditoria
    {
        public int LogId { get; private set; }
        public string LogTabela { get; private set; }
        public string LogAcao { get; private set; } // INSERT, UPDATE, DELETE
        public int? LogRegistroId { get; private set; }
        public string? LogDadosAntigos { get; private set; }
        public string? LogDadosNovos { get; private set; }
        public int? LogUsuarioId { get; private set; }
        public string? LogUsuarioNome { get; private set; }
        public DateTime LogDataHora { get; private set; }
        public string? LogIP { get; private set; }

        public LogAuditoria() { }

        public LogAuditoria(string tabela, string acao, int? registroId,
            string? dadosAntigos, string? dadosNovos, int? usuarioId,
            string? usuarioNome, string? ip)
        {
            LogTabela = tabela;
            LogAcao = acao;
            LogRegistroId = registroId;
            LogDadosAntigos = dadosAntigos;
            LogDadosNovos = dadosNovos;
            LogUsuarioId = usuarioId;
            LogUsuarioNome = usuarioNome;
            LogDataHora = DateTime.Now;
            LogIP = ip;
        }

        public static LogAuditoria CriarLogInsert(string tabela, int registroId,
            object dados, int? usuarioId, string? usuarioNome, string? ip)
        {
            return new LogAuditoria(
                tabela: tabela,
                acao: "INSERT",
                registroId: registroId,
                dadosAntigos: null,
                dadosNovos: System.Text.Json.JsonSerializer.Serialize(dados),
                usuarioId: usuarioId,
                usuarioNome: usuarioNome,
                ip: ip
            );
        }

        public static LogAuditoria CriarLogUpdate(string tabela, int registroId,
            object? dadosAntigos, object dadosNovos, int? usuarioId, string? usuarioNome, string? ip)
        {
            return new LogAuditoria(
                tabela: tabela,
                acao: "UPDATE",
                registroId: registroId,
                dadosAntigos: dadosAntigos != null ? System.Text.Json.JsonSerializer.Serialize(dadosAntigos) : null,
                dadosNovos: System.Text.Json.JsonSerializer.Serialize(dadosNovos),
                usuarioId: usuarioId,
                usuarioNome: usuarioNome,
                ip: ip
            );
        }

        public static LogAuditoria CriarLogDelete(string tabela, int registroId,
            object dadosAntigos, int? usuarioId, string? usuarioNome, string? ip)
        {
            return new LogAuditoria(
                tabela: tabela,
                acao: "DELETE",
                registroId: registroId,
                dadosAntigos: System.Text.Json.JsonSerializer.Serialize(dadosAntigos),
                dadosNovos: null,
                usuarioId: usuarioId,
                usuarioNome: usuarioNome,
                ip: ip
            );
        }
    }
}
