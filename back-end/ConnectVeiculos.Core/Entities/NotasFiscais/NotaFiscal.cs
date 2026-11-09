namespace ConnectVeiculos.Core.Entities.NotasFiscais
{
    /// <summary>
    /// NF-e (modelo 55) emitida pela loja para um veiculo: a de ENTRADA (a loja
    /// compra de pessoa fisica e emite a propria nota) ou a de SAIDA (venda).
    /// O numero e' reservado na criacao; se a Sefaz rejeitar, a mesma nota
    /// (mesmo numero) e' corrigida e enviada de novo.
    /// </summary>
    public class NotaFiscal
    {
        public const string Entrada = "ENTRADA";
        public const string Saida = "SAIDA";
        /// <summary>Devolucao de venda: entrada com finalidade 4, referenciando a nota de venda.</summary>
        public const string Devolucao = "DEVOLUCAO";

        public const string StatusPendente = "PENDENTE";
        public const string StatusAutorizada = "AUTORIZADA";
        public const string StatusRejeitada = "REJEITADA";
        public const string StatusCancelada = "CANCELADA";

        /// <summary>Nota rejeitada cujo numero foi inutilizado na Sefaz: nao pode mais ser reenviada.</summary>
        public const string StatusInutilizada = "INUTILIZADA";

        /// <summary>A Sefaz aceita ate 20 cartas de correcao por nota.</summary>
        public const int MaximoCartasCorrecao = 20;

        public int NfId { get; private set; }
        public int R_VeiId { get; private set; }
        public string NfTipo { get; private set; } = Saida;
        public string NfAmbiente { get; private set; } = "homologacao";
        public int NfSerie { get; private set; }
        public int NfNumero { get; private set; }
        public string? NfChave { get; private set; }
        public string NfStatus { get; private set; } = StatusPendente;
        public int? NfCStat { get; private set; }
        public string? NfMotivo { get; private set; }
        public string? NfProtocolo { get; private set; }
        public DateTime NfDtEmissao { get; private set; }
        public DateTime? NfDtAutorizacao { get; private set; }
        public decimal NfValor { get; private set; }
        public string? NfDestDocumento { get; private set; }
        public string? NfDestNome { get; private set; }

        /// <summary>Dados informados na emissao (JSON), para reenviar apos uma rejeicao.</summary>
        public string? NfDadosJson { get; private set; }

        /// <summary>XML autorizado (nfeProc) - o documento fiscal que a loja guarda por 5 anos.</summary>
        public string? NfXml { get; private set; }

        /// <summary>
        /// A NF-e assinada do ultimo envio. Se a Sefaz autorizar e a resposta
        /// se perder, e com ela e o protocolo da consulta que se monta o nfeProc.
        /// </summary>
        public string? NfXmlAssinado { get; private set; }

        public DateTime? NfDtCancelamento { get; private set; }
        public string? NfProtocoloCancelamento { get; private set; }
        public string? NfXmlCancelamento { get; private set; }

        // Carta de correcao: cada nova substitui a anterior (precisa trazer todas as correcoes).
        public int NfCceSequencia { get; private set; }
        public string? NfCceTexto { get; private set; }
        public string? NfCceProtocolo { get; private set; }
        public DateTime? NfCceData { get; private set; }
        public string? NfCceXml { get; private set; }

        /// <summary>Devolucao: chave da nota de venda devolvida.</summary>
        public string? NfChaveReferenciada { get; private set; }

        /// <summary>Ultimo envio de XML e DANFE ao cliente (obrigacao do emitente).</summary>
        public string? NfEmailEnviadoPara { get; private set; }
        public DateTime? NfDtEnvioEmail { get; private set; }

        public void ReferenciarNota(string chave) => NfChaveReferenciada = chave;

        public void RegistrarEnvioEmail(string email)
        {
            NfEmailEnviadoPara = email;
            NfDtEnvioEmail = DateTime.UtcNow;
        }

        public NotaFiscal() { }

        public NotaFiscal(int veiculoId, string tipo, string ambiente, int serie, int numero)
        {
            R_VeiId = veiculoId;
            NfTipo = tipo;
            NfAmbiente = ambiente;
            NfSerie = serie;
            NfNumero = numero;
            NfDtEmissao = DateTime.UtcNow;
        }

        public bool PodeReenviar => NfStatus is StatusPendente or StatusRejeitada;

        public void PrepararEnvio(string chave, decimal valor, string? destDocumento, string? destNome, string dadosJson, DateTime dataEmissao)
        {
            NfChave = chave;
            NfValor = valor;
            NfDestDocumento = destDocumento;
            NfDestNome = destNome;
            NfDadosJson = dadosJson;
            NfDtEmissao = dataEmissao;
            NfStatus = StatusPendente;
        }

        public void GuardarXmlAssinado(string xml) => NfXmlAssinado = xml;

        /// <summary>Sem resposta (ou lote em processamento): pode ter sido autorizada.</summary>
        public void FicarPendente(int? cStat, string motivo)
        {
            NfStatus = StatusPendente;
            NfCStat = cStat;
            NfMotivo = motivo.Length > 1000 ? motivo[..1000] : motivo;
        }

        public void Autorizar(int cStat, string motivo, string protocolo, DateTime? dataAutorizacao, string xmlProc)
        {
            NfStatus = StatusAutorizada;
            NfCStat = cStat;
            NfMotivo = motivo;
            NfProtocolo = protocolo;
            NfDtAutorizacao = dataAutorizacao ?? DateTime.UtcNow;
            NfXml = xmlProc;
        }

        public void Rejeitar(int? cStat, string motivo)
        {
            NfStatus = StatusRejeitada;
            NfCStat = cStat;
            NfMotivo = motivo.Length > 1000 ? motivo[..1000] : motivo;
        }

        public void RegistrarCartaCorrecao(int sequencia, string texto, string protocolo, DateTime? data, string xmlEvento)
        {
            NfCceSequencia = sequencia;
            NfCceTexto = texto;
            NfCceProtocolo = protocolo;
            NfCceData = data ?? DateTime.UtcNow;
            NfCceXml = xmlEvento;
        }

        public void Inutilizar(string motivo)
        {
            NfStatus = StatusInutilizada;
            NfMotivo = motivo.Length > 1000 ? motivo[..1000] : motivo;
        }

        public void Cancelar(string protocolo, DateTime? data, string xmlEvento)
        {
            NfStatus = StatusCancelada;
            NfProtocoloCancelamento = protocolo;
            NfDtCancelamento = data ?? DateTime.UtcNow;
            NfXmlCancelamento = xmlEvento;
        }
    }
}
