export interface LogAuditoria {
  logId: number;
  logTabela: string;
  logAcao: string;
  logRegistroId: number | null;
  logDadosAntigos: string | null;
  logDadosNovos: string | null;
  logUsuarioId: number | null;
  logUsuarioNome: string | null;
  logDataHora: string;
  logIP: string | null;
}
