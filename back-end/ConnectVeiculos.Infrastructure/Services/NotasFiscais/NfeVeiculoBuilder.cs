using System.Globalization;
using System.Text;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.NotasFiscais;
using Unimake.Business.DFe.Servicos;
using Unimake.Business.DFe.Xml.NFe;

namespace ConnectVeiculos.Infrastructure.Services.NotasFiscais
{
    /// <summary>
    /// Monta a NF-e (layout 4.00) de um veiculo: um item so, o carro, com a
    /// outra parte da operacao como destinatario (vendedor na compra, comprador
    /// na venda). Os impostos seguem as regras configuradas pela loja.
    /// </summary>
    public static class NfeVeiculoBuilder
    {
        public const string NomeHomologacao = "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";

        public static EnviNFe Montar(NfeConfigInfo cfg, NfeEmissaoInput dados, Veiculo veiculo,
            int serie, int numero, int codigoNumerico, DateTime dataEmissao, bool homologacao)
        {
            var devolucao = string.Equals(dados.Tipo, "DEVOLUCAO", StringComparison.OrdinalIgnoreCase);
            // Devolucao de venda e' uma entrada (o carro volta para a loja).
            var entrada = devolucao || string.Equals(dados.Tipo, "ENTRADA", StringComparison.OrdinalIgnoreCase);
            var regra = devolucao ? (cfg.Devolucao ?? RegraDevolucaoPadrao(cfg.Crt)) : entrada ? cfg.Compra : cfg.Venda;
            var chaveReferenciada = SoDigitos(dados.ChaveReferenciada);
            if (devolucao && chaveReferenciada.Length != 44)
                throw new NfeException("Devolução precisa da chave (44 dígitos) da nota de venda devolvida.");
            var ufEmit = Uf(cfg.Uf);
            var ufDest = Uf(dados.Endereco.Uf);
            var interna = ufEmit == ufDest;
            var valor = Math.Round(dados.Valor, 2);
            var documento = SoDigitos(dados.Documento);
            var indIe = IndicadorIe(dados);
            // Sem inscricao (isento ou nao contribuinte) o cliente e' consumidor final.
            var consumidorFinal = indIe != IndicadorIEDestinatario.ContribuinteICMS;

            var (icms, vBc, vIcms) = MontarIcms(cfg.Crt, regra, valor);
            var (pis, vPis) = MontarPis(regra.CstPis, regra.AliquotaPis, valor);
            var (cofins, vCofins) = MontarCofins(regra.CstCofins, regra.AliquotaCofins, valor);
            var cest = Cest(regra, cfg, dados);
            var difal = entrada || interna || indIe != IndicadorIEDestinatario.NaoContribuinte || cfg.Crt is 1 or 4
                ? null
                : MontarDifal(dados, ufEmit, ufDest, vBc);

            // A base do IBS/CBS, na transicao, desconta os tributos atuais.
            var deducoes = vIcms + vPis + vCofins + (decimal)(difal?.VICMSUFDest ?? 0) + (decimal)(difal?.VFCPUFDest ?? 0);
            (IBSCBS Grupo, IBSCBSTot Total)? ibsCbs = cfg.IbsCbsAtivo ? MontarIbsCbs(cfg, regra, valor, deducoes) : null;

            var infNfe = new InfNFe
            {
                Versao = "4.00",
                Ide = new Ide
                {
                    CUF = ufEmit,
                    CNF = codigoNumerico.ToString("D8", CultureInfo.InvariantCulture),
                    NatOp = Limitar(regra.NaturezaOperacao, 60),
                    Mod = ModeloDFe.NFe,
                    Serie = serie,
                    NNF = numero,
                    DhEmi = dataEmissao,
                    DhSaiEnt = dataEmissao,
                    TpNF = entrada ? TipoOperacao.Entrada : TipoOperacao.Saida,
                    IdDest = interna ? DestinoOperacao.OperacaoInterna : DestinoOperacao.OperacaoInterestadual,
                    CMunFG = cfg.CodigoMunicipio ?? 0,
                    TpImp = FormatoImpressaoDANFE.NormalRetrato,
                    TpEmis = TipoEmissao.Normal,
                    TpAmb = homologacao ? TipoAmbiente.Homologacao : TipoAmbiente.Producao,
                    FinNFe = devolucao ? FinalidadeNFe.Devolucao : FinalidadeNFe.Normal,
                    NFref = devolucao ? new List<NFref> { new NFref { RefNFe = chaveReferenciada } } : null,
                    IndFinal = consumidorFinal ? SimNao.Sim : SimNao.Nao,
                    IndPres = IndicadorPresenca.OperacaoPresencial,
                    ProcEmi = ProcessoEmissao.AplicativoContribuinte,
                    VerProc = "ConnectVeiculos 1.0"
                },
                Emit = new Emit
                {
                    CNPJ = SoDigitos(cfg.Cnpj),
                    XNome = Limitar(cfg.RazaoSocial, 60),
                    XFant = Vazio(Limitar(cfg.NomeFantasia, 60)),
                    EnderEmit = new EnderEmit
                    {
                        XLgr = Limitar(cfg.Logradouro, 60),
                        Nro = Limitar(cfg.Numero, 60),
                        XCpl = Vazio(Limitar(cfg.Complemento, 60)),
                        XBairro = Limitar(cfg.Bairro, 60),
                        CMun = cfg.CodigoMunicipio ?? 0,
                        XMun = Limitar(cfg.Municipio, 60),
                        UF = ufEmit,
                        CEP = SoDigitos(cfg.Cep),
                        Fone = Vazio(SoDigitos(cfg.Telefone))
                    },
                    IE = SoDigitos(cfg.InscricaoEstadual),
                    CRT = Crt(cfg.Crt)
                },
                Dest = MontarDestinatario(dados, documento, ufDest, indIe, homologacao),
                Det = new List<Det>
                {
                    new Det
                    {
                        NItem = 1,
                        Prod = new Prod
                        {
                            CProd = veiculo.VeiId.ToString(CultureInfo.InvariantCulture),
                            CEAN = "SEM GTIN",
                            XProd = Limitar(DescricaoVeiculo(veiculo), 120),
                            NCM = SoDigitos(string.IsNullOrWhiteSpace(dados.Ncm) ? cfg.NcmPadrao : dados.Ncm),
                            CEST = cest,
                            CFOP = SoDigitos(interna ? regra.CfopInterno : regra.CfopInterestadual),
                            UCom = "UN",
                            QCom = 1m,
                            VUnCom = valor,
                            VProd = (double)valor,
                            CEANTrib = "SEM GTIN",
                            UTrib = "UN",
                            QTrib = 1m,
                            VUnTrib = valor,
                            IndTot = SimNao.Sim,
                            VeicProd = dados.VeiculoNovo == null ? null : MontarVeiculoNovo(dados.VeiculoNovo)
                        },
                        Imposto = new Imposto
                        {
                            ICMS = icms,
                            ICMSUFDest = difal,
                            PIS = pis,
                            COFINS = cofins,
                            IBSCBS = ibsCbs?.Grupo
                        }
                    }
                },
                Total = new Total
                {
                    ICMSTot = new ICMSTot
                    {
                        VBC = (double)vBc,
                        VICMS = (double)vIcms,
                        VICMSUFDest = difal?.VICMSUFDest ?? 0,
                        VFCPUFDest = difal?.VFCPUFDest ?? 0,
                        VICMSUFRemet = 0,
                        VProd = (double)valor,
                        VPIS = (double)vPis,
                        VCOFINS = (double)vCofins,
                        VNF = (double)valor
                    },
                    IBSCBSTot = ibsCbs?.Total
                },
                Transp = new Transp { ModFrete = ModalidadeFrete.SemOcorrenciaTransporte },
                // Devolucao nao tem pagamento (regra da Sefaz para finalidade 4).
                Pag = MontarPagamento(devolucao ? "90" : dados.MeioPagamento, valor),
                InfAdic = MontarInfAdic(cfg, dados, veiculo),
                InfRespTec = MontarRespTec(cfg)
            };

            return new EnviNFe
            {
                Versao = "4.00",
                IdLote = numero.ToString("D15", CultureInfo.InvariantCulture),
                IndSinc = SimNao.Sim,
                NFe = new List<NFe> { new NFe { InfNFeField = infNfe } }
            };
        }

        public static string DescricaoVeiculo(Veiculo v)
        {
            var sb = new StringBuilder();
            sb.Append($"VEICULO {v.VeiMarca} {v.VeiModelo} {v.VeiAno}");
            if (!string.IsNullOrWhiteSpace(v.VeiCor)) sb.Append($" COR {v.VeiCor}");
            if (!string.IsNullOrWhiteSpace(v.VeiPlaca)) sb.Append($" PLACA {v.VeiPlaca}");
            if (!string.IsNullOrWhiteSpace(v.VeiChassi)) sb.Append($" CHASSI {v.VeiChassi}");
            if (!string.IsNullOrWhiteSpace(v.VeiRenavam)) sb.Append($" RENAVAM {v.VeiRenavam}");
            return SemAcento(sb.ToString()).ToUpperInvariant();
        }

        private static Dest MontarDestinatario(NfeEmissaoInput d, string documento, UFBrasil uf, IndicadorIEDestinatario indIe, bool homologacao)
        {
            var dest = new Dest
            {
                // Em homologacao a Sefaz exige este texto no nome do destinatario.
                XNome = homologacao ? NomeHomologacao : Limitar(d.Nome, 60),
                EnderDest = new EnderDest
                {
                    XLgr = Limitar(d.Endereco.Logradouro, 60),
                    Nro = Limitar(string.IsNullOrWhiteSpace(d.Endereco.Numero) ? "S/N" : d.Endereco.Numero, 60),
                    XCpl = Vazio(Limitar(d.Endereco.Complemento, 60)),
                    XBairro = Limitar(d.Endereco.Bairro, 60),
                    CMun = d.Endereco.CodigoMunicipio,
                    XMun = Limitar(d.Endereco.Municipio, 60),
                    UF = uf,
                    CEP = Vazio(SoDigitos(d.Endereco.Cep)),
                    CPais = 1058,
                    XPais = "BRASIL",
                    Fone = Vazio(SoDigitos(d.Telefone))
                },
                IndIEDest = indIe,
                // Isento e nao contribuinte nao levam IE (regra da Sefaz).
                IE = indIe == IndicadorIEDestinatario.ContribuinteICMS ? SoDigitos(d.InscricaoEstadual) : null,
                Email = Vazio(d.Email?.Trim())
            };
            if (documento.Length == 14) dest.CNPJ = documento;
            else dest.CPF = documento;
            return dest;
        }

        /// <summary>ICMS conforme o regime: CSOSN no Simples/MEI, CST no regime normal.</summary>
        public static (ICMS Icms, decimal VBc, decimal VIcms) MontarIcms(int crt, NfeRegraOperacao regra, decimal valor)
        {
            var codigo = (regra.CstIcms ?? "").Trim();
            var simples = crt is 1 or 4;
            var aliquota = regra.AliquotaIcms;
            var reducao = Math.Clamp(regra.ReducaoBaseIcms, 0m, 100m);
            var baseCalculo = Math.Round(valor * (100m - reducao) / 100m, 2);
            var imposto = Math.Round(baseCalculo * aliquota / 100m, 2);

            if (simples)
            {
                return codigo switch
                {
                    "102" or "103" or "300" or "400" =>
                        (new ICMS { ICMSSN102 = new ICMSSN102 { Orig = OrigemMercadoria.Nacional, CSOSN = codigo } }, 0m, 0m),
                    "500" =>
                        (new ICMS { ICMSSN500 = new ICMSSN500 { Orig = OrigemMercadoria.Nacional, CSOSN = codigo } }, 0m, 0m),
                    "900" when aliquota > 0 =>
                        (new ICMS
                        {
                            ICMSSN900 = new ICMSSN900
                            {
                                Orig = OrigemMercadoria.Nacional, CSOSN = codigo, ModBC = ModalidadeBaseCalculoICMS.ValorOperacao,
                                VBC = (double)baseCalculo, PRedBC = (double)reducao, PICMS = (double)aliquota, VICMS = (double)imposto
                            }
                        }, baseCalculo, imposto),
                    "900" =>
                        (new ICMS { ICMSSN900 = new ICMSSN900 { Orig = OrigemMercadoria.Nacional, CSOSN = codigo } }, 0m, 0m),
                    _ => throw new NfeException($"CSOSN do ICMS não suportado: '{codigo}'. Use 102, 103, 300, 400, 500 ou 900.")
                };
            }

            return codigo switch
            {
                "00" => (new ICMS
                {
                    ICMS00 = new ICMS00
                    {
                        Orig = OrigemMercadoria.Nacional, CST = codigo, ModBC = ModalidadeBaseCalculoICMS.ValorOperacao,
                        VBC = (double)valor, PICMS = (double)aliquota, VICMS = (double)Math.Round(valor * aliquota / 100m, 2)
                    }
                }, valor, Math.Round(valor * aliquota / 100m, 2)),
                "20" => (new ICMS
                {
                    ICMS20 = new ICMS20
                    {
                        Orig = OrigemMercadoria.Nacional, CST = codigo, ModBC = ModalidadeBaseCalculoICMS.ValorOperacao,
                        PRedBC = (double)reducao, VBC = (double)baseCalculo, PICMS = (double)aliquota, VICMS = (double)imposto
                    }
                }, baseCalculo, imposto),
                "40" or "41" or "50" =>
                    (new ICMS { ICMS40 = new ICMS40 { Orig = OrigemMercadoria.Nacional, CST = codigo } }, 0m, 0m),
                // ICMS ja cobrado por substituicao tributaria (carro zero km comprado da montadora).
                "60" => (new ICMS { ICMS60 = new ICMS60 { Orig = OrigemMercadoria.Nacional, CST = codigo } }, 0m, 0m),
                "90" when aliquota > 0 => (new ICMS
                {
                    ICMS90 = new ICMS90
                    {
                        Orig = OrigemMercadoria.Nacional, CST = codigo, ModBC = ModalidadeBaseCalculoICMS.ValorOperacao,
                        VBC = (double)baseCalculo, PRedBC = (double)reducao, PICMS = (double)aliquota, VICMS = (double)imposto
                    }
                }, baseCalculo, imposto),
                "90" => (new ICMS { ICMS90 = new ICMS90 { Orig = OrigemMercadoria.Nacional, CST = codigo } }, 0m, 0m),
                _ => throw new NfeException($"CST do ICMS não suportado: '{codigo}'. Use 00, 20, 40, 41, 50, 60 ou 90.")
            };
        }

        private static readonly HashSet<string> CstNaoTributado = new() { "04", "05", "06", "07", "08", "09" };
        private static readonly HashSet<string> CstTributadoAliquota = new() { "01", "02" };

        /// <summary>
        /// PIS conforme o CST: 01/02 tributado pela aliquota (lucro presumido ou
        /// real), 04 a 09 sem incidencia, o resto (49, 99...) no grupo "outras".
        /// </summary>
        public static (PIS Pis, decimal Valor) MontarPis(string cst, decimal aliquota, decimal valor)
        {
            cst = (cst ?? "").Trim();
            if (CstNaoTributado.Contains(cst)) return (new PIS { PISNT = new PISNT { CST = cst } }, 0m);
            if (CstTributadoAliquota.Contains(cst))
            {
                var v = Math.Round(valor * aliquota / 100m, 2);
                return (new PIS { PISAliq = new PISAliq { CST = cst, VBC = (double)valor, PPIS = (double)aliquota, VPIS = (double)v } }, v);
            }
            return (new PIS { PISOutr = new PISOutr { CST = cst, VBC = 0, PPIS = 0, VPIS = 0 } }, 0m);
        }

        public static (COFINS Cofins, decimal Valor) MontarCofins(string cst, decimal aliquota, decimal valor)
        {
            cst = (cst ?? "").Trim();
            if (CstNaoTributado.Contains(cst)) return (new COFINS { COFINSNT = new COFINSNT { CST = cst } }, 0m);
            if (CstTributadoAliquota.Contains(cst))
            {
                var v = Math.Round(valor * aliquota / 100m, 2);
                return (new COFINS { COFINSAliq = new COFINSAliq { CST = cst, VBC = (double)valor, PCOFINS = (double)aliquota, VCOFINS = (double)v } }, v);
            }
            return (new COFINS { COFINSOutr = new COFINSOutr { CST = cst, VBC = 0, PCOFINS = 0, VCOFINS = 0 } }, 0m);
        }

        /// <summary>
        /// Situacao do cliente no ICMS. Vazio decide pela inscricao: com IE e'
        /// contribuinte, sem IE nao contribuinte (o comportamento de antes).
        /// </summary>
        public static IndicadorIEDestinatario IndicadorIe(NfeEmissaoInput d)
        {
            var temIe = !string.IsNullOrWhiteSpace(SoDigitos(d.InscricaoEstadual));
            return (d.IndicadorIe ?? "").Trim().ToLowerInvariant() switch
            {
                "contribuinte" when !temIe => throw new NfeException("Cliente contribuinte do ICMS precisa da inscrição estadual."),
                "contribuinte" => IndicadorIEDestinatario.ContribuinteICMS,
                "isento" => IndicadorIEDestinatario.ContribuinteIsento,
                "nao_contribuinte" => IndicadorIEDestinatario.NaoContribuinte,
                "" => temIe ? IndicadorIEDestinatario.ContribuinteICMS : IndicadorIEDestinatario.NaoContribuinte,
                var x => throw new NfeException($"Situação do cliente no ICMS inválida: '{x}'.")
            };
        }

        /// <summary>CSTs/CSOSNs de ICMS por substituicao tributaria: com eles a Sefaz exige o CEST.</summary>
        private static readonly HashSet<string> CodigosComSt = new() { "10", "30", "60", "70", "201", "202", "203", "500" };

        private static string? Cest(NfeRegraOperacao regra, NfeConfigInfo cfg, NfeEmissaoInput d)
        {
            var cest = SoDigitos(string.IsNullOrWhiteSpace(d.Cest) ? cfg.CestPadrao : d.Cest);
            if (cest.Length is > 0 and not 7) throw new NfeException("CEST deve ter 7 dígitos.");
            if (CodigosComSt.Contains((regra.CstIcms ?? "").Trim()))
            {
                if (cest.Length == 0)
                    throw new NfeException("ICMS por substituição tributária exige o CEST do veículo. Informe o CEST padrão em Notas fiscais → Configuração (confirme o código com o contador).");
                return cest;
            }
            return cest.Length == 0 ? null : cest;
        }

        // UFs do Sul e Sudeste, menos o Espirito Santo: de la para o resto do
        // pais a aliquota interestadual e' 7%; nos outros casos, 12%.
        private static readonly HashSet<UFBrasil> SulSudesteSemEs = new() { UFBrasil.SP, UFBrasil.RJ, UFBrasil.MG, UFBrasil.PR, UFBrasil.SC, UFBrasil.RS };

        public static decimal AliquotaInterestadual(UFBrasil origem, UFBrasil destino)
            => SulSudesteSemEs.Contains(origem) && !SulSudesteSemEs.Contains(destino) ? 7m : 12m;

        /// <summary>
        /// Diferencial de aliquota (EC 87/2015): venda para consumidor final nao
        /// contribuinte de outro estado por loja do regime normal. A partir de
        /// 2019 o diferencial e' todo da UF de destino (pICMSInterPart = 100).
        /// </summary>
        private static ICMSUFDest MontarDifal(NfeEmissaoInput d, UFBrasil origem, UFBrasil destino, decimal baseCalculo)
        {
            if (d.AliquotaInternaDestino is not { } interna || interna <= 0 || interna > 40)
                throw new NfeException($"Venda para consumidor final de outro estado ({destino}): informe a alíquota interna do ICMS em {destino} para o diferencial de alíquota (DIFAL). Confirme o valor com o contador.");
            var fcp = Math.Clamp(d.PercentualFcpDestino ?? 0m, 0m, 4m);
            var inter = AliquotaInterestadual(origem, destino);
            var vIcms = Math.Max(0m, Math.Round(baseCalculo * (interna - inter) / 100m, 2));
            var vFcp = Math.Round(baseCalculo * fcp / 100m, 2);
            return new ICMSUFDest
            {
                VBCUFDest = (double)baseCalculo,
                VBCFCPUFDest = fcp > 0 ? (double)baseCalculo : 0,
                PFCPUFDest = (double)fcp,
                PICMSUFDest = (double)interna,
                PICMSInter = (double)inter,
                PICMSInterPart = 100,
                VFCPUFDest = (double)vFcp,
                VICMSUFDest = (double)vIcms,
                VICMSUFRemet = 0
            };
        }

        /// <summary>Regra de devolucao quando a loja ainda nao configurou a dela.</summary>
        public static NfeRegraOperacao RegraDevolucaoPadrao(int crt) => new()
        {
            NaturezaOperacao = "Devolucao de venda de veiculo",
            CfopInterno = "1202",
            CfopInterestadual = "2202",
            CstIcms = crt is 1 or 4 ? "900" : "90",
            CstPis = "49",
            CstCofins = "49"
        };

        private static readonly HashSet<string> CstIbsCbsComValor = new() { "000", "200" };
        private static readonly HashSet<string> CstIbsCbsSemValor = new() { "410" };

        /// <summary>
        /// IBS e CBS da reforma tributaria (NT 2025.002). Na transicao a base e'
        /// o valor da operacao menos ICMS, PIS e COFINS. CST 200 aplica a
        /// reducao de aliquota da regra; 410 (imunidade / nao incidencia) vai
        /// sem valores.
        /// </summary>
        public static (IBSCBS Grupo, IBSCBSTot Total) MontarIbsCbs(NfeConfigInfo cfg, NfeRegraOperacao regra, decimal valor, decimal deducoes)
        {
            var cst = SoDigitos(regra.CstIbsCbs);
            var classe = SoDigitos(regra.ClassTribIbsCbs);
            if (cst.Length != 3 || !(CstIbsCbsComValor.Contains(cst) || CstIbsCbsSemValor.Contains(cst)))
                throw new NfeException($"CST do IBS/CBS não suportado: '{regra.CstIbsCbs}'. Use 000, 200 ou 410 (confirme com o contador).");
            if (classe.Length != 6)
                throw new NfeException("Classificação tributária do IBS/CBS (cClassTrib) deve ter 6 dígitos.");

            var grupo = new IBSCBS { CST = cst, CClassTrib = classe };
            if (CstIbsCbsSemValor.Contains(cst))
                return (grupo, TotalIbsCbs(0, 0, 0, 0));

            var reducao = cst == "200" ? Math.Clamp(regra.ReducaoIbsCbs, 0m, 100m) : 0m;
            if (cst == "200" && reducao <= 0)
                throw new NfeException("CST 200 do IBS/CBS precisa do percentual de redução da alíquota.");
            var vBc = Math.Max(0m, Math.Round(valor - deducoes, 2));
            decimal Efetiva(decimal p) => Math.Round(p * (100m - reducao) / 100m, 4);
            decimal Valor(decimal p) => Math.Round(vBc * Efetiva(p) / 100m, 2);
            GRed? Red(decimal p) => reducao > 0 ? new GRed { PRedAliq = (double)reducao, PAliqEfet = (double)Efetiva(p) } : null;

            var vUf = Valor(cfg.AliquotaIbsUf);
            var vMun = Valor(cfg.AliquotaIbsMun);
            var vCbs = Valor(cfg.AliquotaCbs);
            grupo.GIBSCBS = new GIBSCBS
            {
                VBC = (double)vBc,
                GIBSUF = new GIBSUF { PIBSUF = (double)cfg.AliquotaIbsUf, GRed = Red(cfg.AliquotaIbsUf), VIBSUF = (double)vUf },
                GIBSMun = new GIBSMun { PIBSMun = (double)cfg.AliquotaIbsMun, GRed = Red(cfg.AliquotaIbsMun), VIBSMun = (double)vMun },
                VIBS = (double)(vUf + vMun),
                GCBS = new GCBS { PCBS = (double)cfg.AliquotaCbs, GRed = Red(cfg.AliquotaCbs), VCBS = (double)vCbs }
            };
            return (grupo, TotalIbsCbs(vBc, vUf, vMun, vCbs));
        }

        private static IBSCBSTot TotalIbsCbs(decimal vBc, decimal vUf, decimal vMun, decimal vCbs) => new()
        {
            VBCIBSCBS = (double)vBc,
            GIBS = new GIBSTot
            {
                GIBSUF = new GIBSUFTot { VDif = 0, VDevTrib = 0, VIBSUF = (double)vUf },
                GIBSMun = new GIBSMunTot { VDif = 0, VDevTrib = 0, VIBSMun = (double)vMun },
                VIBS = (double)(vUf + vMun),
                VCredPres = 0,
                VCredPresCondSus = 0
            },
            GCBS = new GCBSTot { VDif = 0, VDevTrib = 0, VCBS = (double)vCbs, VCredPres = 0, VCredPresCondSus = 0 }
        };

        /// <summary>Detalhamento de veiculo novo (grupo veicProd), exigido pela Sefaz na venda de zero km.</summary>
        public static VeicProd MontarVeiculoNovo(NfeVeiculoNovo n)
        {
            string Obrig(string? v, string campo, int max)
            {
                var t = SemAcento((v ?? "").Trim()).ToUpperInvariant();
                if (t.Length == 0) throw new NfeException($"Veículo novo: informe {campo}.");
                if (t.Length > max) throw new NfeException($"Veículo novo: {campo} aceita no máximo {max} caracteres.");
                return t;
            }
            var chassi = Obrig(n.Chassi, "o chassi", 17);
            if (chassi.Length != 17) throw new NfeException("Veículo novo: o chassi tem 17 caracteres.");
            if (n.TipoOperacao is < 0 or > 3) throw new NfeException("Veículo novo: tipo de operação inválido.");
            if (n.Condicao is < 1 or > 3) throw new NfeException("Veículo novo: condição do veículo inválida.");
            if (n.Restricao is not (0 or 1 or 2 or 3 or 4 or 9)) throw new NfeException("Veículo novo: restrição inválida.");
            if (n.CodigoMarcaModelo is < 1 or > 999999) throw new NfeException("Veículo novo: informe o código marca/modelo da Denatran (6 dígitos).");
            if (n.AnoModelo is < 1900 or > 2100 || n.AnoFabricacao is < 1900 or > 2100) throw new NfeException("Veículo novo: informe o ano de fabricação e o ano modelo.");
            if (n.Lotacao is < 1 or > 999) throw new NfeException("Veículo novo: informe a lotação.");
            var corDenatran = SoDigitos(n.CorDenatran);
            if (corDenatran.Length is < 1 or > 2) throw new NfeException("Veículo novo: informe a cor na tabela da Denatran (01 a 16).");

            return new VeicProd
            {
                TpOp = (TipoOperacaoVeicNovo)n.TipoOperacao,
                Chassi = chassi,
                CCor = Obrig(n.CodigoCor, "o código da cor da montadora", 4),
                XCor = Obrig(n.DescricaoCor, "a descrição da cor", 40),
                Pot = Obrig(n.Potencia, "a potência (CV)", 4),
                Cilin = Obrig(n.Cilindradas, "as cilindradas", 4),
                PesoL = Obrig(n.PesoLiquido, "o peso líquido", 9),
                PesoB = Obrig(n.PesoBruto, "o peso bruto", 9),
                NSerie = Obrig(n.NumeroSerie, "o número de série", 9),
                TpComb = Obrig(n.TipoCombustivel, "o tipo de combustível (tabela Renavam)", 2),
                NMotor = Obrig(n.NumeroMotor, "o número do motor", 21),
                CMT = Obrig(n.CapacidadeTracao, "a capacidade máxima de tração", 9),
                Dist = Obrig(n.DistanciaEixos, "a distância entre eixos", 4),
                AnoMod = n.AnoModelo.ToString(CultureInfo.InvariantCulture),
                AnoFab = n.AnoFabricacao.ToString(CultureInfo.InvariantCulture),
                TpPint = Obrig(n.TipoPintura, "o tipo de pintura", 1),
                TpVeic = Obrig(n.TipoVeiculo, "o tipo de veículo (tabela Renavam)", 2),
                EspVeic = Obrig(n.EspecieVeiculo, "a espécie do veículo", 1),
                VIN = n.VinRemarcado ? CondicaoVIN.Remarcado : CondicaoVIN.Normal,
                CondVeic = (CondicaoVeiculo)n.Condicao,
                CMod = n.CodigoMarcaModelo,
                CCorDENATRAN = corDenatran.PadLeft(2, '0'),
                Lota = n.Lotacao,
                TpRest = (TipoRestricaoVeiculo)n.Restricao
            };
        }

        public static Pag MontarPagamento(string codigo, decimal valor)
        {
            var meio = (codigo ?? "").Trim() switch
            {
                "01" => MeioPagamento.Dinheiro,
                "02" => MeioPagamento.Cheque,
                "03" => MeioPagamento.CartaoCredito,
                "04" => MeioPagamento.CartaoDebito,
                "15" => MeioPagamento.BoletoBancario,
                "16" => MeioPagamento.DepositoBancario,
                "17" => MeioPagamento.PagamentoInstantaneo,
                "18" => MeioPagamento.TED,
                "90" => MeioPagamento.SemPagamento,
                _ => MeioPagamento.Outros
            };
            var det = new DetPag
            {
                TPag = meio,
                VPag = meio == MeioPagamento.SemPagamento ? 0 : (double)valor
            };
            if (meio == MeioPagamento.Outros) det.XPag = "Outros";
            return new Pag { DetPag = new List<DetPag> { det } };
        }

        private static InfAdic? MontarInfAdic(NfeConfigInfo cfg, NfeEmissaoInput d, Veiculo v)
        {
            var partes = new List<string>();
            if (v.VeiKm > 0) partes.Add($"Quilometragem: {v.VeiKm.ToString("N0", new CultureInfo("pt-BR"))} km");
            if (!string.IsNullOrWhiteSpace(cfg.InformacoesComplementares)) partes.Add(cfg.InformacoesComplementares.Trim());
            if (!string.IsNullOrWhiteSpace(d.InformacoesComplementares)) partes.Add(d.InformacoesComplementares.Trim());
            return partes.Count == 0 ? null : new InfAdic { InfCpl = Limitar(SemAcento(string.Join("; ", partes)), 5000) };
        }

        private static InfRespTec? MontarRespTec(NfeConfigInfo cfg)
        {
            var cnpj = SoDigitos(cfg.RespTecCnpj);
            if (cnpj.Length != 14) return null;
            return new InfRespTec
            {
                CNPJ = cnpj,
                XContato = Limitar(cfg.RespTecContato, 60),
                Email = cfg.RespTecEmail?.Trim(),
                Fone = SoDigitos(cfg.RespTecTelefone)
            };
        }

        public static UFBrasil Uf(string? sigla)
            => Enum.TryParse<UFBrasil>((sigla ?? "").Trim().ToUpperInvariant(), out var uf) && Enum.IsDefined(uf)
                ? uf
                : throw new NfeException($"UF inválida: '{sigla}'.");

        private static CRT Crt(int crt) => crt switch
        {
            1 => CRT.SimplesNacional,
            2 => CRT.SimplesNacionalExcessoSublimite,
            3 => CRT.RegimeNormal,
            4 => CRT.SimplesNacionalMEI,
            _ => throw new NfeException("Regime tributário (CRT) inválido.")
        };

        public static string SoDigitos(string? v) => new((v ?? "").Where(char.IsDigit).ToArray());

        private static string? Vazio(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;

        private static string Limitar(string? v, int max)
        {
            var t = SemAcento((v ?? "").Trim());
            return t.Length > max ? t[..max] : t;
        }

        /// <summary>A Sefaz aceita acento, mas varios DANFEs e sistemas de contador nao: sai sem.</summary>
        public static string SemAcento(string texto)
        {
            var normal = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normal.Length);
            foreach (var c in normal)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
