using ConnectVeiculos.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ConnectVeiculos.API.Filters
{
    // Converte LimitePlanoException em 403 JSON estruturado pra o frontend mostrar
    // toast com link de upgrade. Sem isso, exception vira 500 e perde o contexto
    // (qual recurso, qual limite, qual plano).
    public sealed class LimitePlanoExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is not LimitePlanoException ex) return;

            context.Result = new ObjectResult(new
            {
                error = "limite_plano",
                recurso = ex.Recurso,
                limite = ex.Limite,
                atual = ex.Atual,
                plano = ex.PlanoNome,
                mensagem = ex.Message
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            context.ExceptionHandled = true;
        }
    }
}
