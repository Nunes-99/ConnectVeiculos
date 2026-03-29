using ConnectVeiculos.Core.Interfaces.Database.Operations.RecuperacaoSenha;
using ConnectVeiculos.Infrastructure.Database.UnitOfWork;
using Dapper;

namespace ConnectVeiculos.Infrastructure.Database.Operations.RecuperacaoSenha
{
    public class RecuperacaoSenhaOperations : IRecuperacaoSenhaOperations
    {
        private readonly DbSession _dbSession;

        public RecuperacaoSenhaOperations(DbSession dbSession)
        {
            _dbSession = dbSession;
        }

        public async Task<int> InserirAsync(Core.Entities.RecuperacaoSenha.RecuperacaoSenha recuperacao)
        {
            var sql = @"
                INSERT INTO RecuperacaoSenha (RecUsuId, RecToken, RecDataCriacao, RecDataExpiracao, RecUtilizado)
                VALUES (@RecUsuId, @RecToken, @RecDataCriacao, @RecDataExpiracao, @RecUtilizado);
                SELECT last_insert_rowid()";

            var id = await _dbSession.Connection.QuerySingleAsync<int>(sql, new
            {
                recuperacao.RecUsuId,
                recuperacao.RecToken,
                recuperacao.RecDataCriacao,
                recuperacao.RecDataExpiracao,
                recuperacao.RecUtilizado
            }, _dbSession.Transaction);

            return id;
        }

        public async Task<Core.Entities.RecuperacaoSenha.RecuperacaoSenha?> ObterPorTokenAsync(string token)
        {
            var sql = @"
                SELECT RecId, RecUsuId, RecToken, RecDataCriacao, RecDataExpiracao, RecUtilizado
                FROM RecuperacaoSenha
                WHERE RecToken = @Token";

            return await _dbSession.Connection.QueryFirstOrDefaultAsync<Core.Entities.RecuperacaoSenha.RecuperacaoSenha>(
                sql, new { Token = token }, _dbSession.Transaction);
        }

        public async Task AtualizarAsync(Core.Entities.RecuperacaoSenha.RecuperacaoSenha recuperacao)
        {
            var sql = @"
                UPDATE RecuperacaoSenha
                SET RecUtilizado = @RecUtilizado
                WHERE RecId = @RecId";

            await _dbSession.Connection.ExecuteAsync(sql, new
            {
                recuperacao.RecId,
                recuperacao.RecUtilizado
            }, _dbSession.Transaction);
        }

        public async Task InvalidarTokensAnterioresAsync(int usuarioId)
        {
            var sql = @"
                UPDATE RecuperacaoSenha
                SET RecUtilizado = 1
                WHERE RecUsuId = @UsuarioId AND RecUtilizado = 0";

            await _dbSession.Connection.ExecuteAsync(sql, new { UsuarioId = usuarioId }, _dbSession.Transaction);
        }
    }
}
