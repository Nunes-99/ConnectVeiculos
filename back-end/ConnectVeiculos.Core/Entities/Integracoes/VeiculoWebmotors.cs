namespace ConnectVeiculos.Core.Entities.Integracoes
{
    /// <summary>
    /// Anuncio do veiculo na Webmotors. Guarda os codigos das tabelas dela
    /// (escolhidos uma vez na tela) para que alterar preco, vender ou voltar a
    /// vender atualize o anuncio sozinho, sem perguntar de novo.
    /// </summary>
    public class VeiculoWebmotors
    {
        public const string StatusAtivo = "ATIVO";
        public const string StatusRemovido = "REMOVIDO";
        public const string StatusErro = "ERRO";

        /// <summary>
        /// Tirado do ar pelo usuario. Diferente de REMOVIDO (venda/inativacao):
        /// editar ou estornar nao recoloca; so o botao Anunciar.
        /// </summary>
        public const string StatusPausado = "PAUSADO";

        public int VwmId { get; private set; }
        public int R_VeiId { get; private set; }
        public long? VwmCodigoAnuncio { get; private set; }
        public string VwmStatus { get; private set; }
        public string VwmDadosJson { get; private set; }

        /// <summary>Codigos das fotos enviadas, para trocar sem duplicar.</summary>
        public string VwmFotosJson { get; private set; }

        /// <summary>Assinatura das fotos enviadas (caminhos em ordem).</summary>
        public string VwmFotosAssinatura { get; private set; }

        public string VwmErro { get; private set; }
        public DateTime? VwmPublicadoEm { get; private set; }
        public DateTime? VwmAtualizadoEm { get; private set; }
        public DateTime? VwmRemovidoEm { get; private set; }

        public VeiculoWebmotors() { }

        public VeiculoWebmotors(int veiculoId, string dadosJson)
        {
            R_VeiId = veiculoId;
            VwmDadosJson = dadosJson;
            VwmStatus = StatusRemovido;
        }

        public bool EstaPublicado => VwmStatus == StatusAtivo && VwmCodigoAnuncio is > 0;

        public void SalvarDados(string dadosJson) => VwmDadosJson = dadosJson;

        public void Publicado(long codigoAnuncio)
        {
            if (VwmCodigoAnuncio != codigoAnuncio)
            {
                // Anuncio novo: as fotos do anterior nao existem nele.
                VwmFotosJson = null;
                VwmFotosAssinatura = null;
                VwmPublicadoEm = DateTime.UtcNow;
            }
            VwmCodigoAnuncio = codigoAnuncio;
            VwmStatus = StatusAtivo;
            VwmErro = null;
            VwmAtualizadoEm = DateTime.UtcNow;
            VwmRemovidoEm = null;
        }

        public void FotosEnviadas(string fotosJson, string assinatura)
        {
            VwmFotosJson = fotosJson;
            VwmFotosAssinatura = assinatura;
        }

        public bool Pausado => VwmStatus == StatusPausado;

        public void Removido(bool peloUsuario = false)
        {
            VwmStatus = peloUsuario ? StatusPausado : StatusRemovido;
            VwmErro = null;
            VwmRemovidoEm = DateTime.UtcNow;
        }

        /// <summary>
        /// Falha ao incluir/alterar. Mantem o codigo do anuncio: se ele existe
        /// na Webmotors, a proxima tentativa altera em vez de duplicar.
        /// </summary>
        public void Falhou(string erro)
        {
            VwmErro = erro;
            if (!EstaPublicado) VwmStatus = StatusErro;
            VwmAtualizadoEm = DateTime.UtcNow;
        }
    }
}
