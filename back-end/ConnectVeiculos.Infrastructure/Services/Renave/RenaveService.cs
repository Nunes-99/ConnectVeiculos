using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConnectVeiculos.Core.Entities.Renave;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Renave;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Renave;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Renave
{
    /// <summary>
    /// Renave-WS (Serpro). Fluxo de um usado, na ordem do manual:
    ///   aptidao -> entrada -> assinatura do ATPV pelo vendedor -> NF-e de compra
    ///   -> (Detran confirma) -> saida -> ATPVe do comprador -> NF-e de venda.
    /// A configuracao (ambiente, operador, certificado) fica na
    /// ConfiguracaoSistema do tenant; o certificado e a senha, cifrados.
    /// </summary>
    public class RenaveService : IRenaveService
    {
        private const string KeyAmbiente = "RENAVE_AMBIENTE";
        private const string KeyOperadorCpf = "RENAVE_OPERADOR_CPF";
        private const string KeyOperadorNome = "RENAVE_OPERADOR_NOME";
        private const string KeyEmailLoja = "RENAVE_EMAIL_LOJA";
        private const string KeyCnpjEstabelecimento = "RENAVE_CNPJ_ESTABELECIMENTO";
        private const string KeyCertPfx = "RENAVE_CERT_PFX";
        private const string KeyCertSenha = "RENAVE_CERT_SENHA";
        private const string KeyCertTitular = "RENAVE_CERT_TITULAR";
        private const string KeyCertValidade = "RENAVE_CERT_VALIDADE";

        // O Renave e' do Brasil: datas sem fuso, no horario de Brasilia.
        private static readonly TimeZoneInfo Brasilia = ObterFusoBrasilia();

        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IRenaveTransporte _transporte;
        private readonly RenaveSettings _settings;
        private readonly IConfiguracaoSistemaRepository _config;
        private readonly IVeiculoRenaveRepository _registros;
        private readonly IVeiculoRepository _veiculos;
        private readonly ITokenProtector _protector;
        private readonly ILogger<RenaveService> _logger;

        public RenaveService(
            IRenaveTransporte transporte,
            IOptions<RenaveSettings> settings,
            IConfiguracaoSistemaRepository config,
            IVeiculoRenaveRepository registros,
            IVeiculoRepository veiculos,
            ITokenProtector protector,
            ILogger<RenaveService> logger)
        {
            _transporte = transporte;
            _settings = settings.Value;
            _config = config;
            _registros = registros;
            _veiculos = veiculos;
            _protector = protector;
            _logger = logger;
        }

        // ================================================================
        // Configuracao
        // ================================================================

        public async Task<RenaveConfigInfo> ObterConfigAsync(CancellationToken ct = default)
        {
            var info = new RenaveConfigInfo
            {
                Ambiente = RenaveAmbiente.Normalizar(await _config.GetValorAsync(KeyAmbiente)),
                OperadorCpf = Vazio(await _config.GetValorAsync(KeyOperadorCpf)),
                OperadorNome = Vazio(await _config.GetValorAsync(KeyOperadorNome)),
                EmailLoja = Vazio(await _config.GetValorAsync(KeyEmailLoja)),
                CnpjEstabelecimento = Vazio(await _config.GetValorAsync(KeyCnpjEstabelecimento)),
                CertificadoCadastrado = !string.IsNullOrEmpty(await _config.GetValorAsync(KeyCertPfx)),
                CertificadoTitular = Vazio(await _config.GetValorAsync(KeyCertTitular)),
            };
            if (DateTime.TryParse(await _config.GetValorAsync(KeyCertValidade), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var validade))
                info.CertificadoValidade = validade;

            var certOk = info.CertificadoCadastrado && info.CertificadoValidade > DateTime.UtcNow;
            info.Pronto = info.OperadorCpf != null && info.EmailLoja != null
                && (info.Ambiente == RenaveAmbiente.Homologacao || certOk);
            return info;
        }

        public async Task<RenaveConfigInfo> SalvarConfigAsync(RenaveConfigInput input, CancellationToken ct = default)
        {
            var cpf = SoDigitos(input.OperadorCpf);
            if (cpf.Length > 0 && !DocumentoValido(cpf))
                throw new RenaveException("CPF do operador inválido.");
            var cnpj = SoDigitos(input.CnpjEstabelecimento);
            if (cnpj.Length > 0 && !DocumentoValido(cnpj))
                throw new RenaveException("CNPJ do estabelecimento inválido.");
            var email = (input.EmailLoja ?? "").Trim();
            if (email.Length > 0 && !email.Contains('@'))
                throw new RenaveException("E-mail da loja inválido.");

            await _config.SetValorAsync(KeyAmbiente, RenaveAmbiente.Normalizar(input.Ambiente));
            await _config.SetValorAsync(KeyOperadorCpf, cpf);
            await _config.SetValorAsync(KeyOperadorNome, (input.OperadorNome ?? "").Trim());
            await _config.SetValorAsync(KeyEmailLoja, email);
            await _config.SetValorAsync(KeyCnpjEstabelecimento, cnpj);
            return await ObterConfigAsync(ct);
        }

        public async Task<RenaveConfigInfo> SalvarCertificadoAsync(byte[] pfx, string senha, CancellationToken ct = default)
        {
            X509Certificate2 cert;
            try
            {
                cert = CarregarCertificado(pfx, senha);
            }
            catch (CryptographicException)
            {
                throw new RenaveException("Não foi possível abrir o certificado. Confira o arquivo (.pfx/.p12) e a senha.");
            }

            using (cert)
            {
                if (!cert.HasPrivateKey)
                    throw new RenaveException("O arquivo não tem a chave privada. Exporte o certificado A1 com a chave (.pfx).");
                if (cert.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
                    throw new RenaveException($"Certificado vencido em {cert.NotAfter:dd/MM/yyyy}.");

                await _config.SetValorAsync(KeyCertPfx, _protector.Protect(Convert.ToBase64String(pfx)));
                await _config.SetValorAsync(KeyCertSenha, _protector.Protect(senha ?? ""));
                await _config.SetValorAsync(KeyCertTitular, cert.GetNameInfo(X509NameType.SimpleName, false));
                await _config.SetValorAsync(KeyCertValidade, cert.NotAfter.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            }
            _logger.LogInformation("Renave: certificado A1 cadastrado");
            return await ObterConfigAsync(ct);
        }

        public async Task<RenaveConfigInfo> RemoverCertificadoAsync(CancellationToken ct = default)
        {
            foreach (var k in new[] { KeyCertPfx, KeyCertSenha, KeyCertTitular, KeyCertValidade })
                await _config.SetValorAsync(k, "");
            return await ObterConfigAsync(ct);
        }

        public async Task<RenaveConexao> TestarConexaoAsync(CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            using var doc = await ChamarAsync(ctx, HttpMethod.Get, "/api/cliente-autenticado", null, ct);
            var r = doc!.RootElement;
            return new RenaveConexao
            {
                Ambiente = ctx.Config.Ambiente,
                Cnpj = Texto(r, "cnpj"),
                Nome = Texto(r, "nome"),
                Integradora = Texto(r, "nomeIntegradora")
            };
        }

        public async Task<IReadOnlyList<RenaveMunicipio>> BuscarMunicipiosAsync(string uf, string? nome, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            var q = $"/api/municipios?uf={Uri.EscapeDataString((uf ?? "").Trim().ToUpperInvariant())}";
            if (!string.IsNullOrWhiteSpace(nome)) q += "&nome=" + Uri.EscapeDataString(nome.Trim().ToUpperInvariant());
            using var doc = await ChamarAsync(ctx, HttpMethod.Get, q, null, ct);
            var lista = new List<RenaveMunicipio>();
            foreach (var m in doc!.RootElement.EnumerateArray())
            {
                if (int.TryParse(Texto(m, "id"), out var id))
                    lista.Add(new RenaveMunicipio { Id = id, Nome = Texto(m, "nome") ?? "", Uf = Texto(m, "uf") ?? "" });
            }
            return lista;
        }

        // ================================================================
        // Situacao por veiculo
        // ================================================================

        public async Task<RenaveSituacao> ObterSituacaoAsync(int veiculoId, CancellationToken ct = default)
            => ParaSituacao(veiculoId, await _registros.GetByVeiculoAsync(veiculoId));

        public async Task<IReadOnlyDictionary<int, RenaveSituacao>> ObterSituacoesAsync(IEnumerable<int> veiculoIds, CancellationToken ct = default)
        {
            var registros = await _registros.GetByVeiculosAsync(veiculoIds);
            return registros.ToDictionary(r => r.R_VeiId, r => ParaSituacao(r.R_VeiId, r));
        }

        public async Task<RenaveSituacao> AtualizarSituacaoAsync(int veiculoId, CancellationToken ct = default)
        {
            var registro = await _registros.GetByVeiculoAsync(veiculoId);
            if (registro?.RnvIdEstoque == null) return ParaSituacao(veiculoId, registro);

            var ctx = await ContextoAsync(exigirOperador: false, ct);
            using var doc = await ChamarAsync(ctx, HttpMethod.Get, $"/api/estoques/{registro.RnvIdEstoque}", null, ct);
            var estado = Texto(doc!.RootElement, "estado");
            if (estado != null && estado != registro.RnvEstado)
            {
                registro.AtualizarEstado(estado);
                await _registros.SalvarAsync(registro);
            }
            return ParaSituacao(veiculoId, registro);
        }

        // ================================================================
        // Operacoes
        // ================================================================

        public async Task<RenaveAptidao> ConsultarAptidaoAsync(int veiculoId, RenaveCrvInput crv, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            var (placa, renavam) = await PlacaRenavamAsync(veiculoId);
            var tipo = TipoCrv(crv.TipoCrv);
            var q = $"/api/aptidao-veiculo-estoque?placa={placa}&renavam={renavam}&tipoCrv={tipo}";
            var numero = SoDigitos(crv.NumeroCrv);
            if (numero.Length > 0) q += "&numeroCrv=" + numero;

            using var doc = await ChamarAsync(ctx, HttpMethod.Get, q, null, ct);
            var r = doc!.RootElement;
            var apt = new RenaveAptidao { ComunicacaoComDetranFalhou = Bool(r, "comunicacaoComDetranFalhou") };
            if (r.TryGetProperty("diagnostico", out var d) && d.ValueKind == JsonValueKind.Object)
            {
                apt.Apto = Bool(d, "veiculoApto");
                if (d.TryGetProperty("motivosParaNaoAptidao", out var mot) && mot.ValueKind == JsonValueKind.Array)
                    apt.Motivos = mot.EnumerateArray().Select(m => m.GetString() ?? "").Where(m => m.Length > 0).ToList();
            }
            if (r.TryGetProperty("informacoesDebitos", out var deb) && deb.ValueKind == JsonValueKind.Object)
            {
                apt.ExistemDebitos = Bool(deb, "existemDebitos");
                apt.DebitoIpva = Decimal(deb, "valorDebitoIpva");
                apt.DebitoLicenciamento = Decimal(deb, "valorDebitoLicenciamento");
                apt.DebitoMultas = Decimal(deb, "valorDebitoMultas");
                apt.DebitoDpvat = Decimal(deb, "valorDebitoDpvat");
                apt.OutrosDebitos = Decimal(deb, "valorTaxasEDebitosDiversos");
            }
            if (r.TryGetProperty("veiculo", out var v) && v.ValueKind == JsonValueKind.Object
                && v.TryGetProperty("restricoes", out var rs) && rs.ValueKind == JsonValueKind.Array)
                apt.Restricoes = rs.EnumerateArray().Select(x => Texto(x, "tipo") ?? "").Where(x => x.Length > 0).ToList();
            return apt;
        }

        public async Task<RenaveSituacao> SolicitarEntradaAsync(int veiculoId, RenaveEntradaInput input, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: true, ct);
            var registro = await _registros.GetByVeiculoAsync(veiculoId) ?? new VeiculoRenave(veiculoId);
            if (registro.EmEstoque)
                throw new RenaveException("Este veículo já tem entrada registrada no Renave.");

            var (placa, renavam) = await PlacaRenavamAsync(veiculoId);
            var vendedor = SoDigitos(input.DocumentoVendedor);
            if (!DocumentoValido(vendedor))
                throw new RenaveException("CPF/CNPJ do vendedor inválido.");
            if (string.IsNullOrWhiteSpace(input.EmailVendedor) || !input.EmailVendedor.Contains('@'))
                throw new RenaveException("O Renave exige o e-mail do vendedor.");
            if (input.ValorCompra <= 0)
                throw new RenaveException("Informe o valor de compra.");
            var codigo = SoDigitos(input.CodigoSegurancaCrv);
            if (codigo.Length == 0)
                throw new RenaveException("Informe o código de segurança do CRV.");

            var corpo = new
            {
                dataCompra = Data(input.DataCompra),
                valorCompra = input.ValorCompra,
                emailEstabelecimento = ctx.Config.EmailLoja,
                emailVendedor = input.EmailVendedor!.Trim(),
                cpfOperadorResponsavel = ctx.Config.OperadorCpf,
                veiculo = new
                {
                    placa,
                    renavam,
                    numeroCrv = Vazio(SoDigitos(input.NumeroCrv)),
                    codigoSegurancaCrv = codigo,
                    tipoCrv = TipoCrv(input.TipoCrv),
                    quilometragemHodometro = input.QuilometragemHodometro,
                    dataHoraMedicaoHodometro = DataHora(input.DataHoraMedicaoHodometro ?? AgoraBrasilia()),
                    documentoProprietarioAtual = vendedor,
                    tipoDocumentoProprietarioAtual = vendedor.Length == 14 ? "CNPJ" : "CPF"
                }
            };

            using var doc = await ChamarComRegistroAsync(ctx, registro, HttpMethod.Post, "/api/solicitacoes-entrada-estoque", corpo, ct);
            var r = doc.RootElement;
            registro.RegistrarEntrada(r.GetProperty("id").GetInt64(), Texto(r, "estado") ?? "SOLICITADO",
                input.DataCompra.Date, input.ValorCompra);
            await _registros.SalvarAsync(registro);
            _logger.LogInformation("Renave: entrada do veiculo {VeiculoId} registrada (estoque {Estoque})", veiculoId, registro.RnvIdEstoque);
            return ParaSituacao(veiculoId, registro);
        }

        public async Task<RenaveSituacao> EnviarAssinaturaVendedorAsync(int veiculoId, string tipo, byte[] foto, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            var registro = await RegistroEmEstoqueAsync(veiculoId);
            if (foto == null || foto.Length == 0)
                throw new RenaveException("Envie a foto do documento assinado.");

            var base64 = Convert.ToBase64String(foto);
            object corpo = tipo switch
            {
                RenaveTipoAssinatura.CrvPapel => new
                {
                    idEstoque = registro.RnvIdEstoque,
                    envioAssinaturaProprioPunhoAtpvPapelMoeda = new { fotoAtpvPapelMoedaAssinadoBase64 = base64 }
                },
                RenaveTipoAssinatura.AtpveImpresso => new
                {
                    idEstoque = registro.RnvIdEstoque,
                    envioAssinaturaProprioPunhoAtpve = new { fotoAtpveAssinadoDeProprioPunhoBase64 = base64 }
                },
                _ => throw new RenaveException("Tipo de assinatura inválido.")
            };

            using (await ChamarComRegistroAsync(ctx, registro, HttpMethod.Post, "/api/atpv-assinatura-vendedor", corpo, ct)) { }
            registro.RegistrarAssinaturaVendedor();
            await _registros.SalvarAsync(registro);
            return ParaSituacao(veiculoId, registro);
        }

        public async Task<RenaveSituacao> EnviarNotaFiscalAsync(int veiculoId, string evento, string chave, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            var registro = await _registros.GetByVeiculoAsync(veiculoId);
            if (registro?.RnvIdEstoque == null)
                throw new RenaveException("Registre a entrada no Renave antes de informar a nota.");

            var ev = (evento ?? "").Trim().ToUpperInvariant();
            if (ev != "COMPRA" && ev != "VENDA")
                throw new RenaveException("Evento da nota inválido (COMPRA ou VENDA).");
            if (ev == "VENDA" && registro.RnvDtSaida == null)
                throw new RenaveException("Registre a saída no Renave antes de informar a nota de venda.");
            var chaveLimpa = SoDigitos(chave);
            if (!ChaveNfeValida(chaveLimpa))
                throw new RenaveException("Chave da NF-e inválida (44 dígitos).");

            var corpo = new { idEstoque = registro.RnvIdEstoque, evento = ev, chaveNotaFiscal = chaveLimpa };
            using (await ChamarComRegistroAsync(ctx, registro, HttpMethod.Post, "/api/notas-fiscais", corpo, ct)) { }
            if (ev == "COMPRA") registro.RegistrarNotaEntrada(chaveLimpa);
            else registro.RegistrarNotaSaida(chaveLimpa);
            await _registros.SalvarAsync(registro);
            return ParaSituacao(veiculoId, registro);
        }

        public async Task<RenaveSituacao> SolicitarSaidaAsync(int veiculoId, RenaveSaidaInput input, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: true, ct);
            var registro = await RegistroEmEstoqueAsync(veiculoId);
            var (placa, renavam) = await PlacaRenavamAsync(veiculoId);

            var comprador = SoDigitos(input.DocumentoComprador);
            if (!DocumentoValido(comprador))
                throw new RenaveException("CPF/CNPJ do comprador inválido.");
            if (input.CodigoMunicipio <= 0)
                throw new RenaveException("Escolha o município do comprador.");
            if (input.ValorVenda <= 0)
                throw new RenaveException("Informe o valor de venda.");
            var codigo = SoDigitos(input.CodigoSegurancaCrv);
            if (codigo.Length == 0)
                throw new RenaveException("Informe o código de segurança do CRV atual (emitido em nome da loja).");

            var corpo = new
            {
                dataVenda = Data(input.DataVenda),
                valorVenda = input.ValorVenda,
                emailEstabelecimento = ctx.Config.EmailLoja,
                cpfOperadorResponsavel = ctx.Config.OperadorCpf,
                comprador = new
                {
                    numeroDocumento = comprador,
                    tipoDocumento = comprador.Length == 14 ? "CNPJ" : "CPF",
                    nome = Vazio(input.NomeComprador?.Trim()),
                    email = Vazio(input.EmailComprador?.Trim()),
                    endereco = new
                    {
                        codigoMunicipio = input.CodigoMunicipio,
                        cep = Vazio(SoDigitos(input.Cep)),
                        logradouro = Vazio(input.Logradouro?.Trim()),
                        numero = Vazio(input.Numero?.Trim()),
                        complemento = Vazio(input.Complemento?.Trim()),
                        bairro = Vazio(input.Bairro?.Trim())
                    }
                },
                veiculo = new
                {
                    placa,
                    renavam,
                    numeroCrv = Vazio(SoDigitos(input.NumeroCrv)),
                    codigoSegurancaCrv = codigo
                }
            };

            using var doc = await ChamarComRegistroAsync(ctx, registro, HttpMethod.Post, "/api/solicitacoes-saida-estoque", corpo, ct);
            registro.RegistrarSaida(Texto(doc.RootElement, "estado") ?? registro.RnvEstado ?? "",
                input.DataVenda.Date, input.ValorVenda, comprador, Vazio(input.NomeComprador?.Trim()));
            await _registros.SalvarAsync(registro);
            _logger.LogInformation("Renave: saida do veiculo {VeiculoId} registrada (estoque {Estoque})", veiculoId, registro.RnvIdEstoque);
            return ParaSituacao(veiculoId, registro);
        }

        public async Task<(byte[] Pdf, string Numero)> ObterAtpvAsync(int veiculoId, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            var registro = await _registros.GetByVeiculoAsync(veiculoId);
            if (registro?.RnvDtSaida == null)
                throw new RenaveException("O ATPVe só existe depois da saída registrada no Renave.");
            var (placa, renavam) = await PlacaRenavamAsync(veiculoId);

            using var doc = await ChamarComRegistroAsync(ctx, registro, HttpMethod.Get, $"/api/pdf-atpv/{placa}/{renavam}", null, ct);
            var numero = Texto(doc.RootElement, "numeroAtpv") ?? "";
            var pdf = Texto(doc.RootElement, "pdfAtpvBase64");
            if (string.IsNullOrEmpty(pdf))
                throw new RenaveException("O Renave não devolveu o PDF do ATPVe.");
            if (numero.Length > 0 && numero != registro.RnvNumeroAtpv)
            {
                registro.RegistrarAtpv(numero);
                await _registros.SalvarAsync(registro);
            }
            return (Convert.FromBase64String(pdf), numero);
        }

        // ================================================================
        // Homologacao
        // ================================================================

        public async Task<RenaveVeiculoTeste> CriarVeiculoTesteAsync(CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            ExigirHomologacao(ctx);
            var dono = CpfDeTeste();
            var corpo = new
            {
                documentoProprietario = dono,
                nomeProprietario = "Vendedor de Teste",
                ufJurisdicaoVeiculo = "SP",
                sufixoPlaca = RandomNumberGenerator.GetInt32(1000, 10000).ToString(CultureInfo.InvariantCulture),
                comRouboFurto = false,
                jaBaixado = false
            };
            using var doc = await ChamarAsync(ctx, HttpMethod.Post, "/api/veiculos-usados-mock", corpo, ct);
            var r = doc!.RootElement;
            return new RenaveVeiculoTeste
            {
                Placa = Texto(r, "placa") ?? "",
                Renavam = Texto(r, "renavam") ?? "",
                Chassi = Texto(r, "chassi") ?? "",
                NumeroCrv = Texto(r, "numeroCrv") ?? "",
                CodigoSegurancaCrv = Texto(r, "codigoSegurancaCrv") ?? "",
                DocumentoProprietario = dono
            };
        }

        public async Task<RenaveSituacao> SimularTransferenciaDetranAsync(int veiculoId, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            ExigirHomologacao(ctx);
            var registro = await RegistroEmEstoqueAsync(veiculoId);
            var (placa, _) = await PlacaRenavamAsync(veiculoId);
            using (await ChamarComRegistroAsync(ctx, registro, HttpMethod.Post, "/api/veiculos-usados-mock/transferencia-no-detran", new { placa }, ct)) { }
            return await AtualizarSituacaoAsync(veiculoId, ct);
        }

        // ================================================================
        // HTTP
        // ================================================================

        private sealed record Contexto(RenaveConfigInfo Config, string BaseUrl, byte[]? Pfx, string? Senha);

        private async Task<Contexto> ContextoAsync(bool exigirOperador, CancellationToken ct)
        {
            var config = await ObterConfigAsync(ct);
            if (exigirOperador && (config.OperadorCpf == null || config.EmailLoja == null))
                throw new RenaveException("Configure o Renave em Integrações: CPF do operador responsável e e-mail da loja.");

            byte[]? pfx = null;
            string? senha = null;
            var pfxCifrado = await _config.GetValorAsync(KeyCertPfx);
            if (!string.IsNullOrEmpty(pfxCifrado))
            {
                try
                {
                    pfx = Convert.FromBase64String(_protector.Unprotect(pfxCifrado));
                    senha = _protector.Unprotect(await _config.GetValorAsync(KeyCertSenha) ?? "");
                }
                catch (CryptographicException)
                {
                    throw new RenaveException("Não foi possível ler o certificado guardado. Envie o certificado A1 de novo em Integrações → Renave.");
                }
            }

            if (config.Ambiente == RenaveAmbiente.Producao)
            {
                if (pfx == null)
                    throw new RenaveException("Envie o certificado A1 da loja em Integrações → Renave para operar em produção.");
                if (config.CertificadoValidade <= DateTime.UtcNow)
                    throw new RenaveException("O certificado A1 da loja venceu. Envie o certificado novo em Integrações → Renave.");
            }

            var baseUrl = (config.Ambiente == RenaveAmbiente.Producao ? _settings.UrlProducao : _settings.UrlHomologacao).TrimEnd('/');
            return new Contexto(config, baseUrl, pfx, senha);
        }

        private async Task<JsonDocument> ChamarComRegistroAsync(Contexto ctx, VeiculoRenave registro,
            HttpMethod metodo, string caminho, object? corpo, CancellationToken ct)
        {
            try
            {
                return (await ChamarAsync(ctx, metodo, caminho, corpo, ct))!;
            }
            catch (RenaveException ex)
            {
                // Guarda a recusa no veiculo: a tela mostra o motivo depois.
                if (registro.RnvId != 0 || registro.RnvIdEstoque != null)
                {
                    registro.RegistrarErro(ex.Message);
                    await _registros.SalvarAsync(registro);
                }
                throw;
            }
        }

        private async Task<JsonDocument?> ChamarAsync(Contexto ctx, HttpMethod metodo, string caminho, object? corpo, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(metodo, ctx.BaseUrl + caminho);
            request.Headers.Accept.ParseAdd("application/json");
            if (ctx.Config.CnpjEstabelecimento != null)
                request.Headers.TryAddWithoutValidation("CNPJ-Estabelecimento", ctx.Config.CnpjEstabelecimento);
            if (corpo != null)
                request.Content = new StringContent(JsonSerializer.Serialize(corpo, Json), Encoding.UTF8, "application/json");

            X509Certificate2? cert = null;
            HttpResponseMessage response;
            try
            {
                if (ctx.Pfx != null) cert = CarregarCertificado(ctx.Pfx, ctx.Senha ?? "");
                response = await _transporte.EnviarAsync(request, cert, ct);
            }
            catch (CryptographicException)
            {
                throw new RenaveException("O certificado A1 guardado não abre mais. Envie o certificado de novo em Integrações → Renave.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Renave: falha de comunicacao em {Metodo} {Caminho}", metodo, caminho);
                throw new RenaveException("Não foi possível falar com o Renave agora. Tente de novo em alguns minutos.");
            }
            finally
            {
                cert?.Dispose();
            }

            using (response)
            {
                var texto = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                    return string.IsNullOrWhiteSpace(texto) ? null : JsonDocument.Parse(texto);

                var status = (int)response.StatusCode;
                _logger.LogWarning("Renave recusou {Metodo} {Caminho}: HTTP {Status} {Corpo}", metodo, caminho, status,
                    texto.Length > 500 ? texto[..500] : texto);
                throw new RenaveException(MensagemDeErro(response.StatusCode, texto), status);
            }
        }

        /// <summary>
        /// Texto para a tela a partir da resposta de erro do Renave
        /// ({titulo, detalhe, mensagemParaUsuarioFinal}).
        /// </summary>
        public static string MensagemDeErro(HttpStatusCode status, string corpo)
        {
            if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return "O Renave não reconheceu o certificado da loja. Confira se ele está cadastrado no Credencia (Senatran) e habilitado no Serpro.";

            string? paraUsuario = null, detalhe = null, rastreio = null;
            try
            {
                using var doc = JsonDocument.Parse(corpo);
                var r = doc.RootElement;
                paraUsuario = Texto(r, "mensagemParaUsuarioFinal");
                detalhe = Texto(r, "detalhe");
                rastreio = Texto(r, "logIdRastreabilidade");
            }
            catch (JsonException) { }

            if ((int)status >= 500)
            {
                var msg = "O Renave está com instabilidade. Tente de novo em alguns minutos.";
                return rastreio != null ? $"{msg} (protocolo {rastreio})" : msg;
            }
            return Vazio(paraUsuario) ?? Vazio(detalhe) ?? $"O Renave recusou a operação (HTTP {(int)status}).";
        }

        // ================================================================
        // Auxiliares
        // ================================================================

        private async Task<(string Placa, string Renavam)> PlacaRenavamAsync(int veiculoId)
        {
            var v = await _veiculos.GetByIdAsync(veiculoId)
                ?? throw new RenaveException("Veículo não encontrado.");
            var placa = new string((v.VeiPlaca ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            var renavam = SoDigitos(v.VeiRenavam);
            if (placa.Length != 7)
                throw new RenaveException("Cadastre a placa do veículo antes de usar o Renave.");
            if (renavam.Length == 0)
                throw new RenaveException("Cadastre o Renavam do veículo antes de usar o Renave (está no CRV/CRLV).");
            // Renavam antigo tem 9 digitos; o Renave usa 11, com zeros a esquerda.
            return (placa, renavam.PadLeft(11, '0'));
        }

        private async Task<VeiculoRenave> RegistroEmEstoqueAsync(int veiculoId)
        {
            var registro = await _registros.GetByVeiculoAsync(veiculoId);
            if (registro == null || !registro.EmEstoque)
                throw new RenaveException("Este veículo não está no estoque do Renave. Registre a entrada primeiro.");
            return registro;
        }

        private static void ExigirHomologacao(Contexto ctx)
        {
            if (ctx.Config.Ambiente != RenaveAmbiente.Homologacao)
                throw new RenaveException("Disponível só no ambiente de testes (homologação).");
        }

        public static RenaveSituacao ParaSituacao(int veiculoId, VeiculoRenave? r)
        {
            var s = new RenaveSituacao { VeiculoId = veiculoId };
            if (r == null)
            {
                s.ProximoPasso = "Registrar a entrada no Renave";
                return s;
            }
            s.IdEstoque = r.RnvIdEstoque;
            s.Estado = r.RnvEstado;
            s.DataEntrada = r.RnvDtEntrada;
            s.ValorCompra = r.RnvValorCompra;
            s.DataAssinaturaVendedor = r.RnvDtAssinaturaVendedor;
            s.ChaveNfEntrada = r.RnvChaveNfEntrada;
            s.DataSaida = r.RnvDtSaida;
            s.ValorVenda = r.RnvValorVenda;
            s.CompradorNome = r.RnvCompradorNome;
            s.ChaveNfSaida = r.RnvChaveNfSaida;
            s.NumeroAtpv = r.RnvNumeroAtpv;
            s.UltimoErro = r.RnvUltimoErro;
            s.AtualizadoEm = r.RnvAtualizadoEm;
            s.ProximoPasso = ProximoPasso(r);
            return s;
        }

        private static string ProximoPasso(VeiculoRenave r)
        {
            if (r.RnvIdEstoque == null) return "Registrar a entrada no Renave";
            if (r.RnvDtSaida == null)
            {
                if (r.RnvDtAssinaturaVendedor == null) return "Enviar a assinatura do vendedor (ATPV)";
                if (r.RnvChaveNfEntrada == null) return "Informar a chave da NF-e de compra";
                if (r.RnvEstado != "CONFIRMADO") return "Aguardando o Detran confirmar a entrada";
                return "Em estoque no Renave: registrar a saída ao vender";
            }
            if (r.RnvChaveNfSaida == null) return "Informar a chave da NF-e de venda";
            return "Concluído";
        }

        private static X509Certificate2 CarregarCertificado(byte[] pfx, string senha)
        {
            // Linux (servidor): chave so em memoria. Windows nao aceita chave
            // efemera no TLS do cliente, entao la usa o armazenamento padrao.
            var flags = OperatingSystem.IsWindows() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;
            return new X509Certificate2(pfx, senha, flags);
        }

        private static string TipoCrv(string? tipo)
        {
            var t = (tipo ?? "").Trim().ToUpperInvariant();
            return t is "AZUL" or "VERDE" or "BRANCO" or "DIGITAL"
                ? t
                : throw new RenaveException("Tipo de CRV inválido (AZUL, VERDE, BRANCO ou DIGITAL).");
        }

        private static DateTime AgoraBrasilia() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Brasilia);

        // O Renave-WS (Java LocalDate/LocalDateTime) recusa data com fuso.
        public static string Data(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public static string DataHora(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

        public static string SoDigitos(string? valor) => new((valor ?? "").Where(char.IsDigit).ToArray());

        private static string? Vazio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;

        private static string? Texto(JsonElement e, string nome)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var p)
                ? p.ValueKind switch
                {
                    JsonValueKind.String => p.GetString(),
                    JsonValueKind.Number => p.GetRawText(),
                    _ => null
                }
                : null;

        private static bool Bool(JsonElement e, string nome)
            => e.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.True;

        private static decimal? Decimal(JsonElement e, string nome)
            => e.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : null;

        /// <summary>CPF (11) ou CNPJ (14) com digitos verificadores corretos.</summary>
        public static bool DocumentoValido(string digitos)
        {
            if (digitos.Length is not (11 or 14) || digitos.Distinct().Count() == 1) return false;
            var n = digitos.Select(c => c - '0').ToArray();
            if (n.Length == 11)
            {
                for (var t = 9; t < 11; t++)
                {
                    var s = 0;
                    for (var i = 0; i < t; i++) s += n[i] * (t + 1 - i);
                    var dv = s * 10 % 11 % 10;
                    if (n[t] != dv) return false;
                }
                return true;
            }
            int[] p1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 }, p2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int Dv(int[] pesos) { var s = pesos.Select((p, i) => p * n[i]).Sum() % 11; return s < 2 ? 0 : 11 - s; }
            return n[12] == Dv(p1) && n[13] == Dv(p2);
        }

        /// <summary>Chave de acesso da NF-e: 44 digitos, o ultimo e' o DV (modulo 11).</summary>
        public static bool ChaveNfeValida(string chave)
        {
            if (chave.Length != 44 || !chave.All(char.IsDigit)) return false;
            int soma = 0, peso = 2;
            for (var i = 42; i >= 0; i--)
            {
                soma += (chave[i] - '0') * peso;
                peso = peso == 9 ? 2 : peso + 1;
            }
            var resto = soma % 11;
            var dv = resto < 2 ? 0 : 11 - resto;
            return chave[43] - '0' == dv;
        }

        private static string CpfDeTeste()
        {
            var n = Enumerable.Range(0, 9).Select(_ => RandomNumberGenerator.GetInt32(0, 10)).ToList();
            for (var t = 9; t < 11; t++)
            {
                var s = 0;
                for (var i = 0; i < t; i++) s += n[i] * (t + 1 - i);
                n.Add(s * 10 % 11 % 10);
            }
            return string.Concat(n);
        }

        private static TimeZoneInfo ObterFusoBrasilia()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
        }
    }
}
