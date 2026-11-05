namespace ConnectVeiculos.Core.Entities.Integracoes
{
    /// <summary>
    /// Anuncio do veiculo no NaPista. Guarda a versao e os valores das listas
    /// do NaPista (escolhidos uma vez na tela) para que alterar preco, vender
    /// ou voltar a vender atualize o anuncio sozinho.
    /// </summary>
    public class VeiculoNapista
    {
        /// <summary>No ar (PUBLISHED no NaPista).</summary>
        public const string StatusAtivo = "ATIVO";

        /// <summary>Saiu por venda/inativacao (SOLD/UNPUBLISHED); volta sozinho se o carro voltar.</summary>
        public const string StatusRemovido = "REMOVIDO";

        /// <summary>
        /// Tirado do ar pelo usuario. Diferente de REMOVIDO: editar ou estornar
        /// nao recoloca; so o botao Anunciar.
        /// </summary>
        public const string StatusPausado = "PAUSADO";

        public const string StatusErro = "ERRO";

        public int VnpId { get; private set; }
        public int R_VeiId { get; private set; }
        public string VnpAnuncioId { get; private set; }
        public string VnpStatus { get; private set; }
        public string VnpDadosJson { get; private set; }

        /// <summary>Assinatura das fotos enviadas (caminhos em ordem): so reenvia se mudar.</summary>
        public string VnpFotosAssinatura { get; private set; }
        public int VnpFotos { get; private set; }

        public string VnpErro { get; private set; }
        public DateTime? VnpPublicadoEm { get; private set; }
        public DateTime? VnpAtualizadoEm { get; private set; }
        public DateTime? VnpRemovidoEm { get; private set; }

        public VeiculoNapista() { }

        public VeiculoNapista(int veiculoId, string dadosJson)
        {
            R_VeiId = veiculoId;
            VnpDadosJson = dadosJson;
            VnpStatus = StatusRemovido;
        }

        public bool TemAnuncio => !string.IsNullOrEmpty(VnpAnuncioId);
        public bool EstaPublicado => VnpStatus == StatusAtivo && TemAnuncio;
        public bool Pausado => VnpStatus == StatusPausado;

        public void SalvarDados(string dadosJson) => VnpDadosJson = dadosJson;

        public void Publicado(string anuncioId)
        {
            if (VnpAnuncioId != anuncioId)
            {
                // Anuncio novo: as fotos do anterior nao valem para ele.
                VnpFotosAssinatura = null;
                VnpFotos = 0;
                VnpPublicadoEm = DateTime.UtcNow;
            }
            VnpAnuncioId = anuncioId;
            VnpStatus = StatusAtivo;
            VnpErro = null;
            VnpAtualizadoEm = DateTime.UtcNow;
            VnpRemovidoEm = null;
        }

        public void FotosEnviadas(string assinatura, int quantidade)
        {
            VnpFotosAssinatura = assinatura;
            VnpFotos = quantidade;
        }

        public void Removido(bool peloUsuario = false)
        {
            VnpStatus = peloUsuario ? StatusPausado : StatusRemovido;
            VnpErro = null;
            VnpRemovidoEm = DateTime.UtcNow;
        }

        /// <summary>
        /// Falha ao incluir/alterar. Mantem o id do anuncio: se ele existe no
        /// NaPista, a proxima tentativa altera em vez de duplicar.
        /// </summary>
        public void Falhou(string erro)
        {
            VnpErro = erro.Length > 1000 ? erro[..1000] : erro;
            if (!EstaPublicado) VnpStatus = StatusErro;
            VnpAtualizadoEm = DateTime.UtcNow;
        }
    }
}
