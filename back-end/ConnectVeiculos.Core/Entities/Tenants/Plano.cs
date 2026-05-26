namespace ConnectVeiculos.Core.Entities.Tenants
{
    // Plano de assinatura do SaaS. Vive no banco master (compartilhado entre tenants).
    // Limites null = ilimitado (Enterprise). Integracoes (ML/FB/Google/WhatsApp) sao
    // liberadas para todos os planos por decisao do produto; o plano controla apenas
    // capacidade (quantos veiculos, lojas, usuarios e leads/mes o tenant pode usar).
    public class Plano
    {
        public int PlaId { get; private set; }
        public string PlaNome { get; private set; } = string.Empty;
        public decimal PlaPreco { get; private set; }
        public int? PlaMaxVeiculos { get; private set; }
        public int? PlaMaxLojas { get; private set; }
        public int? PlaMaxUsuarios { get; private set; }
        public int? PlaMaxLeadsMes { get; private set; }
        public int PlaOrdem { get; private set; }
        public bool PlaAtivo { get; private set; } = true;
        public DateTime PlaDtCriacao { get; private set; } = DateTime.UtcNow;

        private Plano() { }

        public Plano(string nome, decimal preco, int? maxVeiculos, int? maxLojas,
            int? maxUsuarios, int? maxLeadsMes, int ordem)
        {
            PlaNome = nome;
            PlaPreco = preco;
            PlaMaxVeiculos = maxVeiculos;
            PlaMaxLojas = maxLojas;
            PlaMaxUsuarios = maxUsuarios;
            PlaMaxLeadsMes = maxLeadsMes;
            PlaOrdem = ordem;
            PlaAtivo = true;
            PlaDtCriacao = DateTime.UtcNow;
        }

        public void Atualizar(string nome, decimal preco, int? maxVeiculos, int? maxLojas,
            int? maxUsuarios, int? maxLeadsMes, int ordem)
        {
            PlaNome = nome;
            PlaPreco = preco;
            PlaMaxVeiculos = maxVeiculos;
            PlaMaxLojas = maxLojas;
            PlaMaxUsuarios = maxUsuarios;
            PlaMaxLeadsMes = maxLeadsMes;
            PlaOrdem = ordem;
        }
    }
}
