using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Services.PrimeirosPassos
{
    public class PassoInfo
    {
        public string Id { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Descricao { get; set; } = "";
        /// <summary>Rota da tela onde o passo se resolve.</summary>
        public string Link { get; set; } = "";
        public string? LinkQuery { get; set; }
        public string Icone { get; set; } = "";
        public bool Feito { get; set; }
        /// <summary>So o Administrador consegue fazer (ex.: usuarios).</summary>
        public bool SoAdministrador { get; set; }
    }

    public class PrimeirosPassosInfo
    {
        /// <summary>A loja escondeu o cartao no Painel.</summary>
        public bool Oculto { get; set; }
        public int Feitos { get; set; }
        public int Total { get; set; }
        public bool Concluido => Total > 0 && Feitos == Total;
        public List<PassoInfo> Passos { get; set; } = new();
    }

    /// <summary>
    /// "Primeiros passos" da loja nova no Painel: cada passo se marca sozinho
    /// pelo que ja esta cadastrado (nada a marcar a mao). Objetivo: a loja
    /// sair do trial com o sistema em uso (estoque, site, redes, portais).
    /// </summary>
    public class PrimeirosPassosService
    {
        public const string ChaveOculto = "PRIMEIROS_PASSOS_OCULTO";
        private const string CorPadrao = "#1a237e";

        private readonly ConnectVeiculosDbContext _db;
        private readonly IConfiguracaoSistemaRepository _config;

        public PrimeirosPassosService(ConnectVeiculosDbContext db, IConfiguracaoSistemaRepository config)
        {
            _db = db;
            _config = config;
        }

        public async Task<PrimeirosPassosInfo> ConsultarAsync()
        {
            var lojas = await _db.Lojas.AsNoTracking().Where(l => l.LojSts).ToListAsync();
            var lojaCompleta = lojas.Any(l =>
                Preenchido(l.LojWhatsApp ?? l.LojTel1) && Preenchido(l.LojCEP) && Preenchido(l.LojCidade) && Preenchido(l.LojImg));
            var sitePersonalizado = lojas.Any(l =>
                Preenchido(l.LojBannerImg) || Preenchido(l.LojSobre)
                || (Preenchido(l.LojCorPrimaria) && !string.Equals(l.LojCorPrimaria, CorPadrao, StringComparison.OrdinalIgnoreCase)));

            var veiculoComFoto = await _db.VeiculosImagens.AsNoTracking().AnyAsync(i => i.ImgSts);
            var equipe = await _db.Usuarios.AsNoTracking().CountAsync(u => u.UsuSts) > 1;
            var venda = await _db.Vendas.AsNoTracking().AnyAsync();

            var redes = await TemValorAsync("META_PAGE_TOKEN_CIFRADO");
            var portal = await _db.IntegracoesMercadoLivre.AsNoTracking().AnyAsync(i => i.IntAccessTokenCifrado != null && i.IntAccessTokenCifrado != "")
                         || await TemValorAsync("ML_ACCESS_TOKEN")
                         || await TemValorAsync("WEBMOTORS_CONTA")
                         || await TemValorAsync("NAPISTA_TOKEN")
                         || await TemValorAsync("OLX_TOKEN");

            var passos = new List<PassoInfo>
            {
                new() { Id = "loja", Icone = "store", Link = "/lojas", Feito = lojaCompleta,
                    Titulo = "Complete os dados da loja",
                    Descricao = "Logo, WhatsApp e endereço com CEP. Aparecem no site, nos anúncios e nos contratos." },
                new() { Id = "veiculo", Icone = "directions_car", Link = "/veiculos", Feito = veiculoComFoto,
                    Titulo = "Cadastre o primeiro carro com fotos",
                    Descricao = "Escolha marca e modelo pela FIPE e suba as fotos: ele já vai para o site da loja." },
                new() { Id = "site", Icone = "palette", Link = "/lojas", Feito = sitePersonalizado,
                    Titulo = "Personalize o site da loja",
                    Descricao = "Cores, banner e o texto \"sobre a loja\" no catálogo público." },
                new() { Id = "redes", Icone = "share", Link = "/integracoes", Feito = redes,
                    Titulo = "Conecte o Facebook e o Instagram",
                    Descricao = "Cada carro novo vira post e story automaticamente." },
                new() { Id = "portais", Icone = "campaign", Link = "/integracoes", Feito = portal,
                    Titulo = "Anuncie em um portal",
                    Descricao = "Mercado Livre, Webmotors, NaPista ou OLX: o estoque é atualizado sozinho." },
                new() { Id = "equipe", Icone = "group_add", Link = "/usuarios", Feito = equipe, SoAdministrador = true,
                    Titulo = "Cadastre sua equipe",
                    Descricao = "Vendedores entram com o próprio usuário e só veem o que precisam." },
                new() { Id = "venda", Icone = "point_of_sale", Link = "/vendas", Feito = venda,
                    Titulo = "Registre a primeira venda",
                    Descricao = "O carro sai do site e dos portais sozinho, e a comissão já fica calculada." }
            };

            return new PrimeirosPassosInfo
            {
                Oculto = await _config.GetValorAsync(ChaveOculto) == "1",
                Passos = passos,
                Feitos = passos.Count(p => p.Feito),
                Total = passos.Count
            };
        }

        public async Task<PrimeirosPassosInfo> OcultarAsync(bool oculto)
        {
            await _config.SetValorAsync(ChaveOculto, oculto ? "1" : "0");
            return await ConsultarAsync();
        }

        private async Task<bool> TemValorAsync(string chave) => Preenchido(await _config.GetValorAsync(chave));

        private static bool Preenchido(string? v) => !string.IsNullOrWhiteSpace(v);
    }
}
