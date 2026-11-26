namespace ConnectVeiculos.Core.Entities.Integracoes
{
    /// <summary>
    /// Anuncio do veiculo na OLX. A importacao da OLX e' assincrona: o envio
    /// devolve um token e o resultado (aceito/recusado, link) vem depois.
    /// Guarda os codigos escolhidos uma vez na tela para que editar, vender
    /// ou voltar a vender atualize o anuncio sozinho.
    /// </summary>
    public class VeiculoOlx
    {
        /// <summary>Enviado para ficar no ar (o resultado da OLX fica em VolSituacao).</summary>
        public const string StatusAtivo = "ATIVO";
        /// <summary>Saiu por venda/inativacao; volta sozinho se o carro voltar.</summary>
        public const string StatusRemovido = "REMOVIDO";
        /// <summary>Tirado do ar pelo usuario: so volta pelo botao Anunciar.</summary>
        public const string StatusPausado = "PAUSADO";
        public const string StatusErro = "ERRO";

        public int VolId { get; private set; }
        public int R_VeiId { get; private set; }
        public string VolStatus { get; private set; }
        public string VolDadosJson { get; private set; }

        /// <summary>Token do ultimo envio, para consultar o resultado; nulo quando ja conferido.</summary>
        public string VolToken { get; private set; }
        public string VolSituacao { get; private set; }
        public string VolListId { get; private set; }
        public string VolUrl { get; private set; }

        /// <summary>Assinatura do ultimo anuncio enviado: so reenvia se algo mudou.</summary>
        public string VolAssinatura { get; private set; }
        public int VolFotos { get; private set; }

        public string VolErro { get; private set; }
        public DateTime? VolPublicadoEm { get; private set; }
        public DateTime? VolAtualizadoEm { get; private set; }
        public DateTime? VolRemovidoEm { get; private set; }

        public VeiculoOlx() { }

        public VeiculoOlx(int veiculoId, string dadosJson)
        {
            R_VeiId = veiculoId;
            VolDadosJson = dadosJson;
            VolStatus = StatusRemovido;
        }

        public bool EstaPublicado => VolStatus == StatusAtivo;
        public bool Pausado => VolStatus == StatusPausado;

        public void SalvarDados(string dadosJson) => VolDadosJson = dadosJson;

        public void Enviado(string token, string assinatura, int fotos)
        {
            if (VolStatus != StatusAtivo) VolPublicadoEm = DateTime.UtcNow;
            VolStatus = StatusAtivo;
            VolToken = token;
            VolSituacao = "pending";
            VolAssinatura = assinatura;
            VolFotos = fotos;
            VolErro = null;
            VolAtualizadoEm = DateTime.UtcNow;
            VolRemovidoEm = null;
        }

        public void Removido(string token, bool peloUsuario = false)
        {
            VolStatus = peloUsuario ? StatusPausado : StatusRemovido;
            VolToken = token;
            VolSituacao = "pending";
            VolAssinatura = null;
            VolErro = null;
            VolRemovidoEm = DateTime.UtcNow;
        }

        /// <summary>A OLX ainda processa o envio (pending/queued): o token continua para conferir de novo.</summary>
        public void Processando(string situacao) => VolSituacao = situacao;

        /// <summary>Resultado do envio conferido na OLX.</summary>
        public void Conferido(string situacao, string listId, string url, string erro)
        {
            VolToken = null;
            VolSituacao = situacao;
            if (!string.IsNullOrEmpty(listId)) VolListId = listId;
            if (!string.IsNullOrEmpty(url)) VolUrl = url;
            if (!string.IsNullOrEmpty(erro))
            {
                VolErro = erro.Length > 1000 ? erro[..1000] : erro;
                // Recusado: a proxima edicao manda de novo.
                VolAssinatura = null;
            }
            VolAtualizadoEm = DateTime.UtcNow;
        }

        /// <summary>A OLX nao aceitou o envio (validacao, plano, conexao).</summary>
        public void Falhou(string erro)
        {
            VolErro = erro.Length > 1000 ? erro[..1000] : erro;
            if (VolStatus != StatusAtivo) VolStatus = StatusErro;
            VolAssinatura = null;
            VolAtualizadoEm = DateTime.UtcNow;
        }
    }
}
