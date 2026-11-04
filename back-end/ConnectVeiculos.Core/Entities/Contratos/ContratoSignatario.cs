namespace ConnectVeiculos.Core.Entities.Contratos
{
    /// <summary>
    /// Quem assina o contrato e as evidencias da assinatura (data, IP,
    /// navegador, como confirmou a identidade e a imagem da rubrica).
    /// O link publico usa um token aleatorio: guardamos o hash (para achar)
    /// e o token cifrado (para mostrar o link de novo a loja).
    /// </summary>
    public class ContratoSignatario
    {
        public const string PapelLoja = "LOJA";
        public const string PapelCliente = "CLIENTE";

        public const string StatusPendente = "PENDENTE";
        public const string StatusAssinado = "ASSINADO";

        /// <summary>Codigo de 6 digitos enviado por e-mail, digitado no link.</summary>
        public const string MetodoCodigoEmail = "CODIGO_EMAIL";
        /// <summary>Assinou no aparelho da loja, conferido por um usuario.</summary>
        public const string MetodoPresencial = "PRESENCIAL";
        /// <summary>Representante da loja, logado no sistema.</summary>
        public const string MetodoUsuario = "USUARIO_SISTEMA";

        public const int MaximoTentativasCodigo = 5;

        public int CsgId { get; private set; }
        public int R_CtrId { get; private set; }
        public string CsgPapel { get; private set; } = PapelCliente;
        public string CsgNome { get; private set; } = string.Empty;
        public string? CsgDocumento { get; private set; }
        public string? CsgEmail { get; private set; }
        public string? CsgTelefone { get; private set; }

        public string? CsgTokenHash { get; private set; }
        public string? CsgTokenProtegido { get; private set; }

        public string? CsgCodigoHash { get; private set; }
        public DateTime? CsgCodigoExpira { get; private set; }
        public int CsgTentativas { get; private set; }

        public string CsgStatus { get; private set; } = StatusPendente;
        public string? CsgMetodo { get; private set; }
        public string? CsgNomeAssinado { get; private set; }
        public string? CsgDocumentoAssinado { get; private set; }
        public string? CsgImagem { get; private set; }
        public string? CsgIp { get; private set; }
        public string? CsgNavegador { get; private set; }
        public int? R_UsuIdConferente { get; private set; }
        public string? CsgConferente { get; private set; }
        public DateTime? CsgDtVisualizacao { get; private set; }
        public DateTime? CsgDtAssinatura { get; private set; }

        public ContratoSignatario() { }

        public ContratoSignatario(string papel, string nome, string? documento, string? email, string? telefone)
        {
            CsgPapel = papel;
            CsgNome = nome.Trim();
            CsgDocumento = documento;
            CsgEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            CsgTelefone = telefone;
        }

        public bool Assinou => CsgStatus == StatusAssinado;

        public void DefinirToken(string hash, string protegido)
        {
            CsgTokenHash = hash;
            CsgTokenProtegido = protegido;
        }

        public void Visualizou()
        {
            CsgDtVisualizacao ??= DateTime.UtcNow;
        }

        public void NovoCodigo(string hash, DateTime expira)
        {
            CsgCodigoHash = hash;
            CsgCodigoExpira = expira;
            CsgTentativas = 0;
        }

        public void ErrouCodigo() => CsgTentativas++;

        public void Assinar(string metodo, string nome, string? documento, string imagem, string? ip, string? navegador,
            int? conferenteId = null, string? conferente = null)
        {
            CsgStatus = StatusAssinado;
            CsgMetodo = metodo;
            CsgNomeAssinado = nome.Trim();
            CsgDocumentoAssinado = documento;
            CsgImagem = imagem;
            CsgIp = ip;
            CsgNavegador = navegador is { Length: > 300 } ? navegador[..300] : navegador;
            R_UsuIdConferente = conferenteId;
            CsgConferente = conferente;
            CsgDtAssinatura = DateTime.UtcNow;
            CsgCodigoHash = null;
        }
    }
}
