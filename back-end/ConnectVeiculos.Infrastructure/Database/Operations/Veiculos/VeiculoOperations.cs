using ConnectVeiculos.Core.Interfaces.Database.Operations.Veiculos;
using ConnectVeiculos.Infrastructure.Database.UnitOfWork;
using Dapper;

namespace ConnectVeiculos.Infrastructure.Database.Operations.Veiculos
{
    public class VeiculoOperations : IVeiculoOperations
    {
        private readonly DbSession _dbSession;

        public VeiculoOperations(DbSession dbSession)
        {
            _dbSession = dbSession;
        }

        public async Task<dynamic> ConsultarVisualizacaoVeiculos(string pesquisa, int? lojaId, string inicio, string intervalo)
        {
            var sql = @"
                SELECT
                    v.VeiId,
                    v.R_LojId,
                    l.LojNome AS LojaNome,
                    v.R_CatId,
                    c.CatNome AS CategoriaNome,
                    v.VeiMarca,
                    v.VeiModelo,
                    v.VeiAno,
                    v.VeiPlaca,
                    v.VeiChassi,
                    v.VeiCor,
                    v.VeiKm,
                    v.VeiPreco,
                    v.VeiDtEntrada,
                    v.VeiSts,
                    v.VeiSitSts,
                    v.VeiPrecoCompra,
                    v.VeiObservacao,
                    v.VeiOpcionais,
                    v.VeiDonoAtual,
                    v.VeiDonoCelular,
                    v.VeiPrecoFipe,
                    v.VeiPostadoInsta,
                    v.VeiPostadoFace,
                    v.VeiDtPostagemInsta,
                    v.VeiDtPostagemFace
                FROM Veiculo v
                INNER JOIN Loja l ON v.R_LojId = l.LojId
                INNER JOIN Categoria c ON v.R_CatId = c.CatId
                WHERE (v.VeiSts = 'D' OR v.VeiSts = 'R' OR v.VeiSts = 'V')
                AND (@LojaId IS NULL OR v.R_LojId = @LojaId)
                AND (@Pesquisa IS NULL OR @Pesquisa = ''
                    OR v.VeiMarca LIKE '%' || @Pesquisa || '%'
                    OR v.VeiModelo LIKE '%' || @Pesquisa || '%'
                    OR v.VeiPlaca LIKE '%' || @Pesquisa || '%')
                ORDER BY v.VeiDtEntrada DESC
                LIMIT @Intervalo OFFSET @Inicio";

            var parameters = new
            {
                Pesquisa = pesquisa,
                LojaId = lojaId,
                Inicio = int.Parse(inicio),
                Intervalo = int.Parse(intervalo)
            };

            return await _dbSession.Connection.QueryAsync(sql, parameters, _dbSession.Transaction);
        }

        public async Task<dynamic> ConsultarManutencaoVeiculo(int id)
        {
            var sql = @"
                SELECT
                    v.VeiId,
                    v.R_LojId,
                    l.LojNome AS LojaNome,
                    v.R_CatId,
                    c.CatNome AS CategoriaNome,
                    v.VeiMarca,
                    v.VeiModelo,
                    v.VeiAno,
                    v.VeiPlaca,
                    v.VeiChassi,
                    v.VeiCor,
                    v.VeiKm,
                    v.VeiPreco,
                    v.VeiDtEntrada,
                    v.VeiSts,
                    v.VeiSitSts,
                    v.VeiPrecoCompra,
                    v.VeiObservacao,
                    v.VeiOpcionais,
                    v.VeiDonoAtual,
                    v.VeiDonoCelular,
                    v.VeiPrecoFipe,
                    v.VeiPostadoInsta,
                    v.VeiPostadoFace,
                    v.VeiDtPostagemInsta,
                    v.VeiDtPostagemFace
                FROM Veiculo v
                INNER JOIN Loja l ON v.R_LojId = l.LojId
                INNER JOIN Categoria c ON v.R_CatId = c.CatId
                WHERE v.VeiId = @Id";

            return await _dbSession.Connection.QueryFirstOrDefaultAsync(sql, new { Id = id }, _dbSession.Transaction);
        }

        public async Task<dynamic> ConsultarVeiculoCompleto(int id)
        {
            var sql = @"
                SELECT
                    v.VeiId,
                    v.R_LojId,
                    l.LojNome AS LojaNome,
                    v.R_CatId,
                    c.CatNome AS CategoriaNome,
                    v.VeiMarca,
                    v.VeiModelo,
                    v.VeiAno,
                    v.VeiPlaca,
                    v.VeiChassi,
                    v.VeiCor,
                    v.VeiKm,
                    v.VeiPreco,
                    v.VeiDtEntrada,
                    v.VeiSts,
                    v.VeiSitSts,
                    v.VeiPrecoCompra,
                    v.VeiObservacao,
                    v.VeiOpcionais,
                    v.VeiDonoAtual,
                    v.VeiDonoCelular,
                    v.VeiPrecoFipe,
                    v.VeiPostadoInsta,
                    v.VeiPostadoFace,
                    v.VeiDtPostagemInsta,
                    v.VeiDtPostagemFace
                FROM Veiculo v
                INNER JOIN Loja l ON v.R_LojId = l.LojId
                INNER JOIN Categoria c ON v.R_CatId = c.CatId
                WHERE v.VeiId = @Id;

                SELECT
                    car.CarId,
                    car.CarNome
                FROM VeiculoCaracteristica vc
                INNER JOIN Caracteristica car ON vc.R_CarId = car.CarId
                WHERE vc.R_VeiId = @Id;

                SELECT
                    obs.ObsId,
                    obs.ObsNome
                FROM VeiculoObservacao vo
                INNER JOIN Observacao obs ON vo.R_ObsId = obs.ObsId
                WHERE vo.R_VeiId = @Id;

                SELECT
                    ImgId,
                    ImgCaminho,
                    ImgOrdem
                FROM VeiculoImagem
                WHERE R_VeiId = @Id AND ImgSts = 1
                ORDER BY ImgOrdem;";

            using var multi = await _dbSession.Connection.QueryMultipleAsync(sql, new { Id = id }, _dbSession.Transaction);

            var veiculo = await multi.ReadFirstOrDefaultAsync();
            var caracteristicas = await multi.ReadAsync();
            var observacoes = await multi.ReadAsync();
            var imagens = await multi.ReadAsync();

            return new
            {
                Veiculo = veiculo,
                Caracteristicas = caracteristicas,
                Observacoes = observacoes,
                Imagens = imagens
            };
        }
    }
}
