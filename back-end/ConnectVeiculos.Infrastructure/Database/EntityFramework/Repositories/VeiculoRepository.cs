using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class VeiculoRepository : IVeiculoRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public VeiculoRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<Veiculo> GetByIdAsync(int id)
        {
            return await _context.Veiculos
                .Include(v => v.Loja)
                .Include(v => v.Categoria)
                .Include(v => v.Caracteristicas)
                .Include(v => v.Observacoes)
                .Include(v => v.Imagens)
                .FirstOrDefaultAsync(v => v.VeiId == id);
        }

        public async Task<Veiculo> GetByPlacaAsync(string placa)
        {
            if (string.IsNullOrWhiteSpace(placa))
                return null;

            return await _context.Veiculos
                .FirstOrDefaultAsync(v => v.VeiPlaca.ToUpper() == placa.ToUpper());
        }

        public async Task<IEnumerable<Veiculo>> GetAllAsync()
        {
            return await _context.Veiculos
                .Include(v => v.Loja)
                .Include(v => v.Categoria)
                .ToListAsync();
        }

        public async Task<IEnumerable<Veiculo>> GetByLojaIdAsync(int lojaId)
        {
            return await _context.Veiculos
                .Include(v => v.Loja)
                .Include(v => v.Categoria)
                .Where(v => v.R_LojId == lojaId)
                .ToListAsync();
        }

        public async Task<(IEnumerable<Veiculo> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null, int? lojaId = null)
        {
            var query = _context.Veiculos
                .Include(v => v.Loja)
                .Include(v => v.Categoria)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(v =>
                    v.VeiMarca.ToLower().Contains(search) ||
                    v.VeiModelo.ToLower().Contains(search) ||
                    v.VeiPlaca.ToLower().Contains(search) ||
                    v.VeiChassi.ToLower().Contains(search));
            }

            if (lojaId.HasValue)
            {
                query = query.Where(v => v.R_LojId == lojaId.Value);
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(v => v.VeiId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<int> CreateAsync(Veiculo veiculo)
        {
            _context.Veiculos.Add(veiculo);
            await _context.SaveChangesAsync();
            return veiculo.VeiId;
        }

        public async Task UpdateAsync(Veiculo veiculo)
        {
            _context.Veiculos.Update(veiculo);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var veiculo = await GetByIdAsync(id);
            if (veiculo != null)
            {
                veiculo.AlterarStatus("I");
                await UpdateAsync(veiculo);
            }
        }

        public async Task<(IEnumerable<Veiculo> Items, int Total)> BuscaAvancadaAsync(BuscaAvancadaParams parametros)
        {
            var query = _context.Veiculos
                .Include(v => v.Loja)
                .Include(v => v.Categoria)
                .Include(v => v.Caracteristicas)
                    .ThenInclude(vc => vc.Caracteristica)
                .Include(v => v.Imagens)
                .AsQueryable();

            // Busca full-text em multiplos campos
            if (!string.IsNullOrWhiteSpace(parametros.Texto))
            {
                var texto = parametros.Texto.ToLower();
                var termos = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var termo in termos)
                {
                    query = query.Where(v =>
                        v.VeiMarca.ToLower().Contains(termo) ||
                        v.VeiModelo.ToLower().Contains(termo) ||
                        v.VeiPlaca.ToLower().Contains(termo) ||
                        v.VeiChassi.ToLower().Contains(termo) ||
                        v.VeiCor.ToLower().Contains(termo));
                }
            }

            // Filtros especificos
            if (!string.IsNullOrWhiteSpace(parametros.Marca))
            {
                query = query.Where(v => v.VeiMarca.ToLower().Contains(parametros.Marca.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(parametros.Modelo))
            {
                query = query.Where(v => v.VeiModelo.ToLower().Contains(parametros.Modelo.ToLower()));
            }

            if (parametros.AnoMinimo.HasValue)
            {
                query = query.Where(v => v.VeiAno >= parametros.AnoMinimo.Value);
            }

            if (parametros.AnoMaximo.HasValue)
            {
                query = query.Where(v => v.VeiAno <= parametros.AnoMaximo.Value);
            }

            if (parametros.PrecoMinimo.HasValue)
            {
                query = query.Where(v => v.VeiPreco >= parametros.PrecoMinimo.Value);
            }

            if (parametros.PrecoMaximo.HasValue)
            {
                query = query.Where(v => v.VeiPreco <= parametros.PrecoMaximo.Value);
            }

            if (parametros.KmMaximo.HasValue)
            {
                query = query.Where(v => v.VeiKm <= parametros.KmMaximo.Value);
            }

            if (!string.IsNullOrWhiteSpace(parametros.Cor))
            {
                query = query.Where(v => v.VeiCor.ToLower().Contains(parametros.Cor.ToLower()));
            }

            if (parametros.LojaId.HasValue)
            {
                query = query.Where(v => v.R_LojId == parametros.LojaId.Value);
            }

            if (parametros.CategoriaId.HasValue)
            {
                query = query.Where(v => v.R_CatId == parametros.CategoriaId.Value);
            }

            if (!string.IsNullOrWhiteSpace(parametros.Status))
            {
                query = query.Where(v => v.VeiSts == parametros.Status);
            }

            if (!string.IsNullOrWhiteSpace(parametros.Situacao))
            {
                query = query.Where(v => v.VeiSitSts.ToLower().Contains(parametros.Situacao.ToLower()));
            }

            // Filtro por caracteristicas
            if (parametros.CaracteristicasIds != null && parametros.CaracteristicasIds.Any())
            {
                query = query.Where(v =>
                    v.Caracteristicas.Any(c => parametros.CaracteristicasIds.Contains(c.R_CarId)));
            }

            // Ordenacao
            query = parametros.OrdenarPor?.ToLower() switch
            {
                "preco" => parametros.Direcao?.ToLower() == "asc"
                    ? query.OrderBy(v => v.VeiPreco)
                    : query.OrderByDescending(v => v.VeiPreco),
                "ano" => parametros.Direcao?.ToLower() == "asc"
                    ? query.OrderBy(v => v.VeiAno)
                    : query.OrderByDescending(v => v.VeiAno),
                "km" => parametros.Direcao?.ToLower() == "asc"
                    ? query.OrderBy(v => v.VeiKm)
                    : query.OrderByDescending(v => v.VeiKm),
                "dataentrada" => parametros.Direcao?.ToLower() == "asc"
                    ? query.OrderBy(v => v.VeiDtEntrada)
                    : query.OrderByDescending(v => v.VeiDtEntrada),
                "marca" => parametros.Direcao?.ToLower() == "asc"
                    ? query.OrderBy(v => v.VeiMarca)
                    : query.OrderByDescending(v => v.VeiMarca),
                _ => query.OrderByDescending(v => v.VeiId)
            };

            var total = await query.CountAsync();
            var items = await query
                .Skip((parametros.Pagina - 1) * parametros.TamanhoPagina)
                .Take(parametros.TamanhoPagina)
                .ToListAsync();

            return (items, total);
        }
    }
}
