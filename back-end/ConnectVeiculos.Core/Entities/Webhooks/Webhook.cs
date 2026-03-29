namespace ConnectVeiculos.Core.Entities.Webhooks
{
    public class Webhook
    {
        public int WebId { get; set; }
        public string WebUrl { get; set; }
        public string WebEventos { get; set; } // JSON array: ["venda.criada", "veiculo.vendido"]
        public string WebSecret { get; set; } // Para assinatura HMAC
        public bool WebAtivo { get; set; }
        public DateTime WebCriadoEm { get; set; }
        public DateTime? WebUltimaExecucao { get; set; }
        public int WebFalhasConsecutivas { get; set; }

        public Webhook()
        {
            WebAtivo = true;
            WebCriadoEm = DateTime.UtcNow;
            WebFalhasConsecutivas = 0;
        }

        public Webhook(string url, string eventos, string secret) : this()
        {
            WebUrl = url;
            WebEventos = eventos;
            WebSecret = secret;
        }

        public void RegistrarExecucao(bool sucesso)
        {
            WebUltimaExecucao = DateTime.UtcNow;
            if (sucesso)
            {
                WebFalhasConsecutivas = 0;
            }
            else
            {
                WebFalhasConsecutivas++;
                // Desativar apos 5 falhas consecutivas
                if (WebFalhasConsecutivas >= 5)
                {
                    WebAtivo = false;
                }
            }
        }
    }

    public static class WebhookEventos
    {
        public const string VendaCriada = "venda.criada";
        public const string VendaEstornada = "venda.estornada";
        public const string VeiculoCadastrado = "veiculo.cadastrado";
        public const string VeiculoVendido = "veiculo.vendido";
        public const string VeiculoPrecoAlterado = "veiculo.preco_alterado";
        public const string UsuarioCadastrado = "usuario.cadastrado";
    }
}
