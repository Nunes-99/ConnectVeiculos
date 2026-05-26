namespace ConnectVeiculos.Core.Exceptions
{
    // Operacao bloqueada porque tenant atingiu o limite do plano. Filter no API
    // converte em 403 JSON pra que o frontend mostre toast com link de upgrade.
    public class LimitePlanoException : Exception
    {
        public string Recurso { get; }
        public int Limite { get; }
        public int Atual { get; }
        public string PlanoNome { get; }

        public LimitePlanoException(string recurso, int limite, int atual, string planoNome)
            : base($"Limite do plano {planoNome} atingido para {recurso}: {atual}/{limite}.")
        {
            Recurso = recurso;
            Limite = limite;
            Atual = atual;
            PlanoNome = planoNome;
        }
    }
}
