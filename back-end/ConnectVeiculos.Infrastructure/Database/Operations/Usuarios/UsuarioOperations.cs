using ConnectVeiculos.Core.Interfaces.Database.Operations.Usuarios;
using ConnectVeiculos.Infrastructure.Database.UnitOfWork;
using Dapper;

namespace ConnectVeiculos.Infrastructure.Database.Operations.Usuarios
{
    public class UsuarioOperations : IUsuarioOperations
    {
        private readonly DbSession _dbSession;

        public UsuarioOperations(DbSession dbSession)
        {
            _dbSession = dbSession;
        }

        public async Task<dynamic> ConsultarVisualizacaoUsuarios(string pesquisa, string inicio, string intervalo)
        {
            var sql = @"
                SELECT
                    UsuId,
                    UsuNome,
                    UsuCPF,
                    UsuRG,
                    UsuEmail,
                    UsuFuncao,
                    UsuSts
                FROM Usuario
                WHERE UsuSts = 1
                AND (@Pesquisa IS NULL OR @Pesquisa = '' OR UsuNome LIKE '%' || @Pesquisa || '%' OR UsuEmail LIKE '%' || @Pesquisa || '%')
                ORDER BY UsuNome
                LIMIT @Intervalo OFFSET @Inicio";

            var parameters = new
            {
                Pesquisa = pesquisa,
                Inicio = int.Parse(inicio),
                Intervalo = int.Parse(intervalo)
            };

            return await _dbSession.Connection.QueryAsync(sql, parameters, _dbSession.Transaction);
        }

        public async Task<dynamic> ConsultarManutencaoUsuario(int id)
        {
            var sql = @"
                SELECT
                    UsuId,
                    UsuNome,
                    UsuCPF,
                    UsuRG,
                    UsuEmail,
                    UsuFuncao,
                    UsuSts
                FROM Usuario
                WHERE UsuId = @Id";

            return await _dbSession.Connection.QueryFirstOrDefaultAsync(sql, new { Id = id }, _dbSession.Transaction);
        }

        public async Task<dynamic> ConsultarUsuarioPorEmail(string email)
        {
            var sql = @"
                SELECT
                    UsuId,
                    UsuNome,
                    UsuCPF,
                    UsuRG,
                    UsuEmail,
                    UsuSenha,
                    UsuFuncao,
                    UsuSts
                FROM Usuario
                WHERE UsuEmail = @Email";

            return await _dbSession.Connection.QueryFirstOrDefaultAsync(sql, new { Email = email }, _dbSession.Transaction);
        }

        public async Task AtualizarSenhaAsync(int usuarioId, string senhaHash)
        {
            var sql = @"UPDATE Usuario SET UsuSenha = @Senha WHERE UsuId = @Id";
            await _dbSession.Connection.ExecuteAsync(sql, new { Id = usuarioId, Senha = senhaHash }, _dbSession.Transaction);
        }
    }
}
