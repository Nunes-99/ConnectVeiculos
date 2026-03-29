namespace ConnectVeiculos.Application.ViewModels.Vendas
{
    public class VendaViewModel
    {
        public int VenId { get; set; }
        public int R_VeiId { get; set; }
        public int R_UsuId { get; set; }
        public string VendedorNome { get; set; }
        public DateTime VenDtVenda { get; set; }
        public string VenMarca { get; set; }
        public string VenModelo { get; set; }
        public short VenAno { get; set; }
        public string VenChassi { get; set; }
        public decimal VenValor { get; set; }
        public decimal VenComissaoPorc { get; set; }
        public decimal VenComissaoValor { get; set; }

        // Dados do Comprador
        public string VenCompradorNome { get; set; }
        public string VenCompradorCpf { get; set; }
        public string VenCompradorTelefone { get; set; }
        public string VenCompradorEmail { get; set; }
        public string VenCompradorEndereco { get; set; }

        // Forma de Pagamento e Status
        public string VenFormaPagamento { get; set; }
        public string VenObservacao { get; set; }
        public string VenStatus { get; set; }
        public DateTime? VenDtEstorno { get; set; }
    }
}
