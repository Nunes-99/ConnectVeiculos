using ConnectVeiculos.Application.Interfaces.Acessos;
using ConnectVeiculos.Application.ViewModels.Acessos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos;

namespace ConnectVeiculos.Application.UseCases.Acessos
{
    public class ConsultarAcessoPorIdUseCase : IConsultarAcessoPorIdUseCase
    {
        private readonly IAcessoRepository _acessoRepository;

        public ConsultarAcessoPorIdUseCase(IAcessoRepository acessoRepository)
        {
            _acessoRepository = acessoRepository;
        }

        public async Task<AcessoViewModel> Execute(int id)
        {
            var acesso = await _acessoRepository.GetByIdAsync(id);

            if (acesso == null)
                return null;

            return new AcessoViewModel
            {
                AcsId = acesso.AcsId,
                AcsNome = acesso.AcsNome,
                AcsDesc = acesso.AcsDesc,
                AcsSts = acesso.AcsSts
            };
        }
    }
}
