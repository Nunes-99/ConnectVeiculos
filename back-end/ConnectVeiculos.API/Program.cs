using ConnectVeiculos.Infrastructure.IoC;
using ConnectVeiculos.Infrastructure.Hubs;
using ConnectVeiculos.API.Extensions;
using ConnectVeiculos.API.Filters;
using ConnectVeiculos.API.Middlewares;
using ConnectVeiculos.Application.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Iniciando ConnectVeiculos API");

var builder = WebApplication.CreateBuilder(args);

// Configurar limites de upload de arquivos (max 50MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50MB
});

// Usar Serilog
builder.Host.UseSerilog();

// Carregar variaveis de ambiente para sobrescrever configuracoes
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();

// Configurar Response Compression (Gzip/Brotli)
builder.Services.AddCustomResponseCompression();

// Configurar API Versioning
builder.Services.AddCustomApiVersioning();

// Configurar Health Checks
builder.Services.AddCustomHealthChecks(builder.Configuration);

// Configurar FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<LoginInputModelValidator>();

// Configurar Hangfire (Background Jobs)
builder.Services.AddCustomHangfire();

// Configurar Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Politica para login (5 tentativas por minuto por IP)
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Politica para API geral (100 requests por minuto por usuario)
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });

    // Politica para endpoints publicos (30 requests por minuto por IP)
    options.AddFixedWindowLimiter("public", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "Limite de requisicoes excedido. Tente novamente em alguns instantes.",
            cancellationToken: token);
    };
});

// Configurar SignalR
builder.Services.AddSignalR();

// Configurar Swagger com suporte a JWT e documentacao XML
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ConnectVeiculos API",
        Version = "v1",
        Description = "API para gerenciamento de veiculos, vendas e catalogo",
        Contact = new OpenApiContact
        {
            Name = "ConnectVeiculos",
            Email = "suporte@connectveiculos.com.br"
        }
    });

    // Incluir comentarios XML na documentacao
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando o esquema Bearer. Exemplo: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configurar JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
// Priorizar variavel de ambiente para a chave secreta (mais seguro em producao)
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };

    // Permitir token via query string para SignalR + validar tenant cross-request
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },

        // Multi-tenant: bloqueia uso de token emitido em tenant A num request
        // que veio pelo subdomain de tenant B. Sem isso, alguem com login em
        // acme.X poderia usar o token em demo.X — request seria authenticated
        // mas operaria no banco do tenant errado (vetor de cross-tenant access).
        OnTokenValidated = context =>
        {
            var tenantContext = context.HttpContext.RequestServices
                .GetService<ConnectVeiculos.Core.Interfaces.Tenancy.ITenantContext>();
            if (tenantContext == null || !tenantContext.IsResolved)
                return Task.CompletedTask;

            var tokenTenantClaim = context.Principal?.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tokenTenantClaim))
                return Task.CompletedTask; // token legado sem claim — compat

            if (!int.TryParse(tokenTenantClaim, out var tokenTenantId)
                || tokenTenantId != tenantContext.TenantId)
            {
                context.Fail($"Token emitido para tenant {tokenTenantClaim}, request no tenant {tenantContext.TenantId}");
            }
            return Task.CompletedTask;
        }
    };
});

// Configurar Authorization Policies baseadas em Roles
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Administrador"))
    .AddPolicy("GerenteOrAdmin", policy => policy.RequireRole("Gerente", "Administrador"))
    .AddPolicy("VendedorOrAbove", policy => policy.RequireRole("Vendedor", "Gerente", "Administrador"))
    .AddPolicy("AnyAuthenticated", policy => policy.RequireAuthenticatedUser());

// Honrar X-Forwarded-* enviados pelo nginx (proxy reverso fazendo TLS termination).
// Sem isso, o ASP.NET nao sabe que o request original veio via HTTPS (gera URLs http://
// em redirects/callbacks OAuth, e cookies "Secure" podem nao ser enviados). KnownNetworks/
// KnownProxies sao limpos porque o nginx esta numa rede Docker, nao em loopback.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configurar Injecao de Dependencia com SQLite
builder.Services.WireUpDependencies(builder.Configuration);

var app = builder.Build();

// Aplicar ForwardedHeaders o mais cedo possivel no pipeline, antes de qualquer
// middleware que dependa de scheme/host (HttpsRedirection, Authentication, CORS).
app.UseForwardedHeaders();

// Inicializar banco de dados SQLite (cria as tabelas se nao existirem)
app.UseInitializeDatabase();

// Multi-tenant: garante que master e cada tenant tem schema atualizado.
// Roda uma vez no startup. Se nao tiver tenants registrados, segue em modo
// single-tenant (fallback DefaultConnection) ate o primeiro tenant ser criado.
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<ConnectVeiculos.Infrastructure.Tenancy.TenantsMigrationsRunner>();
    await runner.RunAsync();
}

// Configure the HTTP request pipeline.
// Swagger habilitado em todos os ambientes para facilitar testes
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConnectVeiculos API v1");
});

app.UseHttpsRedirection();

// Response Compression (antes de qualquer middleware que gere conteudo)
app.UseResponseCompression();

app.UseRouting();

// Multi-tenant: resolve o tenant pelo subdomain do request e popula o
// ITenantContext. Skip de rotas que nao precisam (health, swagger, ACME)
// esta dentro do proprio middleware. Plugado ANTES de Authentication
// pra que claims tenham acesso ao tenant resolvido.
app.UseTenantResolution();

// CORS configuravel via variavel de ambiente (ALLOWED_ORIGINS)
var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")?.Split(',')
    ?? new[] { "http://localhost:4200", "http://localhost:5173" };

app.UseCors(options => options
    .WithOrigins(allowedOrigins)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());

// Rate Limiting
app.UseRateLimiter();

// Middlewares
app.UseErrorHandlingMiddleware();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Mapear Hubs SignalR
app.MapHub<NotificacaoHub>("/hubs/notificacoes");
app.MapHub<CatalogoHub>("/hubs/catalogo");

// Mapear Health Checks
app.MapCustomHealthChecks();

// Configurar Hangfire Dashboard e Jobs
app.UseCustomHangfire();

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicacao terminou inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}
