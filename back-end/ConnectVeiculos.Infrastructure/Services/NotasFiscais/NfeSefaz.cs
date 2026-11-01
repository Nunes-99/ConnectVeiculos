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
        {
            var envio = new EnvEvento
            {
                Versao = "1.00",
                IdLote = DateTime.UtcNow.ToString("yyMMddHHmmssfff"),
                Evento = new List<Evento>
                {
                    new Evento
                    {
                        Versao = "1.00",
                        InfEvento = new InfEvento(new DetEventoCanc
                        {
                            NProt = protocolo,
                            Versao = "1.00",
                            XJust = justificativa
                        })
                        {
                            COrgao = uf,
                            ChNFe = chave,
                            CNPJ = cnpj,
                            DhEvento = DateTime.Now,
                            TpEvento = TipoEventoNFe.Cancelamento,
                            NSeqEvento = 1,
                            VerEvento = "1.00",
                            TpAmb = homologacao ? TipoAmbiente.Homologacao : TipoAmbiente.Producao
                        }
                    }
                }
            };

            var config = new Configuracao { TipoDFe = TipoDFe.NFe, CertificadoDigital = certificado };
            var evento = new RecepcaoEvento(envio, config);
            evento.Executar();

            var ret = evento.Result?.RetEvento?.FirstOrDefault()?.InfEvento;
            if (ret == null)
                return new NfeRetornoEvento { CStat = evento.Result?.CStat, Motivo = evento.Result?.XMotivo ?? "A Sefaz não respondeu ao cancelamento." };

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
