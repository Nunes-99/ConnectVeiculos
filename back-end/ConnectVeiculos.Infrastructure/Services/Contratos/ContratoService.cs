using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ConnectVeiculos.Core.Contratos;
using ConnectVeiculos.Core.Entities.Contratos;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.Contratos
{
    /// <summary>
    /// Contratos dos veiculos com assinatura eletronica propria. O cliente
    /// assina pelo link (confirmando com um codigo enviado por e-mail) ou na
    /// loja, no aparelho do vendedor; a loja assina logada no sistema.
    /// </summary>
    public class ContratoService : IContratoService
    {
        private static readonly CultureInfo PtBr = new("pt-BR");
        private static readonly TimeSpan ValidadeCodigo = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan IntervaloCodigo = TimeSpan.FromSeconds(60);
        private const int TamanhoMaximoImagem = 400_000;

        private readonly ConnectVeiculosDbContext _db;
        private readonly ITokenProtector _protector;
        private readonly IEmailService _email;
        private readonly ITenantContext _tenant;
        private readonly ILogger<ContratoService> _logger;

        public ContratoService(ConnectVeiculosDbContext db, ITokenProtector protector, IEmailService email,
            ITenantContext tenant, ILogger<ContratoService> logger)
        {
            _db = db;
            _protector = protector;
            _email = email;
            _tenant = tenant;
            _logger = logger;
        }

        // ================================================================
        // Modelos
        // ================================================================

        public async Task<IReadOnlyList<ModeloContratoInfo>> ListarModelosAsync(bool incluirInativos, CancellationToken ct = default)
        {
            await GarantirModelosPadraoAsync(ct);
            var q = _db.ModelosContrato.AsNoTracking();
            if (!incluirInativos) q = q.Where(m => m.MdcAtivo);
            return (await q.OrderBy(m => m.MdcNome).ToListAsync(ct)).Select(Info).ToList();
        }

        /// <summary>Na primeira vez, a loja ganha os modelos prontos.</summary>
        private async Task GarantirModelosPadraoAsync(CancellationToken ct)
        {
            if (await _db.ModelosContrato.AnyAsync(m => m.MdcPadrao, ct)) return;
            foreach (var m in ModelosPadrao.Todos)
                _db.ModelosContrato.Add(new ModeloContrato(m.Nome, m.Descricao, m.Tipo, m.Conteudo, padrao: true));
            await _db.SaveChangesAsync(ct);
        }

        public async Task<ModeloContratoInfo> SalvarModeloAsync(int? id, ModeloContratoInput input, CancellationToken ct = default)
        {
            var nome = (input.Nome ?? "").Trim();
            if (nome.Length is < 3 or > 150) throw new ContratoException("O nome do modelo precisa ter entre 3 e 150 letras.");
            var tipo = (input.Tipo ?? "").Trim().ToUpperInvariant();
            if (!TiposContrato.Todos.Contains(tipo)) throw new ContratoException("Tipo de contrato inválido.");
            var conteudo = (input.Conteudo ?? "").Trim();
            if (conteudo.Length < 20) throw new ContratoException("Escreva o texto do contrato.");
            if (conteudo.Length > 50_000) throw new ContratoException("O texto do contrato passou de 50 mil caracteres.");

            ModeloContrato modelo;
            if (id.HasValue)
            {
                modelo = await _db.ModelosContrato.FirstOrDefaultAsync(m => m.MdcId == id.Value, ct)
                    ?? throw new ContratoException("Modelo não encontrado.");
                modelo.Atualizar(nome, input.Descricao, tipo, conteudo, input.Ativo);
            }
            else
            {
                modelo = new ModeloContrato(nome, input.Descricao, tipo, conteudo);
                _db.ModelosContrato.Add(modelo);
            }
            await _db.SaveChangesAsync(ct);
            return Info(modelo);
        }

        public async Task ExcluirModeloAsync(int id, CancellationToken ct = default)
        {
            var modelo = await _db.ModelosContrato.FirstOrDefaultAsync(m => m.MdcId == id, ct)
                ?? throw new ContratoException("Modelo não encontrado.");
            // Modelo pronto ou ja usado so sai da lista: os contratos continuam apontando para ele.
            if (modelo.MdcPadrao || await _db.Contratos.AnyAsync(c => c.R_MdcId == id, ct)) modelo.Desativar();
            else _db.ModelosContrato.Remove(modelo);
            await _db.SaveChangesAsync(ct);
        }

        public IReadOnlyList<ContratoVariavel> Variaveis() => ContratoTexto.Variaveis;

        public async Task<ContratoPreview> PreviewModeloAsync(ModeloPreviewInput input, CancellationToken ct = default)
        {
            var tipo = TiposContrato.Todos.Contains((input.Tipo ?? "").ToUpperInvariant()) ? input.Tipo!.ToUpperInvariant() : TiposContrato.Outro;
            Dictionary<string, string?> valores;
            if (input.VeiculoId.HasValue)
            {
                var (veiculo, loja) = await VeiculoELojaAsync(input.VeiculoId.Value, ct);
                var s = await SugestaoAsync(veiculo, tipo, ct);
                valores = Valores(veiculo, loja, tipo, s.Parte, s.Valor, s.FormaPagamento, null);
            }
            else valores = Exemplo(tipo);
            return Preview(input.Conteudo, "Modelo", valores);
        }

        // ================================================================
        // Contratos
        // ================================================================

        public async Task<ContratoSugestao> SugerirAsync(int veiculoId, int modeloId, CancellationToken ct = default)
        {
            var modelo = await ModeloAsync(modeloId, ct);
            var (veiculo, _) = await VeiculoELojaAsync(veiculoId, ct);
            var s = await SugestaoAsync(veiculo, modelo.MdcTipo, ct);
            s.Extras = ContratoTexto.Extras(modelo.MdcConteudo);
            return s;
        }

        private async Task<ContratoSugestao> SugestaoAsync(Veiculo veiculo, string tipo, CancellationToken ct)
        {
            var s = new ContratoSugestao { Tipo = tipo, PapelCliente = TiposContrato.PapelCliente(tipo) };
            if (TiposContrato.ClienteEhDono(tipo))
            {
                s.Valor = tipo == TiposContrato.Compra && veiculo.VeiPrecoCompra > 0 ? veiculo.VeiPrecoCompra : veiculo.VeiPreco;
                if (!string.IsNullOrWhiteSpace(veiculo.VeiDonoAtual))
                {
                    s.Parte = new ContratoParte { Nome = veiculo.VeiDonoAtual, Telefone = veiculo.VeiDonoCelular };
                    s.Origem = "veiculo";
                }
                return s;
            }

            var venda = await _db.Vendas.AsNoTracking()
                .Where(v => v.R_VeiId == veiculo.VeiId && v.VenStatus == "A")
                .OrderByDescending(v => v.VenId)
                .FirstOrDefaultAsync(ct);
            s.Valor = venda?.VenValor ?? veiculo.VeiPreco;
            if (venda != null)
            {
                s.FormaPagamento = venda.VenFormaPagamento;
                s.Parte = new ContratoParte
                {
                    Nome = venda.VenCompradorNome ?? "",
                    Documento = venda.VenCompradorCpf,
                    Email = venda.VenCompradorEmail,
                    Telefone = venda.VenCompradorTelefone,
                    Endereco = venda.VenCompradorEndereco
                };
                s.Origem = "venda";
            }
            return s;
        }

        public async Task<ContratoPreview> PreviewAsync(ContratoGerarInput input, CancellationToken ct = default)
        {
            var modelo = await ModeloAsync(input.ModeloId, ct);
            var (veiculo, loja) = await VeiculoELojaAsync(input.VeiculoId, ct);
            var valores = Valores(veiculo, loja, modelo.MdcTipo, input.Parte, input.Valor, input.FormaPagamento, input.Extras);
            return Preview(modelo.MdcConteudo, modelo.MdcNome, valores);
        }

        public async Task<ContratoInfo> GerarAsync(ContratoGerarInput input, int? usuarioId, CancellationToken ct = default)
        {
            var modelo = await ModeloAsync(input.ModeloId, ct);
            if (!modelo.MdcAtivo) throw new ContratoException("Este modelo está desativado.");
            var (veiculo, loja) = await VeiculoELojaAsync(input.VeiculoId, ct);

            var parte = input.Parte ?? new ContratoParte();
            parte.Nome = (parte.Nome ?? "").Trim();
            if (parte.Nome.Length < 3) throw new ContratoException($"Informe o nome do {TiposContrato.PapelCliente(modelo.MdcTipo).ToLowerInvariant()}.");
            parte.Documento = Digitos(parte.Documento);
            if (!string.IsNullOrEmpty(parte.Documento) && !DocumentoValido(parte.Documento))
                throw new ContratoException("CPF/CNPJ do cliente inválido.");
            parte.Email = string.IsNullOrWhiteSpace(parte.Email) ? null : parte.Email.Trim();
            if (parte.Email != null && !EmailValido(parte.Email)) throw new ContratoException("E-mail do cliente inválido.");

            var valores = Valores(veiculo, loja, modelo.MdcTipo, parte, input.Valor, input.FormaPagamento, input.Extras);
            var faltando = new List<string>();
            var texto = ContratoTexto.Preencher(modelo.MdcConteudo, valores, faltando);
            var titulo = ContratoTexto.Titulo(ContratoTexto.Blocos(texto), modelo.MdcNome);

            var contrato = new Contrato(veiculo.VeiId, modelo.MdcId, usuarioId, modelo.MdcTipo, titulo, texto, ContratoTexto.Hash(texto));
            var cliente = new ContratoSignatario(ContratoSignatario.PapelCliente, parte.Nome, parte.Documento, parte.Email, Digitos(parte.Telefone));
            var token = NovoToken();
            cliente.DefinirToken(HashToken(token), _protector.Protect(token));
            contrato.Signatarios.Add(cliente);
            contrato.Signatarios.Add(new ContratoSignatario(ContratoSignatario.PapelLoja, loja?.LojNome ?? "Loja", Digitos(loja?.LojCNPJ), loja?.LojEmail, null));

            _db.Contratos.Add(contrato);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Contrato {Id} ({Tipo}) gerado para o veiculo {Veiculo}", contrato.CtrId, contrato.CtrTipo, veiculo.VeiId);
            return Info(contrato, veiculo, comToken: true);
        }

        public async Task<IReadOnlyList<ContratoInfo>> ListarAsync(ContratoFiltro filtro, CancellationToken ct = default)
        {
            var q = _db.Contratos.AsNoTracking().Include(c => c.Signatarios).AsQueryable();
            if (filtro.VeiculoId.HasValue) q = q.Where(c => c.R_VeiId == filtro.VeiculoId.Value);
            if (!string.IsNullOrWhiteSpace(filtro.Status)) q = q.Where(c => c.CtrStatus == filtro.Status.Trim().ToUpper());

            var contratos = await q.OrderByDescending(c => c.CtrId).Take(300).ToListAsync(ct);
            var ids = contratos.Select(c => c.R_VeiId).Distinct().ToList();
            var veiculos = await _db.Veiculos.AsNoTracking().Where(v => ids.Contains(v.VeiId)).ToDictionaryAsync(v => v.VeiId, ct);

            var lista = contratos.Select(c => Info(c, veiculos.GetValueOrDefault(c.R_VeiId), comToken: true));
            var busca = filtro.Busca?.Trim();
            if (!string.IsNullOrEmpty(busca))
            {
                var placa = busca.Replace("-", "");
                lista = lista.Where(c => c.Titulo.Contains(busca, StringComparison.OrdinalIgnoreCase)
                    || (c.Veiculo ?? "").Contains(busca, StringComparison.OrdinalIgnoreCase)
                    || (c.Placa ?? "").Replace("-", "").Contains(placa, StringComparison.OrdinalIgnoreCase)
                    || c.Signatarios.Any(s => s.Nome.Contains(busca, StringComparison.OrdinalIgnoreCase)
                        || (s.Documento ?? "").Contains(Digitos(busca) ?? "\u0000")));
            }
            return lista.ToList();
        }

        public async Task<ContratoInfo> ObterAsync(int id, CancellationToken ct = default)
        {
            var c = await ContratoAsync(id, ct);
            var v = await _db.Veiculos.AsNoTracking().FirstOrDefaultAsync(x => x.VeiId == c.R_VeiId, ct);
            return Info(c, v, comToken: true);
        }

        public async Task<ContratoPreview> TextoAsync(int id, CancellationToken ct = default)
        {
            var c = await ContratoAsync(id, ct);
            return new ContratoPreview { Titulo = c.CtrTitulo, Blocos = ContratoTexto.Blocos(c.CtrConteudo) };
        }

        public async Task<ContratoInfo> CancelarAsync(int id, CancellationToken ct = default)
        {
            var c = await ContratoAsync(id, ct);
            if (c.CtrStatus == Contrato.StatusCancelado) throw new ContratoException("O contrato já está cancelado.");
            // Assinado por todos vale como esta; desfazer o negocio e' outro documento (distrato).
            if (c.CtrStatus == Contrato.StatusAssinado) throw new ContratoException("O contrato já foi assinado por todos e não pode ser cancelado.");
            c.Cancelar();
            await _db.SaveChangesAsync(ct);
            return await ObterAsync(id, ct);
        }

        public async Task<ContratoInfo> AssinarPelaLojaAsync(int id, AssinaturaInput input, int usuarioId, string usuarioNome,
            AssinaturaOrigem origem, CancellationToken ct = default)
        {
            var c = await ContratoAbertoAsync(id, ct);
            var loja = c.Signatarios.First(s => s.CsgPapel == ContratoSignatario.PapelLoja);
            if (loja.Assinou) throw new ContratoException("A loja já assinou este contrato.");
            var imagem = Imagem(input.Imagem);
            var nome = string.IsNullOrWhiteSpace(input.Nome) ? usuarioNome : input.Nome.Trim();
            loja.Assinar(ContratoSignatario.MetodoUsuario, $"{nome} (por {loja.CsgNome})", loja.CsgDocumento, imagem,
                origem.Ip, origem.Navegador, usuarioId, usuarioNome);
            c.Concluir();
            await _db.SaveChangesAsync(ct);
            return await ObterAsync(id, ct);
        }

        public async Task<ContratoInfo> AssinarPresencialAsync(int id, AssinaturaInput input, int usuarioId, string usuarioNome,
            AssinaturaOrigem origem, CancellationToken ct = default)
        {
            var c = await ContratoAbertoAsync(id, ct);
            var cliente = c.Signatarios.First(s => s.CsgPapel == ContratoSignatario.PapelCliente);
            if (cliente.Assinou) throw new ContratoException("O cliente já assinou este contrato.");
            var (nome, documento) = ConferirIdentidade(cliente, input);
            cliente.Assinar(ContratoSignatario.MetodoPresencial, nome, documento, Imagem(input.Imagem),
                origem.Ip, origem.Navegador, usuarioId, usuarioNome);
            c.Concluir();
            await _db.SaveChangesAsync(ct);
            return await ObterAsync(id, ct);
        }

        public async Task EnviarLinkPorEmailAsync(int id, string urlBase, CancellationToken ct = default)
        {
            var c = await ContratoAbertoAsync(id, ct);
            var cliente = c.Signatarios.First(s => s.CsgPapel == ContratoSignatario.PapelCliente);
            if (cliente.Assinou) throw new ContratoException("O cliente já assinou.");
            if (string.IsNullOrEmpty(cliente.CsgEmail)) throw new ContratoException("O cliente não tem e-mail no contrato.");
            var loja = await LojaDoVeiculoAsync(c.R_VeiId, ct);
            var link = Link(urlBase, _protector.Unprotect(cliente.CsgTokenProtegido!));

            var corpo = $@"<p>Olá, {Html(cliente.CsgNome)}!</p>
<p>A {Html(loja?.LojNome ?? "loja")} enviou o documento <strong>{Html(c.CtrTitulo)}</strong> para você conferir e assinar.</p>
<p><a href=""{link}"" style=""display:inline-block;padding:12px 20px;background:#1a237e;color:#fff;border-radius:8px;text-decoration:none"">Ler e assinar</a></p>
<p style=""color:#666;font-size:13px"">Se o botão não abrir, copie o endereço: {link}</p>";
            if (!await _email.SendEmailAsync(cliente.CsgEmail, $"Documento para assinar: {c.CtrTitulo}", corpo))
                throw new ContratoException("Não foi possível enviar o e-mail. Confira o e-mail da loja em Integrações ou mande o link pelo WhatsApp.");
        }

        public async Task<(byte[] Pdf, string NomeArquivo)> PdfAsync(int id, CancellationToken ct = default)
        {
            var c = await ContratoAsync(id, ct);
            var loja = await LojaDoVeiculoAsync(c.R_VeiId, ct);
            return (ContratoPdf.Gerar(c, loja?.LojNome ?? "loja"), $"contrato-{c.CtrId}.pdf");
        }

        // ================================================================
        // Link publico
        // ================================================================

        public async Task<AssinaturaPublicaInfo> AbrirLinkAsync(string token, CancellationToken ct = default)
        {
            var (c, s) = await PeloTokenAsync(token, ct);
            if (c.CtrStatus == Contrato.StatusCancelado) throw new ContratoException("Este documento foi cancelado pela loja.");
            s.Visualizou();
            await _db.SaveChangesAsync(ct);
            return await PublicaAsync(c, s, ct);
        }

        public async Task<string> EnviarCodigoAsync(string token, CancellationToken ct = default)
        {
            var (c, s) = await PeloTokenAsync(token, ct);
            if (!c.Aberto) throw new ContratoException("Este documento não está mais aguardando assinatura.");
            if (s.Assinou) throw new ContratoException("Você já assinou este documento.");
            if (string.IsNullOrEmpty(s.CsgEmail))
                throw new ContratoException("Este documento não tem e-mail para enviar o código. Peça à loja para colher sua assinatura.");
            if (s.CsgCodigoExpira.HasValue && s.CsgCodigoExpira.Value - ValidadeCodigo + IntervaloCodigo > DateTime.UtcNow)
                throw new ContratoException("Já enviamos um código agora há pouco. Aguarde um minuto para pedir outro.");

            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            s.NovoCodigo(HashCodigo(s, codigo), DateTime.UtcNow + ValidadeCodigo);
            await _db.SaveChangesAsync(ct);

            var loja = await LojaDoVeiculoAsync(c.R_VeiId, ct);
            var corpo = $@"<p>Olá, {Html(s.CsgNome)}!</p>
<p>Seu código para assinar <strong>{Html(c.CtrTitulo)}</strong> ({Html(loja?.LojNome ?? "loja")}) é:</p>
<p style=""font-size:28px;letter-spacing:6px;font-weight:bold"">{codigo}</p>
<p style=""color:#666;font-size:13px"">Vale por 10 minutos. Se você não pediu, ignore este e-mail.</p>";
            if (!await _email.SendEmailAsync(s.CsgEmail, $"Código para assinar: {codigo}", corpo))
                throw new ContratoException("Não foi possível enviar o código agora. Tente de novo em alguns minutos.");
            return MascararEmail(s.CsgEmail);
        }

        public async Task<AssinaturaPublicaInfo> AssinarPeloLinkAsync(string token, AssinaturaInput input, AssinaturaOrigem origem, CancellationToken ct = default)
        {
            var (c, s) = await PeloTokenAsync(token, ct);
            if (!c.Aberto) throw new ContratoException("Este documento não está mais aguardando assinatura.");
            if (s.Assinou) throw new ContratoException("Você já assinou este documento.");
            if (string.IsNullOrEmpty(s.CsgEmail))
                throw new ContratoException("Este documento não tem e-mail para confirmar sua identidade. Peça à loja para colher sua assinatura.");

            var codigo = Digitos(input.Codigo);
            if (s.CsgCodigoHash == null || !s.CsgCodigoExpira.HasValue || s.CsgCodigoExpira < DateTime.UtcNow)
                throw new ContratoException("Peça o código por e-mail (ele vale por 10 minutos).");
            if (s.CsgTentativas >= ContratoSignatario.MaximoTentativasCodigo)
                throw new ContratoException("Código errado muitas vezes. Peça um código novo.");
            if (string.IsNullOrEmpty(codigo) || !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(HashCodigo(s, codigo)), Encoding.ASCII.GetBytes(s.CsgCodigoHash)))
            {
                s.ErrouCodigo();
                await _db.SaveChangesAsync(ct);
                throw new ContratoException("Código incorreto.");
            }

            var (nome, documento) = ConferirIdentidade(s, input);
            s.Assinar(ContratoSignatario.MetodoCodigoEmail, nome, documento, Imagem(input.Imagem), origem.Ip, origem.Navegador);
            c.Concluir();
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Contrato {Id}: cliente assinou pelo link", c.CtrId);
            return await PublicaAsync(c, s, ct);
        }

        public async Task<(byte[] Pdf, string NomeArquivo)> PdfPeloLinkAsync(string token, CancellationToken ct = default)
        {
            var (c, _) = await PeloTokenAsync(token, ct);
            var loja = await LojaDoVeiculoAsync(c.R_VeiId, ct);
            return (ContratoPdf.Gerar(c, loja?.LojNome ?? "loja"), $"contrato-{c.CtrId}.pdf");
        }

        // ================================================================
        // Apoio
        // ================================================================

        private async Task<AssinaturaPublicaInfo> PublicaAsync(Contrato c, ContratoSignatario s, CancellationToken ct)
        {
            var loja = await LojaDoVeiculoAsync(c.R_VeiId, ct);
            return new AssinaturaPublicaInfo
            {
                Loja = loja?.LojNome ?? "",
                Titulo = c.CtrTitulo,
                Blocos = ContratoTexto.Blocos(c.CtrConteudo),
                Hash = c.CtrHash,
                Papel = TiposContrato.PapelCliente(c.CtrTipo),
                Nome = s.CsgNome,
                EmailMascarado = s.CsgEmail == null ? null : MascararEmail(s.CsgEmail),
                PrecisaCodigo = s.CsgEmail != null,
                JaAssinou = s.Assinou,
                AssinadoEm = Utc(s.CsgDtAssinatura),
                StatusContrato = c.CtrStatus
            };
        }

        /// <summary>Nome obrigatorio; se o contrato tem CPF/CNPJ, quem assina tem de digitar o mesmo.</summary>
        private static (string Nome, string? Documento) ConferirIdentidade(ContratoSignatario s, AssinaturaInput input)
        {
            if (!input.Aceite) throw new ContratoException("Confirme que leu e concorda com o documento.");
            var nome = (input.Nome ?? "").Trim();
            if (nome.Length < 3) throw new ContratoException("Digite seu nome completo.");
            var documento = Digitos(input.Documento);
            if (!string.IsNullOrEmpty(s.CsgDocumento))
            {
                if (documento != s.CsgDocumento) throw new ContratoException("O CPF/CNPJ não confere com o do documento.");
            }
            else if (!string.IsNullOrEmpty(documento) && !DocumentoValido(documento))
                throw new ContratoException("CPF/CNPJ inválido.");
            return (nome, documento);
        }

        private static string Imagem(string? dataUrl)
        {
            if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith("data:image/png;base64,"))
                throw new ContratoException("Desenhe a assinatura no quadro.");
            if (dataUrl.Length > TamanhoMaximoImagem) throw new ContratoException("A imagem da assinatura ficou grande demais.");
            try
            {
                // Confere que e um PNG de verdade (o PDF nao abre imagem quebrada).
                var info = SixLabors.ImageSharp.Image.Identify(Convert.FromBase64String(dataUrl["data:image/png;base64,".Length..]));
                if (info.Width < 20 || info.Height < 10 || info.Width > 3000 || info.Height > 3000)
                    throw new ContratoException("Imagem da assinatura inválida.");
            }
            catch (Exception ex) when (ex is FormatException or SixLabors.ImageSharp.ImageFormatException or SixLabors.ImageSharp.UnknownImageFormatException)
            {
                throw new ContratoException("Imagem da assinatura inválida.");
            }
            return dataUrl;
        }

        private async Task<(Contrato, ContratoSignatario)> PeloTokenAsync(string token, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 100) throw new ContratoException("Link inválido.");
            var hash = HashToken(token);
            var s = await _db.ContratosSignatarios.FirstOrDefaultAsync(x => x.CsgTokenHash == hash, ct)
                ?? throw new ContratoException("Link inválido ou expirado.");
            var c = await _db.Contratos.Include(x => x.Signatarios).FirstAsync(x => x.CtrId == s.R_CtrId, ct);
            return (c, c.Signatarios.First(x => x.CsgId == s.CsgId));
        }

        private async Task<Contrato> ContratoAsync(int id, CancellationToken ct)
            => await _db.Contratos.Include(c => c.Signatarios).FirstOrDefaultAsync(c => c.CtrId == id, ct)
               ?? throw new ContratoException("Contrato não encontrado.");

        private async Task<Contrato> ContratoAbertoAsync(int id, CancellationToken ct)
        {
            var c = await ContratoAsync(id, ct);
            if (!c.Aberto) throw new ContratoException(c.CtrStatus == Contrato.StatusCancelado
                ? "O contrato está cancelado." : "O contrato já foi assinado por todos.");
            return c;
        }

        private async Task<ModeloContrato> ModeloAsync(int id, CancellationToken ct)
            => await _db.ModelosContrato.AsNoTracking().FirstOrDefaultAsync(m => m.MdcId == id, ct)
               ?? throw new ContratoException("Modelo não encontrado.");

        private async Task<(Veiculo, Loja?)> VeiculoELojaAsync(int veiculoId, CancellationToken ct)
        {
            var veiculo = await _db.Veiculos.AsNoTracking().FirstOrDefaultAsync(v => v.VeiId == veiculoId, ct)
                ?? throw new ContratoException("Veículo não encontrado.");
            var loja = await _db.Lojas.AsNoTracking().FirstOrDefaultAsync(l => l.LojId == veiculo.R_LojId, ct);
            return (veiculo, loja);
        }

        private async Task<Loja?> LojaDoVeiculoAsync(int veiculoId, CancellationToken ct)
        {
            var lojaId = await _db.Veiculos.AsNoTracking().Where(v => v.VeiId == veiculoId).Select(v => (int?)v.R_LojId).FirstOrDefaultAsync(ct);
            return lojaId == null ? null : await _db.Lojas.AsNoTracking().FirstOrDefaultAsync(l => l.LojId == lojaId, ct);
        }

        private static ContratoPreview Preview(string conteudo, string tituloPadrao, IReadOnlyDictionary<string, string?> valores)
        {
            var faltando = new List<string>();
            var texto = ContratoTexto.Preencher(conteudo, valores, faltando);
            var blocos = ContratoTexto.Blocos(texto);
            return new ContratoPreview { Titulo = ContratoTexto.Titulo(blocos, tituloPadrao), Blocos = blocos, Faltando = faltando };
        }

        /// <summary>Valores das variaveis a partir do veiculo, da loja e do formulario.</summary>
        public static Dictionary<string, string?> Valores(Veiculo v, Loja? l, string tipo, ContratoParte? parte, decimal? valor,
            string? formaPagamento, IReadOnlyDictionary<string, string>? extras)
        {
            var hoje = Hoje();
            var d = new Dictionary<string, string?>
            {
                ["loja.nome"] = l?.LojNome,
                ["loja.cnpj"] = FormatarDocumento(l?.LojCNPJ),
                ["loja.endereco"] = l == null ? null : Endereco(l),
                ["loja.cidade"] = l?.LojCidade,
                ["loja.uf"] = l?.LojEstado,
                ["loja.telefone"] = FormatarTelefone(l?.LojTel1),
                ["loja.email"] = l?.LojEmail,
                ["veiculo.marca"] = v.VeiMarca,
                ["veiculo.modelo"] = v.VeiModelo,
                ["veiculo.ano"] = v.VeiAno > 0 ? v.VeiAno.ToString() : null,
                ["veiculo.cor"] = v.VeiCor,
                ["veiculo.placa"] = v.VeiPlaca?.ToUpperInvariant(),
                ["veiculo.chassi"] = v.VeiChassi?.ToUpperInvariant(),
                ["veiculo.renavam"] = v.VeiRenavam,
                ["veiculo.km"] = v.VeiKm.ToString("N0", PtBr),
                ["veiculo.preco"] = v.VeiPreco > 0 ? ContratoTexto.Moeda(v.VeiPreco) : null,
                ["veiculo.opcionais"] = v.VeiOpcionais,
                ["cliente.papel"] = TiposContrato.PapelCliente(tipo),
                ["cliente.nome"] = parte?.Nome,
                ["cliente.documento"] = FormatarDocumento(parte?.Documento),
                ["cliente.email"] = parte?.Email,
                ["cliente.telefone"] = FormatarTelefone(parte?.Telefone),
                ["cliente.endereco"] = parte?.Endereco,
                ["contrato.valor"] = valor > 0 ? ContratoTexto.Moeda(valor.Value) : null,
                ["contrato.valor_extenso"] = valor > 0 ? ContratoTexto.Extenso(valor.Value) : null,
                ["contrato.forma_pagamento"] = formaPagamento,
                ["data.hoje"] = hoje.ToString("dd/MM/yyyy"),
                ["data.extenso"] = ContratoTexto.DataExtenso(hoje)
            };
            if (extras != null)
                foreach (var (k, val) in extras) d["extra." + k.Trim().ToLowerInvariant()] = val;
            return d;
        }

        /// <summary>Dados ficticios para a previa do modelo sem veiculo escolhido.</summary>
        private static Dictionary<string, string?> Exemplo(string tipo)
        {
            var hoje = Hoje();
            return new Dictionary<string, string?>
            {
                ["loja.nome"] = "Sua Loja Veículos Ltda", ["loja.cnpj"] = "00.000.000/0001-00",
                ["loja.endereco"] = "Rua Exemplo, 100 - Centro, Sua Cidade/SP, CEP 00000-000",
                ["loja.cidade"] = "Sua Cidade", ["loja.uf"] = "SP", ["loja.telefone"] = "(11) 0000-0000", ["loja.email"] = "contato@sualoja.com.br",
                ["veiculo.marca"] = "Volkswagen", ["veiculo.modelo"] = "Gol 1.0", ["veiculo.ano"] = "2020", ["veiculo.cor"] = "Prata",
                ["veiculo.placa"] = "ABC1D23", ["veiculo.chassi"] = "9BWAA05U0LT000000", ["veiculo.renavam"] = "00000000000",
                ["veiculo.km"] = "45.000", ["veiculo.preco"] = ContratoTexto.Moeda(55000), ["veiculo.opcionais"] = "Ar-condicionado, direção hidráulica",
                ["cliente.papel"] = TiposContrato.PapelCliente(tipo), ["cliente.nome"] = "Nome do Cliente", ["cliente.documento"] = "000.000.000-00",
                ["cliente.email"] = "cliente@email.com", ["cliente.telefone"] = "(11) 90000-0000", ["cliente.endereco"] = "Rua do Cliente, 10 - Bairro, Cidade/SP",
                ["contrato.valor"] = ContratoTexto.Moeda(55000), ["contrato.valor_extenso"] = ContratoTexto.Extenso(55000),
                ["contrato.forma_pagamento"] = "Pix", ["data.hoje"] = hoje.ToString("dd/MM/yyyy"), ["data.extenso"] = ContratoTexto.DataExtenso(hoje)
            };
        }

        private static string Endereco(Loja l)
        {
            var sb = new StringBuilder();
            sb.Append(l.LojLogradouro);
            if (!string.IsNullOrWhiteSpace(l.LojNumero)) sb.Append(", ").Append(l.LojNumero);
            if (!string.IsNullOrWhiteSpace(l.LojComplemento)) sb.Append(", ").Append(l.LojComplemento);
            if (!string.IsNullOrWhiteSpace(l.LojBairro)) sb.Append(" - ").Append(l.LojBairro);
            if (!string.IsNullOrWhiteSpace(l.LojCidade)) sb.Append(", ").Append(l.LojCidade).Append('/').Append(l.LojEstado);
            var cep = Digitos(l.LojCEP);
            if (cep is { Length: 8 }) sb.Append(", CEP ").Append(cep[..5]).Append('-').Append(cep[5..]);
            return sb.ToString().Trim(' ', ',', '-');
        }

        private static DateTime Hoje()
        {
            try { return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"); }
            catch (TimeZoneNotFoundException) { return DateTime.UtcNow.AddHours(-3); }
        }

        private ContratoInfo Info(Contrato c, Veiculo? v, bool comToken)
            => new()
            {
                Id = c.CtrId,
                VeiculoId = c.R_VeiId,
                Veiculo = v == null ? null : $"{v.VeiMarca} {v.VeiModelo} {v.VeiAno}".Trim(),
                Placa = v?.VeiPlaca,
                Tipo = c.CtrTipo,
                Titulo = c.CtrTitulo,
                Status = c.CtrStatus,
                Hash = c.CtrHash,
                CriadoEm = Utc(c.CtrDtCriacao),
                ConcluidoEm = Utc(c.CtrDtConclusao),
                Signatarios = c.Signatarios.OrderBy(s => s.CsgPapel == ContratoSignatario.PapelLoja ? 1 : 0).Select(s => new ContratoSignatarioInfo
                {
                    Id = s.CsgId,
                    Papel = s.CsgPapel,
                    PapelRotulo = ContratoPdf.Papel(s, c.CtrTipo),
                    Nome = s.CsgNomeAssinado ?? s.CsgNome,
                    Documento = FormatarDocumento(s.CsgDocumentoAssinado ?? s.CsgDocumento),
                    Email = s.CsgEmail,
                    Telefone = s.CsgTelefone,
                    Status = s.CsgStatus,
                    Metodo = s.CsgMetodo,
                    VisualizadoEm = Utc(s.CsgDtVisualizacao),
                    AssinadoEm = Utc(s.CsgDtAssinatura),
                    Token = comToken && c.Aberto && !s.Assinou && s.CsgTokenProtegido != null ? Desproteger(s.CsgTokenProtegido) : null
                }).ToList()
            };

        private string? Desproteger(string protegido)
        {
            try { return _protector.Unprotect(protegido); }
            catch (CryptographicException) { return null; }
        }

        private static ModeloContratoInfo Info(ModeloContrato m) => new()
        {
            Id = m.MdcId,
            Nome = m.MdcNome,
            Descricao = m.MdcDescricao,
            Tipo = m.MdcTipo,
            Conteudo = m.MdcConteudo,
            Ativo = m.MdcAtivo,
            Padrao = m.MdcPadrao,
            AtualizadoEm = Utc(m.MdcDtAtualizacao)
        };

        // O SQLite devolve as datas sem fuso; elas sao gravadas em UTC.
        private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
        private static DateTime? Utc(DateTime? d) => d.HasValue ? Utc(d.Value) : null;

        private string Link(string urlBase, string token)
            => $"{urlBase.TrimEnd('/')}/assinar/{Uri.EscapeDataString(_tenant.TenantSlug ?? "default")}/{token}";

        private static string NovoToken()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static string HashToken(string token)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private static string HashCodigo(ContratoSignatario s, string codigo)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{s.CsgId}:{s.CsgTokenHash}:{codigo}"))).ToLowerInvariant();

        private static string? Digitos(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var d = new string(texto.Where(char.IsDigit).ToArray());
            return d.Length == 0 ? null : d;
        }

        private static bool EmailValido(string email)
            => email.Length <= 200 && System.Net.Mail.MailAddress.TryCreate(email, out var a) && a.Address == email && email.Contains('.');

        private static string Html(string texto) => System.Net.WebUtility.HtmlEncode(texto);

        public static string MascararEmail(string email)
        {
            var arroba = email.IndexOf('@');
            if (arroba <= 1) return email;
            return email[0] + new string('*', Math.Min(arroba - 1, 6)) + email[arroba..];
        }

        public static string? FormatarDocumento(string? doc)
        {
            var d = Digitos(doc);
            return d switch
            {
                { Length: 11 } => $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}",
                { Length: 14 } => $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}",
                _ => doc
            };
        }

        public static string? FormatarTelefone(string? tel)
        {
            var d = Digitos(tel);
            if (d != null && d.StartsWith("55") && d.Length is 12 or 13) d = d[2..];
            return d switch
            {
                { Length: 11 } => $"({d[..2]}) {d[2..7]}-{d[7..]}",
                { Length: 10 } => $"({d[..2]}) {d[2..6]}-{d[6..]}",
                _ => tel
            };
        }

        public static bool DocumentoValido(string digitos)
        {
            if (digitos.Length == 11) return Cpf(digitos);
            if (digitos.Length == 14) return Cnpj(digitos);
            return false;
        }

        private static bool Cpf(string c)
        {
            if (c.Distinct().Count() == 1) return false;
            for (var t = 9; t < 11; t++)
            {
                var soma = 0;
                for (var i = 0; i < t; i++) soma += (c[i] - '0') * (t + 1 - i);
                var dv = soma * 10 % 11 % 10;
                if (c[t] - '0' != dv) return false;
            }
            return true;
        }

        private static bool Cnpj(string c)
        {
            if (c.Distinct().Count() == 1) return false;
            int[] p1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 }, p2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int Dv(int[] p) { var s = 0; for (var i = 0; i < p.Length; i++) s += (c[i] - '0') * p[i]; var r = s % 11; return r < 2 ? 0 : 11 - r; }
            return c[12] - '0' == Dv(p1) && c[13] - '0' == Dv(p2);
        }
    }
}
