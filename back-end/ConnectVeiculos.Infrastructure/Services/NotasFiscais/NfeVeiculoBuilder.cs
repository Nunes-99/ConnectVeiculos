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
            var entrada = string.Equals(dados.Tipo, "ENTRADA", StringComparison.OrdinalIgnoreCase);
            var regra = entrada ? cfg.Compra : cfg.Venda;
            var ufEmit = Uf(cfg.Uf);
            var ufDest = Uf(dados.Endereco.Uf);
            var interna = ufEmit == ufDest;
            var valor = Math.Round(dados.Valor, 2);
            var documento = SoDigitos(dados.Documento);
            var naoContribuinte = string.IsNullOrWhiteSpace(dados.InscricaoEstadual);

            var (icms, vBc, vIcms) = MontarIcms(cfg.Crt, regra, valor);

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
                    FinNFe = FinalidadeNFe.Normal,
                    IndFinal = naoContribuinte ? SimNao.Sim : SimNao.Nao,
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
                Dest = MontarDestinatario(dados, documento, ufDest, naoContribuinte, homologacao),
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
                            CFOP = SoDigitos(interna ? regra.CfopInterno : regra.CfopInterestadual),
                            UCom = "UN",
                            QCom = 1m,
                            VUnCom = valor,
                            VProd = (double)valor,
                            CEANTrib = "SEM GTIN",
                            UTrib = "UN",
                            QTrib = 1m,
                            VUnTrib = valor,
                            IndTot = SimNao.Sim
                        },
                        Imposto = new Imposto
                        {
                            ICMS = icms,
                            PIS = MontarPis(regra.CstPis),
                            COFINS = MontarCofins(regra.CstCofins)
                        }
                    }
                },
                Total = new Total
                {
                    ICMSTot = new ICMSTot
                    {
                        VBC = (double)vBc,
                        VICMS = (double)vIcms,
                        VProd = (double)valor,
                        VNF = (double)valor
                    }
                },
                Transp = new Transp { ModFrete = ModalidadeFrete.SemOcorrenciaTransporte },
                Pag = MontarPagamento(dados.MeioPagamento, valor),
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

        private static Dest MontarDestinatario(NfeEmissaoInput d, string documento, UFBrasil uf, bool naoContribuinte, bool homologacao)
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
                IndIEDest = naoContribuinte ? IndicadorIEDestinatario.NaoContribuinte : IndicadorIEDestinatario.ContribuinteICMS,
                IE = naoContribuinte ? null : SoDigitos(d.InscricaoEstadual),
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
                "90" when aliquota > 0 => (new ICMS
                {
                    ICMS90 = new ICMS90
                    {
                        Orig = OrigemMercadoria.Nacional, CST = codigo, ModBC = ModalidadeBaseCalculoICMS.ValorOperacao,
                        VBC = (double)baseCalculo, PRedBC = (double)reducao, PICMS = (double)aliquota, VICMS = (double)imposto
                    }
                }, baseCalculo, imposto),
                "90" => (new ICMS { ICMS90 = new ICMS90 { Orig = OrigemMercadoria.Nacional, CST = codigo } }, 0m, 0m),
                _ => throw new NfeException($"CST do ICMS não suportado: '{codigo}'. Use 00, 20, 40, 41, 50 ou 90.")
            };
        }

        private static readonly HashSet<string> CstNaoTributado = new() { "04", "05", "06", "07", "08", "09" };

        private static PIS MontarPis(string cst)
        {
            cst = (cst ?? "").Trim();
            return CstNaoTributado.Contains(cst)
                ? new PIS { PISNT = new PISNT { CST = cst } }
                : new PIS { PISOutr = new PISOutr { CST = cst, VBC = 0, PPIS = 0, VPIS = 0 } };
        }

        private static COFINS MontarCofins(string cst)
        {
            cst = (cst ?? "").Trim();
            return CstNaoTributado.Contains(cst)
                ? new COFINS { COFINSNT = new COFINSNT { CST = cst } }
                : new COFINS { COFINSOutr = new COFINSOutr { CST = cst, VBC = 0, PCOFINS = 0, VCOFINS = 0 } };
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
