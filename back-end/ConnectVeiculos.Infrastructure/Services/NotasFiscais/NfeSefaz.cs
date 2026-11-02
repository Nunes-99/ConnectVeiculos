using System.Security.Cryptography.X509Certificates;
using ConnectVeiculos.Core.NotasFiscais;
using Unimake.Business.DFe.Servicos;
using Unimake.Business.DFe.Servicos.NFe;
using Unimake.Business.DFe.Xml.NFe;

namespace ConnectVeiculos.Infrastructure.Services.NotasFiscais
{
    public class NfeRetornoAutorizacao
    {
        public bool Autorizada { get; set; }
        public int? CStat { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? Protocolo { get; set; }
        public DateTime? DataAutorizacao { get; set; }
        public string? XmlProc { get; set; }
    }

    public class NfeRetornoEvento
    {
        public bool Registrado { get; set; }
        public int? CStat { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? Protocolo { get; set; }
        public DateTime? Data { get; set; }
        public string? Xml { get; set; }
    }

    /// <summary>
    /// Conversa com a Sefaz (webservices da UF), separado do servico para os
    /// testes nao dependerem de rede nem de certificado ICP-Brasil.
    /// </summary>
    public interface INfeSefaz
    {
        NfeRetornoAutorizacao Autorizar(EnviNFe xml, X509Certificate2 certificado);
        NfeRetornoEvento Cancelar(string chave, string protocolo, string justificativa, string cnpj,
            UFBrasil uf, bool homologacao, X509Certificate2 certificado);
        NfeStatusSefaz Status(UFBrasil uf, bool homologacao, X509Certificate2 certificado);
        NfeRetornoEvento CartaCorrecao(string chave, int sequencia, string correcao, string cnpj,
            UFBrasil uf, bool homologacao, X509Certificate2 certificado);
        NfeRetornoEvento Inutilizar(int serie, int inicial, int final, string justificativa, string cnpj,
            UFBrasil uf, bool homologacao, X509Certificate2 certificado);
    }

    public class NfeSefazUnimake : INfeSefaz
    {
        // 100 autorizada, 150 autorizada fora de prazo.
        private static readonly int[] Autorizacoes = { 100, 150 };
        // 135 evento registrado, 155 cancelamento fora de prazo homologado.
        private static readonly int[] EventosOk = { 135, 136, 155 };

        public NfeRetornoAutorizacao Autorizar(EnviNFe xml, X509Certificate2 certificado)
        {
            var config = new Configuracao
            {
                TipoDFe = TipoDFe.NFe,
                TipoEmissao = TipoEmissao.Normal,
                CertificadoDigital = certificado
            };

            Autorizacao autorizacao;
            try
            {
                // O construtor assina e valida contra o schema: erro aqui e' dado nosso, nao da Sefaz.
                autorizacao = new Autorizacao(xml, config);
            }
            catch (Exception ex)
            {
                throw new NfeException("A nota não passou na validação: " + ex.Message);
            }

            autorizacao.Executar();
            var lote = autorizacao.Result;
            var prot = lote?.ProtNFe?.InfProt;
            if (prot == null)
            {
                return new NfeRetornoAutorizacao
                {
                    CStat = lote?.CStat,
                    Motivo = lote?.XMotivo ?? "A Sefaz não devolveu o protocolo da nota."
                };
            }

            var chave = xml.NFe[0].InfNFeField.Chave;
            var autorizada = Autorizacoes.Contains(prot.CStat);
            return new NfeRetornoAutorizacao
            {
                Autorizada = autorizada,
                CStat = prot.CStat,
                Motivo = prot.XMotivo,
                Protocolo = prot.NProt,
                DataAutorizacao = prot.DhRecbto.UtcDateTime,
                XmlProc = autorizada && autorizacao.NfeProcResults.TryGetValue(chave, out var proc)
                    ? proc.GerarXML().OuterXml
                    : null
            };
        }

        public NfeRetornoEvento Cancelar(string chave, string protocolo, string justificativa, string cnpj,
            UFBrasil uf, bool homologacao, X509Certificate2 certificado)
            => EnviarEvento(new DetEventoCanc { NProt = protocolo, Versao = "1.00", XJust = justificativa },
                TipoEventoNFe.Cancelamento, 1, chave, cnpj, uf, homologacao, certificado, "ao cancelamento");

        /// <summary>CC-e (evento 110110). O texto de condicao de uso a biblioteca preenche.</summary>
        public NfeRetornoEvento CartaCorrecao(string chave, int sequencia, string correcao, string cnpj,
            UFBrasil uf, bool homologacao, X509Certificate2 certificado)
            => EnviarEvento(new DetEventoCCE { Versao = "1.00", XCorrecao = correcao },
                TipoEventoNFe.CartaCorrecao, sequencia, chave, cnpj, uf, homologacao, certificado, "à carta de correção");

        private static NfeRetornoEvento EnviarEvento(EventoDetalhe detalhe, TipoEventoNFe tipo, int sequencia, string chave,
            string cnpj, UFBrasil uf, bool homologacao, X509Certificate2 certificado, string oQue)
        {
            var envio = MontarEvento(detalhe, tipo, sequencia, chave, cnpj, uf, homologacao);
            var config = new Configuracao { TipoDFe = TipoDFe.NFe, CertificadoDigital = certificado };
            var evento = new RecepcaoEvento(envio, config);
            evento.Executar();

            var ret = evento.Result?.RetEvento?.FirstOrDefault()?.InfEvento;
            if (ret == null)
                return new NfeRetornoEvento { CStat = evento.Result?.CStat, Motivo = evento.Result?.XMotivo ?? $"A Sefaz não respondeu {oQue}." };

            var ok = EventosOk.Contains(ret.CStat);
            return new NfeRetornoEvento
            {
                Registrado = ok,
                CStat = ret.CStat,
                Motivo = ret.XMotivo,
                Protocolo = ret.NProt,
                Data = ret.DhRegEvento.UtcDateTime,
                Xml = ok && evento.ProcEventoNFeResult.Count > 0 ? evento.ProcEventoNFeResult[0].GerarXML().OuterXml : null
            };
        }

        /// <summary>Monta o lote do evento (separado para os testes validarem contra o XSD sem rede).</summary>
        public static EnvEvento MontarCartaCorrecao(string chave, int sequencia, string correcao, string cnpj, UFBrasil uf, bool homologacao)
            => MontarEvento(new DetEventoCCE { Versao = "1.00", XCorrecao = correcao }, TipoEventoNFe.CartaCorrecao,
                sequencia, chave, cnpj, uf, homologacao);

        private static EnvEvento MontarEvento(EventoDetalhe detalhe, TipoEventoNFe tipo, int sequencia, string chave,
            string cnpj, UFBrasil uf, bool homologacao)
        {
            return new EnvEvento
            {
                Versao = "1.00",
                IdLote = DateTime.UtcNow.ToString("yyMMddHHmmssfff"),
                Evento = new List<Evento>
                {
                    new Evento
                    {
                        Versao = "1.00",
                        InfEvento = new InfEvento(detalhe)
                        {
                            COrgao = uf,
                            ChNFe = chave,
                            CNPJ = cnpj,
                            DhEvento = DateTime.Now,
                            TpEvento = tipo,
                            NSeqEvento = sequencia,
                            VerEvento = "1.00",
                            TpAmb = homologacao ? TipoAmbiente.Homologacao : TipoAmbiente.Producao
                        }
                    }
                }
            };
        }

        /// <summary>Inutilizacao de numeracao: 102 = homologada.</summary>
        public NfeRetornoEvento Inutilizar(int serie, int inicial, int final, string justificativa, string cnpj,
            UFBrasil uf, bool homologacao, X509Certificate2 certificado)
        {
            var inut = MontarInutilizacao(serie, inicial, final, justificativa, cnpj, uf, homologacao);
            var config = new Configuracao { TipoDFe = TipoDFe.NFe, CertificadoDigital = certificado };
            var servico = new Inutilizacao(inut, config);
            servico.Executar();

            var ret = servico.Result?.InfInut;
            if (ret == null) return new NfeRetornoEvento { Motivo = "A Sefaz não respondeu à inutilização." };
            var ok = ret.CStat == 102;
            return new NfeRetornoEvento
            {
                Registrado = ok,
                CStat = ret.CStat,
                Motivo = ret.XMotivo,
                Protocolo = ret.NProt,
                Data = ret.DhRecbto.UtcDateTime,
                Xml = ok ? servico.ProcInutNFeResult?.GerarXML().OuterXml : null
            };
        }

        public static InutNFe MontarInutilizacao(int serie, int inicial, int final, string justificativa, string cnpj,
            UFBrasil uf, bool homologacao)
        {
            return new InutNFe
            {
                Versao = "4.00",
                InfInut = new InutNFeInfInut
                {
                    Ano = DateTime.Now.ToString("yy"),
                    CNPJ = cnpj,
                    CUF = uf,
                    Mod = ModeloDFe.NFe,
                    Serie = serie,
                    NNFIni = inicial,
                    NNFFin = final,
                    XJust = justificativa,
                    TpAmb = homologacao ? TipoAmbiente.Homologacao : TipoAmbiente.Producao
                }
            };
        }

        public NfeStatusSefaz Status(UFBrasil uf, bool homologacao, X509Certificate2 certificado)
        {
            var tpAmb = homologacao ? TipoAmbiente.Homologacao : TipoAmbiente.Producao;
            var config = new Configuracao { TipoDFe = TipoDFe.NFe, CertificadoDigital = certificado };
            var status = new StatusServico(new ConsStatServ { Versao = "4.00", CUF = uf, TpAmb = tpAmb }, config);
            status.Executar();
            return new NfeStatusSefaz
            {
                CStat = status.Result.CStat,
                Motivo = status.Result.XMotivo,
                Ambiente = homologacao ? "homologacao" : "producao"
            };
        }
    }
}
