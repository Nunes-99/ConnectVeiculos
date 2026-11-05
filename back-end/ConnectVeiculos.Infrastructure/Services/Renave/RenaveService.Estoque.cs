using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Renave;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Renave;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.Renave
{
    /// <summary>
    /// Renave alem do fluxo de um usado: importar o estoque que ja esta no
    /// Renave, zero km vindo da montadora (pendentes, entrada, rejeicao),
    /// entregas indicadas pela montadora, consulta de veiculo e os termos de
    /// entrada/saida. Rotas conforme o swagger do Renave-WS (grupo
    /// "Estabelecimento (Concessionária ou Revenda)").
    /// </summary>
    public partial class RenaveService
    {
        private static readonly string[] EstadosEmEstoque = { "SOLICITADO", "TRANSFERIDO", "CONFIRMADO" };
        private static readonly string[] MotivosRejeicaoZeroKm = { "DESISTENCIA", "ERRO_OPERACIONAL_MONTADORA" };
        private static readonly string[] EstadosEntrega = { "PENDENTE", "REALIZADA", "CANCELADA" };

        // Veiculo cadastrado pela importacao/zero km entra como Reservado: fica
        // fora do catalogo e das redes ate a loja completar marca, modelo e fotos.
        private const string StatusVeiculoNovo = "R";

        // ================================================================
        // Termos de entrada e saida
        // ================================================================

        public async Task<(byte[] Pdf, string Numero)> ObterTermoAsync(int veiculoId, bool saida, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            var registro = await _registros.GetByVeiculoAsync(veiculoId);
            if (registro?.RnvIdEstoque == null)
                throw new RenaveException("Este veículo não tem entrada no Renave.");
            // O Renave so emite termo para usado (e estoque de ITE); zero km recusa.
            if (registro.RnvZeroKm)
                throw new RenaveException("Zero km não tem termo de entrada nem de saída no Renave.");
            if (!saida && registro.RnvDtAssinaturaVendedor == null)
                throw new RenaveException("O termo de entrada sai depois que a assinatura do vendedor (ATPV) é enviada.");
            if (saida && registro.RnvDtSaida == null)
                throw new RenaveException("O termo de saída só existe depois da saída registrada no Renave.");

            var caminho = $"/api/estoques/{registro.RnvIdEstoque}/{(saida ? "termo-saida-estoque" : "termo-entrada-estoque")}";
            using var doc = await ChamarAsync(ctx, HttpMethod.Get, caminho, null, ct);
            var r = doc?.RootElement ?? default;
            var pdf = Texto(r, "pdfBase64");
            if (string.IsNullOrEmpty(pdf))
                throw new RenaveException("O Renave não devolveu o PDF do termo.");
            var numero = Texto(r, saida ? "numeroTermoSaidaEstoque" : "numeroTermoEntradaEstoque") ?? "";
            return (Convert.FromBase64String(pdf), numero);
        }

        // ================================================================
        // Importar estoque
        // ================================================================

        public async Task<RenaveImportacaoResultado> ImportarEstoqueAsync(CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            var resultado = new RenaveImportacaoResultado();

            // Sem filtro de data o Renave devolve so os ultimos 3 meses: pede
            // desde o inicio do Renave e, se ele recusar o periodo, aceita o padrao.
            var estoques = new SortedDictionary<long, JsonElement>();
            var desde = Uri.EscapeDataString(DataHora(new DateTime(2020, 1, 1)));
            var ate = Uri.EscapeDataString(DataHora(AgoraBrasilia()));
            foreach (var estado in EstadosEmEstoque)
            {
                List<JsonElement> lista;
                try
                {
                    lista = await ListarEstoquesAsync(ctx,
                        $"estadoEstoque={estado}&dataHoraRegistroEntradaInicial={desde}&dataHoraRegistroEntradaFinal={ate}", ct);
                }
                // Periodo recusado (4xx): tenta o padrao dele. Lentidao/5xx nao
                // repete: insistir so piora (na homologacao derrubou o servico).
                catch (RenaveException ex) when (ex.StatusHttp is >= 400 and < 500 and not (401 or 403))
                {
                    resultado.SoUltimosTresMeses = true;
                    lista = await ListarEstoquesAsync(ctx, $"estadoEstoque={estado}", ct);
                }
                foreach (var e in lista)
                    if (e.TryGetProperty("id", out var id) && id.TryGetInt64(out var idEstoque))
                        estoques[idEstoque] = e;
            }

            var registros = await _registros.GetTodosAsync();
            var porEstoque = registros.Where(r => r.RnvIdEstoque != null)
                .GroupBy(r => r.RnvIdEstoque!.Value).ToDictionary(g => g.Key, g => g.First().R_VeiId);
            var veiculosComEstoqueAberto = registros.Where(r => r.EmEstoque).Select(r => r.R_VeiId).ToHashSet();
            var veiculos = (await _veiculos.GetAllAsync()).ToList();

            foreach (var (idEstoque, e) in estoques)
            {
                var placa = Vazio(NormalizarPlaca(Texto(e, "placa")));
                var chassi = Vazio(NormalizarChassi(Texto(e, "chassi")));
                var item = new RenaveImportacaoItem
                {
                    IdEstoque = idEstoque,
                    Placa = placa,
                    Chassi = chassi,
                    Estado = Texto(e, "estado"),
                    ZeroKm = placa == null
                };
                resultado.Itens.Add(item);
                resultado.Encontrados++;

                try
                {
                    if (e.TryGetProperty("saidaEstoque", out var saida) && saida.ValueKind == JsonValueKind.Object)
                    {
                        Ignorar(resultado, item, "Já tem saída registrada no Renave.");
                        continue;
                    }
                    if (porEstoque.TryGetValue(idEstoque, out var jaLigado))
                    {
                        item.VeiculoId = jaLigado;
                        item.Acao = RenaveAcaoImportacao.JaVinculado;
                        resultado.JaVinculados++;
                        continue;
                    }

                    var entrada = e.TryGetProperty("entradaEstoque", out var en) && en.ValueKind == JsonValueKind.Object ? en : default;
                    var dataEntrada = LerDataHora(entrada, "dataHora") ?? LerDataHora(entrada, "dataHoraRegistro");
                    var vendedor = entrada.ValueKind == JsonValueKind.Object
                        && entrada.TryGetProperty("vendedor", out var vd) && vd.ValueKind == JsonValueKind.Object
                        ? Vazio(SoDigitos(Texto(vd, "numeroDocumento"))) : null;
                    var chaveNf = Vazio(SoDigitos(Texto(entrada, "chaveNotaFiscalEntrada")));

                    var veiculo = AcharVeiculo(veiculos, placa, chassi);
                    int veiculoId;
                    if (veiculo != null)
                    {
                        if (veiculosComEstoqueAberto.Contains(veiculo.VeiId))
                        {
                            item.VeiculoId = veiculo.VeiId;
                            Ignorar(resultado, item, "O veículo do sistema com esta placa/chassi já está ligado a outro estoque do Renave.");
                            continue;
                        }
                        veiculoId = veiculo.VeiId;
                        item.Acao = RenaveAcaoImportacao.Vinculado;
                        resultado.Vinculados++;
                    }
                    else
                    {
                        int.TryParse(Texto(e, "quilometragemHodometro"), out var km);
                        veiculoId = await CadastrarVeiculoMinimoAsync(placa, chassi, Vazio(SoDigitos(Texto(e, "renavam"))),
                            km, dataEntrada, item.ZeroKm, 0m);
                        item.Acao = RenaveAcaoImportacao.Criado;
                        item.Mensagem = "Cadastrado como Reservado: complete marca, modelo, preço e fotos.";
                        resultado.Criados++;
                    }

                    var registro = await _registros.GetByVeiculoAsync(veiculoId) ?? new VeiculoRenave(veiculoId);
                    registro.RegistrarImportacao(idEstoque, item.Estado ?? "SOLICITADO", dataEntrada, item.ZeroKm, chaveNf, vendedor);
                    await _registros.SalvarAsync(registro);
                    item.VeiculoId = veiculoId;
                    veiculosComEstoqueAberto.Add(veiculoId);
                    porEstoque[idEstoque] = veiculoId;
                }
                catch (RenaveException ex)
                {
                    Ignorar(resultado, item, ex.Message);
                }
            }

            _logger.LogInformation("Renave: importacao do estoque — {Encontrados} no Renave, {Vinculados} vinculados, {Criados} cadastrados, {Ja} ja ligados, {Ignorados} ignorados",
                resultado.Encontrados, resultado.Vinculados, resultado.Criados, resultado.JaVinculados, resultado.Ignorados);
            return resultado;
        }

        private static void Ignorar(RenaveImportacaoResultado resultado, RenaveImportacaoItem item, string motivo)
        {
            // Quem ja tinha sido contado como vinculado/criado volta atras.
            if (item.Acao == RenaveAcaoImportacao.Vinculado) resultado.Vinculados--;
            if (item.Acao == RenaveAcaoImportacao.Criado) resultado.Criados--;
            item.Acao = RenaveAcaoImportacao.Ignorado;
            item.Mensagem = motivo;
            resultado.Ignorados++;
        }

        private async Task<List<JsonElement>> ListarEstoquesAsync(Contexto ctx, string filtro, CancellationToken ct)
        {
            using var doc = await ChamarAsync(ctx, HttpMethod.Get, "/api/estoques?" + filtro, null, ct);
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return new List<JsonElement>();
            return doc.RootElement.EnumerateArray().Select(x => x.Clone()).ToList();
        }

        // ================================================================
        // Consulta de veiculo
        // ================================================================

        public async Task<RenaveConsulta> ConsultarVeiculoAsync(string? placa, string? chassi, string? renavam, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            var p = Vazio(NormalizarPlaca(placa));
            var c = Vazio(NormalizarChassi(chassi));
            var r = Vazio(SoDigitos(renavam));
            if (p == null && c == null && r == null)
                throw new RenaveException("Informe a placa, o chassi ou o Renavam.");
            if (p != null && p.Length != 7)
                throw new RenaveException("Placa inválida.");

            var filtros = new List<string>();
            if (p != null) filtros.Add("placa=" + p);
            if (c != null) filtros.Add("chassi=" + Uri.EscapeDataString(c));
            if (r != null) filtros.Add("renavam=" + r.PadLeft(11, '0'));

            using var doc = await ChamarAsync(ctx, HttpMethod.Get, "/api/veiculos?" + string.Join("&", filtros), null, ct);
            if (doc == null)
                throw new RenaveException("O Renave não encontrou este veículo.");
            var v = doc.RootElement;
            var consulta = new RenaveConsulta
            {
                Placa = Vazio(Texto(v, "placa")),
                Renavam = Vazio(Texto(v, "renavam")),
                Chassi = Vazio(Texto(v, "chassi")),
                Acabado = Bool(v, "veiculoAcabado"),
                DataPreCadastro = LerDataHora(v, "dataPreCadastro")
            };
            if (v.TryGetProperty("restricoes", out var rs) && rs.ValueKind == JsonValueKind.Array)
                consulta.Restricoes = rs.EnumerateArray().Select(x => Texto(x, "tipo") ?? "").Where(x => x.Length > 0).ToList();

            // Estoques deste veiculo na loja (o Renave so mostra os do proprio estabelecimento).
            var filtroEstoque = consulta.Placa != null ? "placa=" + NormalizarPlaca(consulta.Placa)
                : consulta.Chassi != null ? "chassi=" + Uri.EscapeDataString(NormalizarChassi(consulta.Chassi)) : null;
            if (filtroEstoque != null)
            {
                try
                {
                    foreach (var e in await ListarEstoquesAsync(ctx, filtroEstoque, ct))
                    {
                        if (!(e.TryGetProperty("id", out var id) && id.TryGetInt64(out var idEstoque))) continue;
                        var saida = e.TryGetProperty("saidaEstoque", out var s) && s.ValueKind == JsonValueKind.Object ? s : default;
                        var entrada = e.TryGetProperty("entradaEstoque", out var en) && en.ValueKind == JsonValueKind.Object ? en : default;
                        consulta.Estoques.Add(new RenaveEstoqueResumo
                        {
                            Id = idEstoque,
                            Estado = Texto(e, "estado"),
                            DataEntrada = LerDataHora(entrada, "dataHora"),
                            DataSaida = LerDataHora(saida, "dataHora"),
                            MotivoSaida = Texto(saida, "motivo")
                        });
                    }
                }
                catch (RenaveException ex)
                {
                    // A consulta do veiculo ja respondeu; sem o estoque a tela ainda serve.
                    _logger.LogInformation("Renave: consulta de estoque falhou na consulta de veiculo: {Erro}", ex.Message);
                }
            }

            var veiculos = (await _veiculos.GetAllAsync()).ToList();
            consulta.VeiculoId = AcharVeiculo(veiculos, Vazio(NormalizarPlaca(consulta.Placa)), Vazio(NormalizarChassi(consulta.Chassi)))?.VeiId;
            return consulta;
        }

        // ================================================================
        // Zero km
        // ================================================================

        public async Task<IReadOnlyList<RenaveZeroKmPendente>> ListarZeroKmPendentesAsync(CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            using var doc = await ChamarAsync(ctx, HttpMethod.Get, "/api/veiculos-zero-km-pendentes-entrada-estoque", null, ct);
            var lista = new List<RenaveZeroKmPendente>();
            if (doc == null || doc.RootElement.ValueKind != JsonValueKind.Array) return lista;

            var veiculos = (await _veiculos.GetAllAsync()).ToList();
            foreach (var x in doc.RootElement.EnumerateArray())
            {
                var chassi = NormalizarChassi(Texto(x, "chassi"));
                if (chassi.Length == 0) continue;
                var v = AcharVeiculo(veiculos, null, chassi);
                lista.Add(new RenaveZeroKmPendente
                {
                    Chassi = chassi,
                    DataHoraCadastro = LerDataHora(x, "dataHoraCadastro"),
                    Acabado = Bool(x, "veiculoAcabado"),
                    VeiculoId = v?.VeiId,
                    VeiculoDescricao = v == null ? null : $"{v.VeiMarca} {v.VeiModelo} {v.VeiAno}".Trim()
                });
            }
            // O Renave nao garante ordem: o mais recente primeiro.
            return lista.OrderByDescending(p => p.DataHoraCadastro ?? DateTime.MinValue).ToList();
        }

        public async Task<RenaveSituacao> EntradaZeroKmAsync(RenaveEntradaZeroKmInput input, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: true, ct);
            var chassi = NormalizarChassi(input.Chassi);
            if (chassi.Length == 0)
                throw new RenaveException("Informe o chassi do zero km.");
            if (input.ValorCompra <= 0)
                throw new RenaveException("Informe o valor de compra (o da nota da montadora).");
            if (input.QuilometragemHodometro < 0)
                throw new RenaveException("Quilometragem inválida.");
            // O swagger marca como opcional, mas o Renave recusa sem ela
            // ("Chave da Nota fiscal é obrigatório", homologacao 04/11/2026).
            var chave = SoDigitos(input.ChaveNotaFiscal);
            if (chave.Length == 0)
                throw new RenaveException("Informe a chave da NF-e de compra (a nota da montadora).");
            if (!ChaveNfeValida(chave))
                throw new RenaveException("Chave da NF-e inválida (44 dígitos).");

            // Qual veiculo do sistema recebe a entrada: o escolhido, o do mesmo
            // chassi, ou um novo com os dados minimos (cadastrado so depois que o
            // Renave aceitar, para nao sobrar veiculo se ele recusar).
            var veiculos = (await _veiculos.GetAllAsync()).ToList();
            Veiculo? veiculo = null;
            if (input.VeiculoId is > 0)
            {
                veiculo = veiculos.FirstOrDefault(v => v.VeiId == input.VeiculoId)
                    ?? throw new RenaveException("Veículo não encontrado.");
            }
            veiculo ??= AcharVeiculo(veiculos, null, chassi);
            if (veiculo != null)
            {
                var existente = await _registros.GetByVeiculoAsync(veiculo.VeiId);
                if (existente?.EmEstoque == true)
                    throw new RenaveException("Este veículo do sistema já está no estoque do Renave.");
            }

            var corpo = new
            {
                chassi,
                chaveNotaFiscal = chave,
                cpfOperadorResponsavel = ctx.Config.OperadorCpf,
                dataEntradaEstoque = Data(input.DataEntrada),
                dataHoraMedicaoHodometro = DataHora(AgoraBrasilia()),
                quilometragemHodometro = input.QuilometragemHodometro,
                valorCompra = input.ValorCompra
            };
            using var doc = await ChamarAsync(ctx, HttpMethod.Post, "/api/entradas-estoque-zero-km", corpo, ct);
            var r = doc?.RootElement ?? default;
            if (!(r.ValueKind == JsonValueKind.Object && r.TryGetProperty("id", out var id) && id.TryGetInt64(out var idEstoque)))
                throw new RenaveException("O Renave não devolveu o número do estoque. Confira em Consultar veículo antes de tentar de novo.");

            var veiculoId = veiculo?.VeiId
                ?? await CadastrarVeiculoMinimoAsync(null, chassi, null, input.QuilometragemHodometro, input.DataEntrada, true, input.ValorCompra);
            var registro = await _registros.GetByVeiculoAsync(veiculoId) ?? new VeiculoRenave(veiculoId);
            registro.RegistrarEntradaZeroKm(idEstoque, Texto(r, "estado") ?? "CONFIRMADO", input.DataEntrada.Date, input.ValorCompra, chave);
            await _registros.SalvarAsync(registro);
            _logger.LogInformation("Renave: entrada do zero km {Chassi} no veiculo {VeiculoId} (estoque {Estoque})", chassi, veiculoId, idEstoque);
            return ParaSituacao(veiculoId, registro);
        }

        public async Task RejeitarZeroKmAsync(RenaveRejeicaoZeroKmInput input, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: true, ct);
            var chassi = NormalizarChassi(input.Chassi);
            if (chassi.Length == 0)
                throw new RenaveException("Informe o chassi do zero km.");
            var motivo = (input.Motivo ?? "").Trim().ToUpperInvariant();
            if (!MotivosRejeicaoZeroKm.Contains(motivo))
                throw new RenaveException("Motivo inválido (desistência ou erro operacional da montadora).");

            var corpo = new { chassi, cpfOperadorResponsavel = ctx.Config.OperadorCpf, motivoRejeicao = motivo };
            using (await ChamarAsync(ctx, HttpMethod.Post, "/api/rejeicoes-zero-km", corpo, ct)) { }
            _logger.LogInformation("Renave: zero km {Chassi} rejeitado ({Motivo})", chassi, motivo);
        }

        // ================================================================
        // Entregas indicadas pela montadora
        // ================================================================

        public async Task<IReadOnlyList<RenaveEntrega>> ListarEntregasAsync(string? estado, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            var filtro = (estado ?? "").Trim().ToUpperInvariant();
            if (filtro.Length > 0 && !EstadosEntrega.Contains(filtro))
                throw new RenaveException("Situação de entrega inválida.");

            // O Renave exige ao menos um filtro ("Deve ser informado pelo menos um
            // parâmetro de pesquisa"): "todas" vira uma consulta por situacao.
            var lista = new List<RenaveEntrega>();
            foreach (var e in filtro.Length > 0 ? new[] { filtro } : EstadosEntrega)
            {
                using var doc = await ChamarAsync(ctx, HttpMethod.Get, "/api/entregas-veiculo-zero-km?estado=" + e, null, ct);
                if (doc != null && doc.RootElement.ValueKind == JsonValueKind.Array)
                    lista.AddRange(doc.RootElement.EnumerateArray().Select(ParaEntrega));
            }
            return lista.GroupBy(e => e.Id).Select(g => g.First()).OrderByDescending(e => e.Id).ToList();
        }

        public Task<RenaveEntrega> RealizarEntregaAsync(long idEntrega, CancellationToken ct = default)
            => AcaoEntregaAsync("/api/entregas-veiculo-zero-km/realizacao", idEntrega, ct);

        public Task<RenaveEntrega> CancelarEntregaAsync(long idEntrega, CancellationToken ct = default)
            => AcaoEntregaAsync("/api/entregas-veiculo-zero-km/cancelamento", idEntrega, ct);

        private async Task<RenaveEntrega> AcaoEntregaAsync(string caminho, long idEntrega, CancellationToken ct)
        {
            var ctx = await ContextoAsync(exigirOperador: true, ct);
            if (idEntrega <= 0) throw new RenaveException("Entrega inválida.");
            using var doc = await ChamarAsync(ctx, HttpMethod.Post, caminho,
                new { idEntrega, cpfOperadorResponsavel = ctx.Config.OperadorCpf }, ct);
            return doc == null ? new RenaveEntrega { Id = idEntrega } : ParaEntrega(doc.RootElement);
        }

        private static RenaveEntrega ParaEntrega(JsonElement e)
        {
            var realizacao = e.TryGetProperty("realizacaoEntrega", out var re) && re.ValueKind == JsonValueKind.Object ? re : default;
            var cancelamento = e.TryGetProperty("cancelamentoEntrega", out var ce) && ce.ValueKind == JsonValueKind.Object ? ce : default;
            return new RenaveEntrega
            {
                Id = e.TryGetProperty("id", out var id) && id.TryGetInt64(out var n) ? n : 0,
                Chassi = Vazio(Texto(e, "chassi")),
                Estado = Vazio(Texto(e, "estado")),
                CnpjEntregador = Vazio(Texto(e, "cnpjEstabelecimentoEntregador")),
                CnpjVendedor = Vazio(Texto(e, "cnpjEstabelecimentoVendedor")),
                IdAtpv = Vazio(Texto(e, "idAtpv")),
                DataRealizacao = LerDataHora(realizacao, "dataHoraRegistro"),
                DataCancelamento = LerDataHora(cancelamento, "dataHoraRegistro"),
                MotivoCancelamento = Vazio(Texto(cancelamento, "motivo"))
            };
        }

        // ================================================================
        // Homologacao: zero km ficticio
        // ================================================================

        public async Task<RenaveZeroKmTeste> CriarZeroKmTesteAsync(bool acabado, CancellationToken ct = default)
        {
            var ctx = await ContextoAsync(exigirOperador: false, ct);
            ExigirHomologacao(ctx);
            var sufixo = RandomNumberGenerator.GetInt32(1000, 10000).ToString(CultureInfo.InvariantCulture);
            var caminho = "/api/veiculos-zero-km-mock" + (acabado ? "" : "?veiculoInacabado=true");
            using var doc = await ChamarAsync(ctx, HttpMethod.Post, caminho, new { sufixoChassi = sufixo }, ct);
            var chassi = Texto(doc?.RootElement ?? default, "chassi");
            if (string.IsNullOrEmpty(chassi))
                throw new RenaveException("O Renave não devolveu o chassi do zero km de teste.");
            return new RenaveZeroKmTeste { Chassi = chassi, Acabado = acabado };
        }

        // ================================================================
        // Auxiliares
        // ================================================================

        /// <summary>
        /// Cadastra o veiculo com o que o Renave sabe (placa, chassi, Renavam,
        /// km). Entra como Reservado: nao aparece no catalogo nem vai para as
        /// redes ate a loja completar o cadastro e marcar Disponivel.
        /// </summary>
        private async Task<int> CadastrarVeiculoMinimoAsync(string? placa, string? chassi, string? renavam, int km,
            DateTime? dataEntrada, bool zeroKm, decimal precoCompra)
        {
            if (_lojas == null || _categorias == null)
                throw new RenaveException("Cadastro automático de veículo indisponível.");
            var loja = (await _lojas.GetAllAsync()).OrderBy(l => l.LojId).FirstOrDefault()
                ?? throw new RenaveException("Cadastre a loja antes de importar veículos do Renave.");
            var categoria = (await _categorias.GetAllAsync()).OrderBy(c => c.CatId).FirstOrDefault()
                ?? throw new RenaveException("Cadastre ao menos uma categoria antes de importar veículos do Renave.");

            var data = dataEntrada ?? AgoraBrasilia();
            var veiculo = new Veiculo(0, loja.LojId, categoria.CatId,
                veiMarca: "",
                veiModelo: zeroKm ? "Zero km (Renave)" : "Importado do Renave",
                veiAno: (short)data.Year,
                veiPlaca: placa ?? "",
                veiChassi: chassi ?? "",
                veiCor: "",
                veiKm: Math.Max(km, 0),
                veiPreco: 0m,
                veiDtEntrada: data,
                veiSts: StatusVeiculoNovo,
                veiSitSts: "A",
                veiPrecoCompra: precoCompra,
                veiObservacao: "Cadastrado a partir do estoque do Renave. Complete marca, modelo, ano, cor, preço e fotos e mude para Disponível.",
                veiRenavam: renavam);
            var id = await _veiculos.CreateAsync(veiculo);
            _logger.LogInformation("Renave: veiculo {VeiculoId} cadastrado a partir do Renave (placa {Placa}, chassi {Chassi})", id, placa, chassi);
            return id;
        }

        private static Veiculo? AcharVeiculo(IEnumerable<Veiculo> veiculos, string? placa, string? chassi)
            => veiculos.FirstOrDefault(v => placa != null && NormalizarPlaca(v.VeiPlaca) == placa)
               ?? veiculos.FirstOrDefault(v => chassi != null && NormalizarChassi(v.VeiChassi) == chassi);

        public static string NormalizarPlaca(string? placa)
            => new string((placa ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        public static string NormalizarChassi(string? chassi)
            => new string((chassi ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        /// <summary>Data ou data-hora do Renave (sem fuso, horario de Brasilia).</summary>
        private static DateTime? LerDataHora(JsonElement e, string nome)
        {
            var texto = e.ValueKind == JsonValueKind.Object ? Texto(e, nome) : null;
            return DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        }
    }
}
