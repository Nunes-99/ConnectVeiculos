using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ConnectVeiculos.Infrastructure.Middlewares
{
    public class ConnectionStringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public ConnectionStringMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            context.Items["ConnectionString"] = connectionString;

            await _next(context);
        }
    }
}
