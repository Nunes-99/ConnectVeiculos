namespace ConnectVeiculos.Core.Entities.PushSubscriptions
{
    public class PushSubscription
    {
        public int PsbId { get; private set; }
        public int? R_UsuId { get; private set; }
        public string PsbEndpoint { get; private set; } = "";
        public string PsbP256dh { get; private set; } = "";
        public string PsbAuth { get; private set; } = "";
        public string PsbUserAgent { get; private set; } = "";
        public DateTime PsbDtCriacao { get; private set; }

        public PushSubscription() { }

        public PushSubscription(int psbId, int? rUsuId, string endpoint, string p256dh, string auth, string userAgent)
        {
            PsbId = psbId;
            R_UsuId = rUsuId;
            PsbEndpoint = endpoint;
            PsbP256dh = p256dh;
            PsbAuth = auth;
            PsbUserAgent = userAgent ?? "";
            PsbDtCriacao = DateTime.Now;
        }
    }
}
