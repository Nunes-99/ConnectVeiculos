using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Napista;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ConnectVeiculos.Infrastructure.Services.Napista
{
    /// <summary>
    /// Caminho inverso: a loja ja anunciava no NaPista antes do ConnectVeiculos.
    /// Le os anuncios publicados (GET /seller/{id}/offers, documentacao
    /// "Buscar Anuncios por Loja e Status") e cria os veiculos ja ligados a
    /// eles, sem criar outro anuncio la. Depois disso vale o fluxo normal:
    /// editar atualiza, vender marca vendido.
    /// </summary>
    public partial class NapistaService
    {
        private const int MaximoImportacao = 100;
        private const long TamanhoMaximoFoto = 15 * 1024 * 1024;
        private const int LadoMaximoFoto = 1920;

        /// <summary>
        /// Foto so e' baixada de endereco https publico: o endereco vem na
        /// resposta do NaPista, e nao pode servir para o servidor acessar a
        /// rede interna. Os testes trocam a checagem (nao ha DNS la).
        /// </summary>
        public static Func<Uri, CancellationToken, Task<bool>> EnderecoDeFotoPermitido { get; set; } = EnderecoPublicoAsync;

        private sealed class Oferta
        {
            public string Id = "";
            public string Titulo = "";
            public string Status = "";
            public string? VersaoId;
            public string? Descricao;
            public int? AnoModelo;
            public int? AnoFabricacao;
            public int Km;
            public decimal Preco;
            public string? Placa;
            public string? Combustivel;
            public string? Cor;
            public string? Cambio;
            public int? Portas;
            public bool Blindado;
            public List<string> Opcionais = new();
            public List<string> Fotos = new();
            public DateTime? CriadoEm;

            public bool PlacaEscondida => (Placa ?? "").Contains('*');
        }

        public async Task<List<NapistaAnuncioExistente>> ListarAnunciosExistentesAsync(CancellationToken ct = default)
        {
            var conta = await ContaObrigatoriaAsync();
            var ofertas = await OfertasPublicadasAsync(Seller(conta), ct);
            var vinculos = await VinculosAsync(ofertas.Select(o => o.Id).ToList(), ct);
            var veiculos = (await _veiculos.GetAllAsync()).ToList();

            var cores = new Dictionary<string, string>();
            try { cores = (await AtributosAsync("CAR", ct)).Cores.ToDictionary(c => c.Id, c => c.Nome); }
            catch (NapistaException) { }

            return ofertas.Select(o =>
            {
                var item = new NapistaAnuncioExistente
                {
                    AnuncioId = o.Id,
                    Titulo = o.Titulo,
                    Status = o.Status,
                    AnoModelo = o.AnoModelo,
                    AnoFabricacao = o.AnoFabricacao,
                    Km = o.Km,
                    Preco = o.Preco,
                    Placa = o.Placa,
                    PlacaEscondida = o.PlacaEscondida,
                    Cor = o.Cor != null && cores.TryGetValue(o.Cor, out var nomeCor) ? nomeCor : o.Cor,
                    Fotos = o.Fotos.Count,
                    FotoCapa = o.Fotos.FirstOrDefault(f => f.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                };
                Veiculo? v = null;
                if (vinculos.TryGetValue(o.Id, out var vinculado))
                {
                    item.VeiculoVinculadoId = vinculado;
                    v = veiculos.FirstOrDefault(x => x.VeiId == vinculado);
                }
                else if (!o.PlacaEscondida && Placa(o.Placa).Length > 0)
                {
                    v = veiculos.FirstOrDefault(x => Placa(x.VeiPlaca) == Placa(o.Placa));
                    item.VeiculoMesmaPlacaId = v?.VeiId;
                }
                if (v != null) item.VeiculoDescricao = $"{v.VeiMarca} {v.VeiModelo} {v.VeiAno} ({v.VeiPlaca})".Replace("  ", " ");
                return item;
            }).ToList();
        }

        // Uma importacao por loja de cada vez: duas juntas (duplo clique, duas
        // pessoas) viam o mesmo anuncio sem vinculo e criavam o carro duas vezes.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> TravasImportacao = new();

        public async Task<NapistaImportacaoResultado> ImportarAnunciosAsync(NapistaImportarPedido pedido, CancellationToken ct = default)
        {
            var trava = TravasImportacao.GetOrAdd(_tenant.TenantSlug ?? "", _ => new SemaphoreSlim(1, 1));
            await trava.WaitAsync(ct);
            try { return await ImportarSemTravaAsync(pedido, ct); }
            finally { trava.Release(); }
        }

        private async Task<NapistaImportacaoResultado> ImportarSemTravaAsync(NapistaImportarPedido pedido, CancellationToken ct)
        {
            var conta = await ContaObrigatoriaAsync();
            var itens = (pedido.Itens ?? new()).Where(i => !string.IsNullOrWhiteSpace(i.AnuncioId))
                .GroupBy(i => i.AnuncioId).Select(g => g.First()).ToList();
            if (itens.Count == 0) throw new NapistaException("Escolha ao menos um anúncio.");
            if (itens.Count > MaximoImportacao) throw new NapistaException($"Importe no máximo {MaximoImportacao} anúncios por vez.");
            if (await _lojas.GetByIdAsync(pedido.LojaId) == null) throw new NapistaException("Escolha a loja onde os veículos vão entrar.");
            if (!await _db.Categorias.AsNoTracking().AnyAsync(c => c.CatId == pedido.CategoriaId, ct))
                throw new NapistaException("Escolha a categoria dos veículos.");

            var ofertas = (await OfertasPublicadasAsync(Seller(conta), ct)).ToDictionary(o => o.Id);
            var vinculos = await VinculosAsync(itens.Select(i => i.AnuncioId).ToList(), ct);
            var veiculos = (await _veiculos.GetAllAsync()).ToList();
            var resultado = new NapistaImportacaoResultado();

            foreach (var item in itens)
            {
                var d = new NapistaImportacaoDetalhe { AnuncioId = item.AnuncioId };
                resultado.Detalhes.Add(d);
                try
                {
                    if (!ofertas.TryGetValue(item.AnuncioId, out var o))
                        throw new NapistaException("O anúncio não está mais publicado no NaPista.");
                    d.Titulo = o.Titulo;

                    if (vinculos.TryGetValue(o.Id, out var jaLigado))
                    {
                        (d.Sucesso, d.Acao, d.VeiculoId, d.Mensagem) = (true, "ignorado", jaLigado, $"Já estava no ConnectVeiculos (veículo #{jaLigado}).");
                        continue;
                    }

                    var placa = PlacaDoPedido(o, item.Placa);
                    var mesmaPlaca = veiculos.FirstOrDefault(v => Placa(v.VeiPlaca) == placa);
                    if (mesmaPlaca != null)
                    {
                        await VincularAsync(o, mesmaPlaca, ct);
                        vinculos[o.Id] = mesmaPlaca.VeiId;
                        (d.Sucesso, d.Acao, d.VeiculoId) = (true, "vinculado", mesmaPlaca.VeiId);
                        d.Mensagem = $"Ligado ao veículo já cadastrado com a placa {mesmaPlaca.VeiPlaca}.";
                        resultado.Vinculados++;
                        continue;
                    }

                    var (novoId, veiculo, fotos, semEndereco) = await CriarVeiculoAsync(o, placa, pedido, ct);
                    veiculos.Add(veiculo);
                    vinculos[o.Id] = novoId;
                    (d.Sucesso, d.Acao, d.VeiculoId, d.Fotos) = (true, "criado", novoId, fotos);
                    d.Mensagem = $"Veículo criado com {fotos} foto(s)."
                                 + (semEndereco > 0 ? $" {semEndereco} foto(s) do NaPista não puderam ser baixadas; envie pelo cadastro." : "");
                    resultado.Criados++;
                }
                catch (Exception ex) when (ex is NapistaException or Core.Exceptions.LimitePlanoException)
                {
                    (d.Sucesso, d.Mensagem) = (false, ex.Message);
                    resultado.Erros++;
                }
            }
            _logger.LogInformation("NaPista: importacao de anuncios no tenant {Tenant}: {Criados} criado(s), {Vinculados} ligado(s), {Erros} erro(s)",
                _tenant.TenantSlug, resultado.Criados, resultado.Vinculados, resultado.Erros);
            return resultado;
        }

        // ================================================================

        private async Task<List<Oferta>> OfertasPublicadasAsync(string sellerId, CancellationToken ct)
        {
            var ofertas = new List<Oferta>();
            for (var pagina = 0; pagina < 40; pagina++)
            {
                using var doc = await GetAsync($"/seller/{sellerId}/offers?status=PUBLISHED&page={pagina}&limit=50", ct);
                var r = doc.RootElement;
                if (r.TryGetProperty("pageData", out var lista) && lista.ValueKind == JsonValueKind.Array)
                    ofertas.AddRange(lista.EnumerateArray().Select(LerOferta).Where(o => o.Id != ""));
                var total = r.TryGetProperty("totalPages", out var tp) && tp.TryGetInt32(out var t) ? t : 1;
                if (pagina + 1 >= total) break;
            }
            return ofertas.GroupBy(o => o.Id).Select(g => g.First()).ToList();
        }

        private static Oferta LerOferta(JsonElement e)
        {
            var o = new Oferta
            {
                Id = Texto(e, "id") ?? "",
                Titulo = (Texto(e, "title") ?? "").Trim(),
                Status = Texto(e, "status") ?? "",
                VersaoId = Texto(e, "versionId"),
                Descricao = Texto(e, "description"),
                AnoModelo = Inteiro(e, "modelYear"),
                AnoFabricacao = Inteiro(e, "manufacturedYear"),
                Km = Math.Max(Inteiro(e, "mileage") ?? 0, 0),
                Preco = Math.Max(Decimal(e, "price") ?? 0, 0),
                Placa = Texto(e, "plate"),
                Combustivel = Texto(e, "fuelType"),
                Cor = Texto(e, "color"),
                Cambio = Texto(e, "transmissionType"),
                Portas = Inteiro(e, "numberOfDoors"),
                Blindado = e.TryGetProperty("armoured", out var b) && b.ValueKind == JsonValueKind.True,
            };
            if (e.TryGetProperty("equipments", out var eq) && eq.ValueKind == JsonValueKind.Array)
                o.Opcionais = eq.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList();
            if (e.TryGetProperty("photos", out var fs) && fs.ValueKind == JsonValueKind.Array)
                o.Fotos = fs.EnumerateArray()
                    .Select(f => (url: Texto(f, "url"), ordem: Inteiro(f, "order") ?? int.MaxValue))
                    .Where(f => !string.IsNullOrWhiteSpace(f.url))
                    .OrderBy(f => f.ordem).Select(f => f.url!.Trim()).ToList();
            if (Texto(e, "createdDate") is { } criado &&
                DateTime.TryParse(criado, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
                o.CriadoEm = dt;
            return o;
        }

        private async Task<Dictionary<string, int>> VinculosAsync(List<string> anuncioIds, CancellationToken ct)
            => await _anuncios.VeiculosPorAnuncioAsync(anuncioIds);

        /// <summary>
        /// Placa do anuncio, ou a que a pessoa digitou quando o NaPista
        /// esconde ("******4"): a digitada tem de bater com o final visivel.
        /// </summary>
        public static string PlacaDoPedido(string? placaNoNapista, string? placaDigitada)
        {
            var original = (placaNoNapista ?? "").Trim();
            if (original.Length > 0 && !original.Contains('*'))
            {
                var p = Placa(original);
                if (p.Length != 7) throw new NapistaException($"A placa do anúncio ({original}) não é válida.");
                return p;
            }
            var digitada = Placa(placaDigitada);
            if (digitada.Length == 0)
                throw new NapistaException("O NaPista não mostra a placa inteira deste anúncio. Digite a placa para importar.");
            if (digitada.Length != 7 || !char.IsLetter(digitada[0]) || !char.IsDigit(digitada[3]))
                throw new NapistaException($"A placa {placaDigitada} não é válida (ex.: ABC1D23).");
            var finalVisivel = new string(original.Reverse().TakeWhile(c => c != '*').Reverse().Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (finalVisivel.Length > 0 && !digitada.EndsWith(finalVisivel))
                throw new NapistaException($"A placa {digitada} não bate com a do anúncio (termina em {finalVisivel}).");
            return digitada;
        }

        private static string PlacaDoPedido(Oferta o, string? digitada) => PlacaDoPedido(o.Placa, digitada);

        /// <summary>Ja existe veiculo com a placa: so liga ao anuncio. As fotos do NaPista ficam ate a primeira alteracao.</summary>
        private async Task VincularAsync(Oferta o, Veiculo veiculo, CancellationToken ct)
        {
            if (veiculo.VeiSts != "D")
                throw new NapistaException($"O veículo com a placa {veiculo.VeiPlaca} não está disponível no ConnectVeiculos (vendido, reservado ou inativo).");
            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculo.VeiId);
            if (anuncio != null && anuncio.TemAnuncio && anuncio.VnpAnuncioId != o.Id && anuncio.VnpStatus != VeiculoNapista.StatusRemovido)
                throw new NapistaException($"O veículo com a placa {veiculo.VeiPlaca} já está ligado a outro anúncio do NaPista.");

            var dados = await DadosDaOfertaAsync(o, ct);
            anuncio ??= new VeiculoNapista(veiculo.VeiId, "");
            anuncio.SalvarDados(JsonSerializer.Serialize(dados.Dados, Json));
            anuncio.Publicado(o.Id);
            // Fotos do cadastro contam como enviadas: as do NaPista ficam ate a
            // primeira alteracao, que reenvia as do cadastro (o PATCH apaga as de la).
            var fotos = (await _imagens.GetByVeiculoIdAsync(veiculo.VeiId)).Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem).Take(MaximoFotos).ToList();
            anuncio.FotosEnviadas(string.Join("|", fotos.Select(f => f.ImgCaminho)), fotos.Count);
            await _anuncios.SalvarAsync(anuncio);
        }

        private async Task<(int Id, Veiculo Veiculo, int Fotos, int SemEndereco)> CriarVeiculoAsync(Oferta o, string placa, NapistaImportarPedido pedido, CancellationToken ct)
        {
            if (o.AnoModelo is not > 1900) throw new NapistaException("O anúncio não tem ano do modelo.");
            await _limites.GarantirPodeCriarVeiculoAsync(ct);

            var info = await DadosDaOfertaAsync(o, ct);
            var veiculo = new Veiculo(0, pedido.LojaId, pedido.CategoriaId,
                Limitar(info.Marca, 100), Limitar(info.Modelo, 200), (short)o.AnoModelo.Value,
                placa, "", Limitar(info.Cor, 50), o.Km, o.Preco,
                o.CriadoEm?.ToLocalTime() ?? DateTime.Now, "D", o.Km == 0 ? "Novo" : "Usado", 0m,
                veiObservacao: string.IsNullOrWhiteSpace(o.Descricao) ? null : Limitar(o.Descricao.Trim(), 2000),
                veiOpcionais: info.Opcionais.Count == 0 ? null : Limitar(string.Join(",", info.Opcionais), 1000));
            var id = await _veiculos.CreateAsync(veiculo);

            var (caminhos, semEndereco) = await BaixarFotosAsync(id, o.Fotos, ct);

            var anuncio = new VeiculoNapista(id, JsonSerializer.Serialize(info.Dados, Json));
            anuncio.Publicado(o.Id);
            anuncio.FotosEnviadas(string.Join("|", caminhos), caminhos.Count);
            await _anuncios.SalvarAsync(anuncio);
            return (id, veiculo, caminhos.Count, semEndereco);
        }

        private sealed record DadosImportados(NapistaAnuncioDados Dados, string Marca, string Modelo, string Cor, List<string> Opcionais);

        /// <summary>
        /// O anuncio traz so o titulo ("MARCA MODELO VERSAO") e o versionId.
        /// Marca e modelo saem do catalogo: a marca e' o maior nome do catalogo
        /// que abre o titulo; o modelo, o maior que abre o resto.
        /// </summary>
        private async Task<DadosImportados> DadosDaOfertaAsync(Oferta o, CancellationToken ct)
        {
            var categorias = new List<string>();
            var prefixo = o.Opcionais.Select(x => x.Split('_')[0]).FirstOrDefault(x => Categorias.Contains(x));
            if (prefixo != null) categorias.Add(prefixo);
            categorias.AddRange(Categorias.Where(c => c != prefixo));

            var titulo = o.Titulo.Trim();
            var dados = new NapistaAnuncioDados
            {
                Categoria = categorias[0], VersaoId = o.VersaoId, AnoModelo = o.AnoModelo, AnoFabricacao = o.AnoFabricacao ?? o.AnoModelo,
                Cor = o.Cor, Combustivel = o.Combustivel, Cambio = o.Cambio, Portas = o.Portas, Blindado = o.Blindado,
                Opcionais = o.Opcionais.ToList()
            };
            string marca = "", modelo = titulo;
            try
            {
                foreach (var cat in categorias)
                {
                    var m = MarcaDoTitulo(titulo, await MarcasAsync(cat, ct));
                    if (m == null) continue;
                    dados.Categoria = cat;
                    dados.MarcaId = m.Id;
                    marca = Capitalizar(m.Nome);
                    modelo = titulo[Math.Min(titulo.Length, m.Nome.Length)..].Trim();
                    dados.ModeloId = NapistaSugestao.Modelo(modelo, await ModelosAsync(cat, m.Id, o.AnoModelo, ct))?.Id;
                    break;
                }
            }
            catch (NapistaException ex)
            {
                _logger.LogInformation("NaPista: catalogo indisponivel ao importar {Anuncio}: {Erro}", o.Id, ex.Message);
            }
            if (marca == "")
            {
                // Sem catalogo: a primeira palavra do titulo faz as vezes de marca.
                var partes = titulo.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                marca = partes.Length > 0 ? Capitalizar(partes[0]) : "Não informada";
                modelo = partes.Length > 1 ? partes[1] : titulo;
            }
            if (modelo == "") modelo = titulo;

            string cor = o.Cor ?? "";
            var opcionais = new List<string>();
            try
            {
                var atributos = await AtributosAsync(dados.Categoria, ct);
                cor = atributos.Cores.FirstOrDefault(c => c.Id == o.Cor)?.Nome ?? cor;
                opcionais = o.Opcionais.Select(id => atributos.Opcionais.FirstOrDefault(x => x.Id == id)?.Nome)
                    .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList();
            }
            catch (NapistaException) { }

            return new DadosImportados(dados, marca, modelo, cor, opcionais);
        }

        public static NapistaItem? MarcaDoTitulo(string titulo, IEnumerable<NapistaItem> marcas)
        {
            var t = titulo.Trim().ToUpperInvariant() + " ";
            return marcas.Where(m => m.Nome.Trim().Length > 0 && t.StartsWith(m.Nome.Trim().ToUpperInvariant() + " "))
                .OrderByDescending(m => m.Nome.Length).FirstOrDefault();
        }

        private static string Capitalizar(string texto)
        {
            var t = CultureInfo.GetCultureInfo("pt-BR").TextInfo;
            // Siglas curtas (BMW, BYD, GWM, MG) ficam em maiusculas.
            return string.Join(' ', texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Length <= 3 ? p.ToUpperInvariant() : t.ToTitleCase(p.ToLowerInvariant())));
        }

        /// <summary>
        /// Baixa as fotos do anuncio para o cadastro, como o upload da tela faz
        /// (JPEG, ate 1920px). Endereco relativo ou fora do ar e' pulado.
        /// </summary>
        private async Task<(List<string> Caminhos, int SemEndereco)> BaixarFotosAsync(int veiculoId, List<string> urls, CancellationToken ct)
        {
            var caminhos = new List<string>();
            var falhas = 0;
            var pasta = Path.Combine(_pastaRaiz, "uploads", "veiculos", veiculoId.ToString());
            foreach (var url in urls.Take(MaximoFotos))
            {
                try
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                        || !await EnderecoDeFotoPermitido(uri, ct))
                    {
                        _logger.LogInformation("NaPista: foto sem endereco utilizavel no veiculo {VeiculoId}: {Url}", veiculoId, url.Length > 200 ? url[..200] : url);
                        falhas++;
                        continue;
                    }
                    using var resp = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (!resp.IsSuccessStatusCode || resp.Content.Headers.ContentLength > TamanhoMaximoFoto) { falhas++; continue; }

                    await using var origem = await resp.Content.ReadAsStreamAsync(ct);
                    using var memoria = new MemoryStream();
                    var buffer = new byte[81920];
                    int lidos;
                    while ((lidos = await origem.ReadAsync(buffer, ct)) > 0)
                    {
                        if (memoria.Length + lidos > TamanhoMaximoFoto) throw new InvalidDataException("foto grande demais");
                        memoria.Write(buffer, 0, lidos);
                    }
                    memoria.Position = 0;

                    using var imagem = await Image.LoadAsync(memoria, ct);
                    if (imagem.Width > LadoMaximoFoto || imagem.Height > LadoMaximoFoto)
                        imagem.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(LadoMaximoFoto, LadoMaximoFoto) }));
                    Directory.CreateDirectory(pasta);
                    var nome = $"{Guid.NewGuid()}.jpg";
                    await imagem.SaveAsJpegAsync(Path.Combine(pasta, nome), new JpegEncoder { Quality = 85 }, ct);

                    var caminho = $"/uploads/veiculos/{veiculoId}/{nome}";
                    await _imagens.CreateAsync(new VeiculoImagem(0, veiculoId, caminho, caminhos.Count + 1, true));
                    caminhos.Add(caminho);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UnknownImageFormatException
                                               or InvalidImageContentException or InvalidDataException or IOException)
                {
                    _logger.LogInformation("NaPista: foto nao baixada no veiculo {VeiculoId}: {Erro}", veiculoId, ex.Message);
                    falhas++;
                }
            }
            return (caminhos, falhas);
        }

        private static async Task<bool> EnderecoPublicoAsync(Uri uri, CancellationToken ct)
        {
            if (uri.IsLoopback) return false;
            IPAddress[] ips;
            try { ips = IPAddress.TryParse(uri.Host, out var ip) ? new[] { ip } : await Dns.GetHostAddressesAsync(uri.Host, ct); }
            catch (SocketException) { return false; }
            return ips.Length > 0 && ips.All(Publico);
        }

        private static bool Publico(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return false;
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                return !(ip.GetAddressBytes()[0] is 0xfc or 0xfd) && !ip.Equals(IPAddress.IPv6None);
            var b = ip.GetAddressBytes();
            return !(b[0] == 10 || b[0] == 127 || b[0] == 0 || b[0] >= 224
                     || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                     || (b[0] == 192 && b[1] == 168)
                     || (b[0] == 169 && b[1] == 254)
                     || (b[0] == 100 && b[1] >= 64 && b[1] <= 127));
        }
    }
}
