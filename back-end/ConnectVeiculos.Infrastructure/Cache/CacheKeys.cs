namespace ConnectVeiculos.Infrastructure.Cache
{
    public static class CacheKeys
    {
        public const string Lojas = "lojas";
        public const string Categorias = "categorias";
        public const string Usuarios = "usuarios";
        public const string Veiculos = "veiculos";
        public const string Vendas = "vendas";
        public const string Dashboard = "dashboard";
        public const string Catalogo = "catalogo";

        public static string LojaById(int id) => $"loja_{id}";
        public static string CategoriaById(int id) => $"categoria_{id}";
        public static string UsuarioById(int id) => $"usuario_{id}";
        public static string VeiculoById(int id) => $"veiculo_{id}";
        public static string VendaById(int id) => $"venda_{id}";
        public static string VeiculosPorLoja(int lojaId) => $"veiculos_loja_{lojaId}";
    }
}
