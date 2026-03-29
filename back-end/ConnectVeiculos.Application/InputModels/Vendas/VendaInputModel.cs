namespace ConnectVeiculos.Application.InputModels.Vendas
{
    public class VendaInputModel
    {
        public int VenId { get; set; }
        public int R_VeiId { get; set; }
        public int R_UsuId { get; set; }
        public DateTime VenDtVenda { get; set; }
        public decimal VenValor { get; set; }
        public decimal VenComissaoPorc { get; set; }

        // Dados do Comprador
        public string VenCompradorNome { get; set; }
        public string VenCompradorCpf { get; set; }
        public string VenCompradorTelefone { get; set; }
        public string VenCompradorEmail { get; set; }
        public string VenCompradorEndereco { get; set; }

        // Forma de Pagamento
        public string VenFormaPagamento { get; set; }
        public string VenObservacao { get; set; }
    }
}
