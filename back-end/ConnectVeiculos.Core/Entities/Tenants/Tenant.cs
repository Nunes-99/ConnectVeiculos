namespace ConnectVeiculos.Core.Entities.Tenants
{
    /// <summary>
    /// Registry de um tenant (cliente operador) no banco master. Cada tenant tem
    /// seu proprio banco de dados isolado em data/{slug}.db.
    /// </summary>
    public class Tenant
    {
        public int TenId { get; private set; }

        /// <summary>Identificador unico para subdomain (ex: "acme" -> acme.connectveiculos.dev.br).</summary>
        public string TenSlug { get; private set; } = string.Empty;

        /// <summary>Nome amigavel (ex: "Acme Veiculos LTDA").</summary>
        public string TenNome { get; private set; } = string.Empty;

        /// <summary>Nome do arquivo .db relativo ao diretorio data/. Default: "{slug}.db".</summary>
        public string TenDatabaseFile { get; private set; } = string.Empty;

        public TenantStatus TenStatus { get; private set; } = TenantStatus.Active;

        public DateTime TenDtCriacao { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// Codigo de verificacao do Google Search Console para este tenant.
        /// Apenas o "content" da meta tag (ex: "ABC123XYZ"), sem aspas nem tag.
        /// O SSR injeta no &lt;head&gt; uma &lt;meta name="google-site-verification"&gt;
        /// para CADA tenant que tenha codigo cadastrado, permitindo que o Google
        /// valide multiplas contas Merchant Center sobre o mesmo dominio raiz.
        /// </summary>
        public string? TenGoogleVerifCode { get; private set; }

        /// <summary>
        /// Codigo de verificacao de dominio do Meta/Facebook para este tenant.
        /// Apenas o "content" da meta tag, mesma logica do TenGoogleVerifCode.
        /// </summary>
        public string? TenFacebookVerifCode { get; private set; }

         // FK para Plano (no master). Default na migration: plano Free.
         public int? TenPlaId { get; private set; }

         // Trial expira em (UTC). Durante o trial, limites do plano sao IGNORADOS — tenant
         // tem acesso ilimitado. Apos a data, limites do TenPlaId voltam a valer. Null =
         // sem trial (tenant cadastrado antes da feature ou nunca esteve em trial).
         public DateTime? TenTrialAte { get; private set; }

        /// <summary>
        /// Dominio proprio do catalogo (ex: "diamanteveiculos.com.br"), na forma
        /// de <see cref="Catalogo.DominioProprio.Normalizar"/>. Opcional: sem ele
        /// o catalogo segue em connectveiculos.dev.br/catalogo/{slug}.
        /// </summary>
        public string? TenDominioProprio { get; private set; }

        /// <summary>
        /// Quando o servidor confirmou o HTTPS do dominio (scripts/dominios-proprios.sh).
        /// Null = aguardando o DNS apontar e o certificado sair. So com o dominio
        /// ativo os links publicos (feeds, posts, sitemap) passam a usa-lo.
        /// </summary>
        public DateTime? TenDominioAtivoEm { get; private set; }

        /// <summary>
        /// Assinatura mensal no Mercado Pago (id do /preapproval), o status dela
        /// (<see cref="Interfaces.Services.StatusAssinatura"/>) e o plano pago.
        /// O TenPlaId so muda quando o aviso do Mercado Pago confirma.
        /// </summary>
        public string? TenAssinaturaId { get; private set; }
        public string? TenAssinaturaStatus { get; private set; }
        public int? TenAssinaturaPlaId { get; private set; }
        public DateTime? TenAssinaturaAtualizadaEm { get; private set; }

        /// <summary>
        /// Quando a assinatura atual do cartao foi confirmada: conta os 7 dias
        /// do direito de arrependimento (CDC art. 49).
        /// </summary>
        public DateTime? TenAssinaturaInicio { get; private set; }

        /// <summary>
        /// Plano pago por Pix (sem debito automatico): vale ate esta data (UTC).
        /// Cada Pix aprovado soma 30 dias; vencido, o job volta a loja ao Free.
        /// O id do ultimo pagamento evita somar duas vezes o mesmo aviso.
        /// </summary>
        public DateTime? TenPlanoPagoAte { get; private set; }
        public string? TenPixUltimoPagamentoId { get; private set; }
        public DateTime? TenPixUltimoPagamentoEm { get; private set; }

        public Tenant() { }

        public Tenant(string slug, string nome, string? databaseFile = null)
        {
            TenSlug = slug;
            TenNome = nome;
            TenDatabaseFile = string.IsNullOrWhiteSpace(databaseFile) ? $"{slug}.db" : databaseFile;
            TenStatus = TenantStatus.Active;
            TenDtCriacao = DateTime.UtcNow;
        }

        public void Suspender() => TenStatus = TenantStatus.Suspended;
        public void Reativar() => TenStatus = TenantStatus.Active;

        public void Renomear(string novoNome) => TenNome = novoNome;

        public void SetGoogleVerifCode(string? code)
            => TenGoogleVerifCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();

        public void SetFacebookVerifCode(string? code)
            => TenFacebookVerifCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();

         public void AlterarPlano(int novoPlanoId) => TenPlaId = novoPlanoId;

         public void IniciarTrial(int dias) => TenTrialAte = DateTime.UtcNow.AddDays(dias);

         public bool EmTrial() => TenTrialAte.HasValue && TenTrialAte.Value > DateTime.UtcNow;

        /// <summary>
        /// Soma <paramref name="dias"/> ao periodo pago. Renovar antes de vencer
        /// nao perde os dias que faltavam; trocar de plano comeca do zero.
        /// Devolve false se esse pagamento ja tinha sido contado.
        /// </summary>
        public bool RegistrarPagamentoPix(string pagamentoId, int planoId, int dias, DateTime agora)
        {
            if (TenPixUltimoPagamentoId == pagamentoId) return false;
            var aproveita = TenPlaId == planoId && TenPlanoPagoAte.HasValue && TenPlanoPagoAte.Value > agora;
            TenPlanoPagoAte = (aproveita ? TenPlanoPagoAte!.Value : agora).AddDays(dias);
            TenPixUltimoPagamentoId = pagamentoId;
            TenPixUltimoPagamentoEm = agora;
            TenPlaId = planoId;
            return true;
        }

        public void EncerrarPeriodoPix() => TenPlanoPagoAte = null;

        /// <summary>
        /// Pix devolvido: tira os dias que ele tinha dado. Devolve true se nao
        /// sobrou periodo pago (a loja deve voltar ao Free).
        /// </summary>
        public bool DesfazerPagamentoPix(int dias, DateTime agora)
        {
            TenPixUltimoPagamentoEm = null;
            if (TenPlanoPagoAte.HasValue) TenPlanoPagoAte = TenPlanoPagoAte.Value.AddDays(-dias);
            if (TenPlanoPagoAte.HasValue && TenPlanoPagoAte.Value > agora) return false;
            TenPlanoPagoAte = null;
            return true;
        }

        /// <summary>
        /// Cartao cancelado depois dos 7 dias: o plano segue ate o fim do mes
        /// ja cobrado e o job das horas volta a loja ao Free nessa data.
        /// </summary>
        public void ManterPlanoAte(DateTime fim) => TenPlanoPagoAte = fim;

        public void RegistrarAssinatura(string assinaturaId, string status, int? planoId)
        {
            if (status == Interfaces.Services.StatusAssinatura.Ativa
                && (TenAssinaturaId != assinaturaId || TenAssinaturaInicio == null))
                TenAssinaturaInicio = DateTime.UtcNow;
            TenAssinaturaId = assinaturaId;
            TenAssinaturaStatus = status;
            TenAssinaturaPlaId = planoId;
            TenAssinaturaAtualizadaEm = DateTime.UtcNow;
        }

        /// <summary>
        /// Troca (ou limpa, com null) o dominio proprio. Dominio novo volta a ficar
        /// pendente ate o servidor confirmar o certificado dele.
        /// </summary>
        public void SetDominioProprio(string? dominio)
        {
            if (dominio == TenDominioProprio) return;
            TenDominioProprio = dominio;
            TenDominioAtivoEm = null;
        }

        public void AtivarDominioProprio() => TenDominioAtivoEm ??= DateTime.UtcNow;

        public bool DominioProprioAtivo() => TenDominioProprio != null && TenDominioAtivoEm.HasValue;
    }
}
