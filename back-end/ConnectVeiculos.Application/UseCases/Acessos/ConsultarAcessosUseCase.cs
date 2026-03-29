using ConnectVeiculos.Application.Interfaces.Acessos;
using ConnectVeiculos.Application.ViewModels.Acessos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos;

namespace ConnectVeiculos.Application.UseCases.Acessos
{
    public class ConsultarAcessosUseCase : IConsultarAcessosUseCase
    {
        private readonly IAcessoRepository _acessoRepository;

        public ConsultarAcessosUseCase(IAcessoRepository acessoRepository)
        {
            _acessoRepository = acessoRepository;
        }

        public async Task<List<AcessoViewModel>> Execute(string pesquisa, string inicio, string intervalo)
        {
            var acessos = await _acessoRepository.GetAllAsync();

            if (!string.IsNullOrEmpty(pesquisa))
            {
                acessos = acessos.Where(a =>
                    a.AcsNome.Contains(pesquisa, StringComparison.OrdinalIgnoreCase) ||
                    (a.AcsDesc != null && a.AcsDesc.Contains(pesquisa, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            return acessos.Select(a => new AcessoViewModel
            {
                AcsId = a.AcsId,
                AcsNome = a.AcsNome,
                AcsDesc = a.AcsDesc,
                AcsSts = a.AcsSts
            }).ToList();
        }
    }
}
