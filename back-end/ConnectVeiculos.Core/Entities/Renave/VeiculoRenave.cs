namespace ConnectVeiculos.Core.Entities.Renave
{
    /// <summary>
    /// Registro do veiculo no Renave (estoque eletronico da Senatran/Serpro).
    /// Um por veiculo: guarda o id do estoque no Renave e o andamento das
    /// etapas (entrada, assinatura do vendedor, notas, saida, ATPVe) para a
    /// tela mostrar o que falta sem consultar o Serpro a cada abertura.
    /// </summary>
    public class VeiculoRenave
    {
        public int RnvId { get; private set; }
        public int R_VeiId { get; private set; }

        /// <summary>Id do estoque no Renave (retornado na entrada).</summary>
        public long? RnvIdEstoque { get; private set; }

        /// <summary>Estado do estoque no Renave: SOLICITADO, TRANSFERIDO, CONFIRMADO, FINALIZADO.</summary>
        public string? RnvEstado { get; private set; }

        public DateTime? RnvDtEntrada { get; private set; }
        public decimal? RnvValorCompra { get; private set; }
        public DateTime? RnvDtAssinaturaVendedor { get; private set; }
        public string? RnvChaveNfEntrada { get; private set; }

        public DateTime? RnvDtSaida { get; private set; }
        public decimal? RnvValorVenda { get; private set; }
        public string? RnvCompradorDocumento { get; private set; }
        public string? RnvCompradorNome { get; private set; }
        public string? RnvChaveNfSaida { get; private set; }
        public string? RnvNumeroAtpv { get; private set; }

        /// <summary>Ultima recusa do Renave, para a tela explicar o que deu errado.</summary>
        public string? RnvUltimoErro { get; private set; }
        public DateTime RnvAtualizadoEm { get; private set; }

        public VeiculoRenave() { }

        public VeiculoRenave(int veiculoId)
        {
            R_VeiId = veiculoId;
            RnvAtualizadoEm = DateTime.UtcNow;
        }

        public bool EmEstoque => RnvIdEstoque.HasValue && RnvDtSaida == null;

        public void RegistrarEntrada(long idEstoque, string estado, DateTime dataCompra, decimal valorCompra)
        {
            RnvIdEstoque = idEstoque;
            RnvEstado = estado;
            RnvDtEntrada = dataCompra;
            RnvValorCompra = valorCompra;
            RnvUltimoErro = null;
            Tocar();
        }

        public void RegistrarAssinaturaVendedor()
        {
            RnvDtAssinaturaVendedor = DateTime.UtcNow;
            RnvUltimoErro = null;
            Tocar();
        }

        public void RegistrarNotaEntrada(string chave)
        {
            RnvChaveNfEntrada = chave;
            RnvUltimoErro = null;
            Tocar();
        }

        public void RegistrarSaida(string estado, DateTime dataVenda, decimal valorVenda, string documentoComprador, string? nomeComprador)
        {
            RnvEstado = estado;
            RnvDtSaida = dataVenda;
            RnvValorVenda = valorVenda;
            RnvCompradorDocumento = documentoComprador;
            RnvCompradorNome = nomeComprador;
            RnvUltimoErro = null;
            Tocar();
        }

        public void RegistrarNotaSaida(string chave)
        {
            RnvChaveNfSaida = chave;
            RnvUltimoErro = null;
            Tocar();
        }

        public void RegistrarAtpv(string numero)
        {
            RnvNumeroAtpv = numero;
            Tocar();
        }

        public void AtualizarEstado(string estado)
        {
            RnvEstado = estado;
            Tocar();
        }

        public void RegistrarErro(string mensagem)
        {
            RnvUltimoErro = mensagem.Length > 1000 ? mensagem[..1000] : mensagem;
            Tocar();
        }

        private void Tocar() => RnvAtualizadoEm = DateTime.UtcNow;
    }
}
