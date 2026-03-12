using KnowledgeHub.API.Extensions;
using KnowledgeHub.API.Hubs;
using KnowledgeHub.API.Middleware;
using KnowledgeHub.Core.Interfaces.Services;
using KnowledgeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/knowledgehub-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Add services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddDatabase(builder.Configuration, builder.Environment);
    builder.Services.AddIdentityServices();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddSwaggerDocumentation();
    builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);
    builder.Services.AddCorsPolicies(builder.Configuration);
    builder.Services.AddFluentValidationServices();
    builder.Services.AddRateLimitingPolicies();
    builder.Services.AddApiVersioningServices();

    var app = builder.Build();

    // Auto-migrate in development
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    // Seed roles
    {
        using var scope = app.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        string[] roles = ["Admin", "User"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role });
            }
        }
    }

    // Seed demo data in development
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
        await seeder.SeedAsync();
    }

    // Middleware pipeline
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseCors("AllowAngular");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<ChatHub>("/hubs/chat");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make the implicit Program class accessible for integration tests
public partial class Program { }
