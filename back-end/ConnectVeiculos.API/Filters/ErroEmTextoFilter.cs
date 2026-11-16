using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ConnectVeiculos.API.Filters
{
    // Varios endpoints recusam com BadRequest("motivo") / NotFound("motivo"), que
    // saem como text/plain. O frontend usa fetch e espera JSON: ao receber texto,
    // o Angular descarta o corpo e o usuario via "Unexpected token 'A', "Arquivo
    // na"... is not valid JSON" no lugar do motivo. Aqui o texto vira
    // { error: "motivo" }, o formato que o interceptor de erros ja sabe ler.
    public sealed class ErroEmTextoFilter : IAlwaysRunResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is not ObjectResult { Value: string motivo } resultado) return;

            var status = resultado.StatusCode ?? StatusCodes.Status200OK;
            if (status < 400) return;

            resultado.Value = new { error = motivo };
            // O tipo declarado continuava string e o formatador escolhia text/plain.
            resultado.DeclaredType = null;
            resultado.ContentTypes.Clear();
        }

        public void OnResultExecuted(ResultExecutedContext context) { }
    }
}
