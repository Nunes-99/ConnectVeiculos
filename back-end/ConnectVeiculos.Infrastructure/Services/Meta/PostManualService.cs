using System.Collections.Concurrent;
using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.Meta
{
    /// <inheritdoc cref="IPostManualService"/>
    public class PostManualService : IPostManualService
    {
        public const string Instagram = "instagram";
        public const string Facebook = "facebook";

        // Se o servidor reiniciar no meio, a reserva nao pode travar o carro
        // para sempre: depois disso o botao volta a funcionar.
        private static readonly TimeSpan ValidadeDaReserva = TimeSpan.FromMinutes(5);
        // O painel pergunta pela situacao por alguns minutos; depois disso a
        // lista ja mostra o selo e a entrada so ocupa memoria.
        private static readonly TimeSpan GuardarResultado = TimeSpan.FromMinutes(30);

        private static readonly ConcurrentDictionary<string, PostManualSituacao> Situacoes = new();

        private readonly IInstagramPostService _instagram;
        private readonly IFacebookPagePostService _facebook;
        private readonly IVeiculoRepository _veiculos;
        private readonly IVeiculoImagemRepository _imagens;
        private readonly IVeiculoPublicacaoRepository _publicacoes;
        private readonly ITenantContext? _tenant;
        private readonly ILogger<PostManualService> _logger;

        public PostManualService(
            IInstagramPostService instagram,
            IFacebookPagePostService facebook,
            IVeiculoRepository veiculos,
            IVeiculoImagemRepository imagens,
            IVeiculoPublicacaoRepository publicacoes,
            ILogger<PostManualService> logger,
            ITenantContext? tenant = null)
        {
            _instagram = instagram;
            _facebook = facebook;
            _veiculos = veiculos;
            _imagens = imagens;
            _publicacoes = publicacoes;
            _logger = logger;
            _tenant = tenant;
        }

        public static bool RedeValida(string? rede) => rede == Instagram || rede == Facebook;

        public async Task<PostManualSituacao> PrepararAsync(int veiculoId, string rede)
        {
            if (!RedeValida(rede)) return Falha("Rede inválida: use instagram ou facebook.");
            LimparAntigas();

            var chave = Chave(veiculoId, rede);
            if (Situacoes.TryGetValue(chave, out var atual)
                && atual.Estado == PostManualSituacao.Publicando
                && DateTime.UtcNow - atual.Em < ValidadeDaReserva)
                return Falha($"Este carro já está sendo publicado no {NomeDaRede(rede)}. Aguarde, o selo aparece quando terminar.");

            var configurado = rede == Instagram ? await _instagram.IsConfiguredAsync() : await _facebook.IsConfiguredAsync();
            if (!configurado)
                return Falha($"Conecte o {NomeDaRede(rede)} em Integrações antes de publicar.");

            var veiculo = await _veiculos.GetByIdAsync(veiculoId);
            if (veiculo == null) return Falha("Veículo não encontrado.");
            if (veiculo.VeiSts == "I") return Falha("Veículo inativo não pode ser publicado.");
            if (!(await _imagens.GetByVeiculoIdAsync(veiculoId) ?? Enumerable.Empty<Core.Entities.VeiculosImagens.VeiculoImagem>()).Any(i => i.ImgSts))
                return Falha("O veículo não tem foto.");

            var reserva = new PostManualSituacao
            {
                Estado = PostManualSituacao.Publicando,
                Mensagem = $"Publicando no {NomeDaRede(rede)}. Leva cerca de 1 minuto; pode fechar esta janela."
            };
            // Dois cliques ao mesmo tempo: so um fica com a reserva.
            if (atual == null ? !Situacoes.TryAdd(chave, reserva) : !Situacoes.TryUpdate(chave, reserva, atual))
                return Falha($"Este carro já está sendo publicado no {NomeDaRede(rede)}. Aguarde, o selo aparece quando terminar.");
            return reserva;
        }

        public async Task PublicarAsync(int veiculoId, string rede, string? modeloLegenda)
        {
            var chave = Chave(veiculoId, rede);
            try
            {
                var r = rede == Instagram
                    ? await _instagram.PublicarManualAsync(veiculoId, modeloLegenda)
                    : await _facebook.PublicarManualAsync(veiculoId, modeloLegenda);
                if (r == null)
                {
                    // Conexao com a Meta caida e' o motivo que o lojista resolve
                    // sozinho: confere antes de cair no texto generico.
                    var teste = rede == Instagram ? await _instagram.TestarAsync() : await _facebook.TestarAsync();
                    var motivo = teste.Sucesso || string.IsNullOrWhiteSpace(teste.Mensagem)
                        ? $"Não foi possível publicar no {NomeDaRede(rede)}. Confira as fotos do carro e tente de novo; se continuar, fale com o suporte."
                        : teste.Mensagem;
                    _logger.LogWarning("Post manual {Rede} do veiculo {VeiculoId} nao publicado: {Motivo}", rede, veiculoId, motivo);
                    Situacoes[chave] = new PostManualSituacao { Estado = PostManualSituacao.Falhou, Mensagem = motivo };
                    return;
                }

                var plataforma = rede == Instagram ? "Instagram" : "FacebookPage";
                var existente = await _publicacoes.GetAtivaByVeiculoEPlataformaAsync(veiculoId, plataforma);
                if (existente != null)
                {
                    existente.Remover();
                    await _publicacoes.UpdateAsync(existente);
                }
                var publicacao = new VeiculoPublicacao(veiculoId, plataforma, r.ExternoId, r.Url);
                publicacao.DefinirLegenda(modeloLegenda);
                await _publicacoes.CreateAsync(publicacao);

                Situacoes[chave] = new PostManualSituacao
                {
                    Estado = PostManualSituacao.Publicado,
                    Mensagem = $"Publicado no {NomeDaRede(rede)}!",
                    Url = r.Url
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no post manual {Rede} do veiculo {VeiculoId}", rede, veiculoId);
                Situacoes[chave] = new PostManualSituacao
                {
                    Estado = PostManualSituacao.Falhou,
                    Mensagem = $"Erro ao publicar no {NomeDaRede(rede)}. Tente de novo em alguns minutos."
                };
            }
        }

        public PostManualSituacao? Situacao(int veiculoId, string rede)
            => RedeValida(rede) && Situacoes.TryGetValue(Chave(veiculoId, rede), out var s) ? s : null;

        private string Chave(int veiculoId, string rede) => $"{_tenant?.TenantSlug}:{rede}:{veiculoId}";

        private static string NomeDaRede(string rede) => rede == Instagram ? "Instagram" : "Facebook";

        private static PostManualSituacao Falha(string mensagem)
            => new() { Estado = PostManualSituacao.Falhou, Mensagem = mensagem };

        private static void LimparAntigas()
        {
            var limite = DateTime.UtcNow - GuardarResultado;
            foreach (var (k, v) in Situacoes)
                if (v.Em < limite) Situacoes.TryRemove(k, out _);
        }
    }
}
