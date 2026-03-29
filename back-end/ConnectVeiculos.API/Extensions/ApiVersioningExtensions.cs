using Asp.Versioning;

namespace ConnectVeiculos.API.Extensions
{
    public static class ApiVersioningExtensions
    {
        public static IServiceCollection AddCustomApiVersioning(this IServiceCollection services)
        {
            services.AddApiVersioning(options =>
            {
                // Versao padrao quando nao especificada
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;

                // Reportar versoes suportadas no header
                options.ReportApiVersions = true;

                // Ler versao de multiplas fontes
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),           // /api/v1/...
                    new QueryStringApiVersionReader("api-version"), // ?api-version=1.0
                    new HeaderApiVersionReader("X-Api-Version")     // Header: X-Api-Version: 1.0
                );
            })
            .AddApiExplorer(options =>
            {
                // Formato da versao no Swagger
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            return services;
        }
    }
}
