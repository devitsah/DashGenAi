using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Application.PromptProcessing;
using AI_Dashboard.Application.PromptProcessing.Pipeline;
using AI_Dashboard.Application.PromptProcessing.Pipeline.Steps;
using AI_Dashboard.Infrastructure.AI;
using AI_Dashboard.Infrastructure.LayoutEngine;
using AI_Dashboard.Infrastructure.Postgres;
using AI_Dashboard.Infrastructure.Postgres.Repositories;
using AI_Dashboard.Infrastructure.SchemaIntrospection;
using AI_Dashboard.Infrastructure.QueryExecution;
using System.Text;
using AI_Dashboard.Api.Services;
using AI_Dashboard.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.Npgsql;
 
var builder = WebApplication.CreateBuilder(args);
 
// --- Auth / Identity ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserSessionRepository, UserSessionRepository>();
 
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.SigningKey = JwtKeyProvider.GetOrCreateKey(builder.Environment.ContentRootPath);
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtOptions));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer, ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
    });
builder.Services.AddAuthorization();
 
// --- NpgsqlDataSource with pgvector support (shared by EF Core + raw Npgsql) ---
var dataSourceBuilder = new NpgsqlDataSourceBuilder(
        builder.Configuration.GetConnectionString("Postgres")!);
dataSourceBuilder.UseVector();
var npgsqlDataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(npgsqlDataSource);
 
// --- EF Core ---
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(npgsqlDataSource));
 
// --- Repositories ---
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IWidgetRepository, WidgetRepository>();
builder.Services.AddScoped<IPromptRepository, PromptRepository>();
builder.Services.AddScoped<IQueryRepository, QueryRepository>();
 
// --- Schema introspection ---
builder.Services.AddSingleton<SchemaCache>();
builder.Services.AddSingleton<EmbeddingCache>();
builder.Services.AddSingleton<IEmbeddingCache>(sp => sp.GetRequiredService<EmbeddingCache>());
builder.Services.AddScoped<ISchemaIntrospectionService, PostgresSchemaIntrospectionService>();
builder.Services.AddSingleton<ISchemaGraph, SchemaGraph>();
builder.Services.AddScoped<ISchemaChunkStore, PostgresSchemaChunkStore>();
builder.Services.AddHostedService<SchemaPreWarmService>();
 
// --- Schema introspection options ---
var introspectionOptions = builder.Configuration
    .GetSection("SchemaIntrospection")
    .Get<SchemaIntrospectionOptions>() ?? new SchemaIntrospectionOptions();
builder.Services.AddSingleton<ISchemaIntrospectionOptions>(introspectionOptions);
 
// --- Prompt guard ---
builder.Services.AddSingleton<IPromptGuard, PromptGuard>();
 
// --- Layout engine (pure heuristics, no AI) ---
builder.Services.AddSingleton<ILayoutEngine, GridLayoutEngine>();
 
// --- Query execution + SQL validation ---
builder.Services.AddSingleton<QueryResultCache>();
builder.Services.AddScoped<IQueryExecutor, PostgresQueryExecutor>();
builder.Services.AddScoped<ISqlValidator, PostgresSqlValidator>();
 
// --- Ollama: multi-model (qwen3:1.7b intent, nomic-embed-text RAG, qwen2.5-coder:14b SQL) ---
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
// Register IOllamaModelOptions so Application steps get model names without referencing Infrastructure
builder.Services.AddSingleton<IOllamaModelOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value);
var ollamaTimeout = builder.Configuration.GetSection("Ollama").GetValue<int>("TimeoutSeconds", 300);
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>(c =>
    c.Timeout = TimeSpan.FromSeconds(ollamaTimeout));
 
// --- Pipeline steps (named, not IEnumerable — orchestrator receives them directly) ---
builder.Services.AddScoped<PromptGuardStep>();
builder.Services.AddScoped<SemanticIntentStep>();
builder.Services.AddScoped<SchemaResolutionStep>();
builder.Services.AddScoped<TableColumnConfirmationStep>();
builder.Services.AddScoped<ClarificationStep>();
builder.Services.AddScoped<VisualizationCompatibilityStep>();
builder.Services.AddScoped<SqlGenerationStep>();
builder.Services.AddScoped<JoinTenantGuardStep>();
builder.Services.AddScoped<SqlRepairStep>();
builder.Services.AddScoped<VisualizationRecommendationStep>();
builder.Services.AddScoped<LayoutGenerationStep>();
builder.Services.AddScoped<SuggestionsStep>();
builder.Services.AddScoped<PromptPipelineOrchestrator>();
// --- MediatR ---
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ProcessPromptCommand).Assembly));
 
// --- API ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Enter: Bearer {your JWT token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p =>
        p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));
 
var app = builder.Build();
 
// CORS must be first — before authentication, authorization, and any middleware
// that could return a response. Browser preflight OPTIONS requests must get
// CORS headers or the login/register calls fail before they reach the API.
app.UseCors();
app.UseAuthentication();
 
// Pre-warm all three models into Ollama VRAM on startup — parallel so total cost
// is max(load_time) not sum(load_time).
_ = Task.Run(async () =>
{
    try
    {
        var ollama = app.Services.GetRequiredService<IOllamaClient>();
        var opts   = app.Services.GetRequiredService<IOllamaModelOptions>();
        await Task.WhenAll(
            ollama.GenerateAsync("warmup", "ping", 1),                               // qwen3:1.7b
            ollama.EmbedAsync("warmup"),                                             // nomic-embed-text
            ollama.GenerateWithModelAsync(opts.CoderModel, "warmup", "ping", 1, default) // qwen2.5-coder:14b
        );
    }
    catch { /* non-fatal */ }
});
 
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
 
app.UseMiddleware<AI_Dashboard.Api.Middleware.ExceptionHandlingMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.Run();