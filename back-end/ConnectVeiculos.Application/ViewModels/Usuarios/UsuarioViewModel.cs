namespace ConnectVeiculos.Application.ViewModels.Usuarios
{
    public class UsuarioViewModel
    {
        public int UsuId { get; set; }
        public int R_LojId { get; set; }
        public int R_AcsId { get; set; }
        public string LojaNome { get; set; }
        public string AcessoNome { get; set; }
        public string UsuNome { get; set; }
        public string UsuCPF { get; set; }
        public string UsuRG { get; set; }
        public string UsuEmail { get; set; }
        public string UsuFuncao { get; set; }
        public bool UsuSts { get; set; }
    }
}
