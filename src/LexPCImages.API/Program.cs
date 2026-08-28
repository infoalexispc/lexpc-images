using LexPCImages.API.Configuration;
using LexPCImages.API.Middleware;
using LexPCImages.Modules.Optimizer.Application.DependencyInjection;
using LexPCImages.Modules.Optimizer.Infrastructure.DependencyInjection;
using LexPCImages.Modules.Optimizer.Presentation;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));

// --- Módulos ---
builder.Services.AddOptimizerApplication();
builder.Services.AddOptimizerInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddOptimizerPresentation();

// --- Preocupaciones transversales del host ---
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var corsSection = builder.Configuration.GetSection(FrontendCorsOptions.SectionName);
builder.Services
    .AddOptions<FrontendCorsOptions>()
    .Bind(corsSection)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// El origen ya no está en el código: cada entorno declara el suyo en Cors:AllowedOrigins.
var allowedOrigins = corsSection.Get<FrontendCorsOptions>()?.AllowedOrigins ?? [];
builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsOptions.PolicyName,
    policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors(FrontendCorsOptions.PolicyName);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapHealthChecks("/health");
app.MapOptimizerEndpoints();

app.Run();

public partial class Program;
