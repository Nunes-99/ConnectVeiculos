using ConnectVeiculos.Core.Entities.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class ConfiguracaoSistemaRepository : IConfiguracaoSistemaRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public ConfiguracaoSistemaRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetValorAsync(string chave)
        {
            var config = await _context.Configuracoes.FirstOrDefaultAsync(c => c.CfgChave == chave);
            return config?.CfgValor;
        }

        public async Task SetValorAsync(string chave, string valor)
        {
            var config = await _context.Configuracoes.FirstOrDefaultAsync(c => c.CfgChave == chave);
            if (config != null)
            {
                config.AtualizarValor(valor);
                _context.Configuracoes.Update(config);
            }
            else
            {
                _context.Configuracoes.Add(new ConfiguracaoSistema(chave, valor));
            }
            await _context.SaveChangesAsync();
        }
    }
}
