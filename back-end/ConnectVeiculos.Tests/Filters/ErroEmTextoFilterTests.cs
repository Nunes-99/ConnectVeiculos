using System.Collections.Generic;
using ConnectVeiculos.API.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace ConnectVeiculos.Tests.Filters
{
    public class ErroEmTextoFilterTests
    {
        private static ObjectResult Executar(ObjectResult resultado)
        {
            var acao = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
            var contexto = new ResultExecutingContext(acao, new List<IFilterMetadata>(), resultado, controller: null!);
            new ErroEmTextoFilter().OnResultExecuting(contexto);
            return (ObjectResult)contexto.Result;
        }

        private static string? Motivo(ObjectResult r)
            => r.Value!.GetType().GetProperty("error")?.GetValue(r.Value) as string;

        [Fact]
        public void Recusa_Em_Texto_Vira_Json_Com_O_Motivo()
        {
            var r = Executar(new BadRequestObjectResult("Arquivo não é uma imagem válida."));

            r.StatusCode.Should().Be(400);
            Motivo(r).Should().Be("Arquivo não é uma imagem válida.");
            r.ContentTypes.Should().BeEmpty();
        }

        [Fact]
        public void Nao_Encontrado_Em_Texto_Tambem_Vira_Json()
        {
            var r = Executar(new NotFoundObjectResult("Loja não encontrada."));

            r.StatusCode.Should().Be(404);
            Motivo(r).Should().Be("Loja não encontrada.");
        }

        [Fact]
        public void Resposta_De_Sucesso_Em_Texto_Fica_Como_Esta()
        {
            var r = Executar(new OkObjectResult("tudo certo"));

            r.Value.Should().Be("tudo certo");
        }

        [Fact]
        public void Erro_Que_Ja_E_Objeto_Fica_Como_Esta()
        {
            var corpo = new { error = "limite_plano", limite = 5 };
            var r = Executar(new BadRequestObjectResult(corpo));

            r.Value.Should().BeSameAs(corpo);
        }
    }
}
