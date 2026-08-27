using System.Reflection;
using LexPCImages.Modules.Optimizer.Application.DependencyInjection;
using LexPCImages.Modules.Optimizer.Infrastructure.DependencyInjection;
using LexPCImages.Modules.Optimizer.Presentation;
using LexPCImages.Modules.Optimizer.Presentation.Controllers;
using LexPCImages.Shared;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, lc) => lc.ReadFrom.Configuration(context.Configuration));

builder.Services.AddOptimizerApplication();
builder.Services.AddOptimizerInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddApplicationPart(Assembly.GetAssembly(typeof(OptimizerController))!);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4300")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

builder.Services.AddSingleton<IModuleRegistration, OptimizerModule>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<LexPCImages.API.Middleware.GlobalExceptionMiddleware>();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapHealthChecks("/health");

foreach (var module in app.Services.GetServices<IModuleRegistration>())
{
    module.MapEndpoints(app);
}

app.Run();

public partial class Program;
