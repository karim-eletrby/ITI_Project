using Application;
using Application.BackgroundJobs;
using Application.Common;
using Application.Hubs;
using Domain.Common;
using Domain.Entites;
using Infrastructure;
using Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Configuration;
using Presentation.Middleware;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
    ProductionConfigurationValidator.Validate(builder.Configuration);

// 1. Add MVC Controllers with Views
builder.Services.AddControllersWithViews()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var fieldErrors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            var response = new ErrorResponse
            {
                Success = false,
                Message = "Please fix the errors below.",
                FieldErrors = fieldErrors
            };

            return new BadRequestObjectResult(response);
        };
    });
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 3. Register SignalR (hub implementations live in Application layer)
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576_000);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1_048_576_000;
});

// 4. Register Layer Services (Infrastructure & Application)
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices();

// 5. Register Background Workers
builder.Services.AddHostedService<BirthdayNotificationWorker>();

var app = builder.Build();

// 6. Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        if (context.Database.IsSqlServer())
        {
            var pending = await context.Database.GetPendingMigrationsAsync();
            if (pending.Any())
                logger.LogInformation("Applying pending migrations: {Migrations}", string.Join(", ", pending));

            await context.Database.MigrateAsync();
            logger.LogInformation("Database schema is up to date.");
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await LegacyUsernameRepair.RunAsync(userManager, logger);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed. Picture uploads and posts require an up-to-date schema. Run: dotnet ef database update --project Infrastructure --startup-project Presentation");
        throw;
    }
}

// Configure the HTTP request pipeline
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;
        if (path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.ContentType = "video/mp4";
        else if (path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.ContentType = "video/webm";
        else if (path.EndsWith(".mov", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.ContentType = "video/quicktime";
    }
});

app.UseRouting();

// 7. Authentication & Authorization Middleware
app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.MapStaticAssets();
}

// 8. Map SignalR Hubs
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<ChatHub>("/chatHub");

// 9. Map API Controllers (CRITICAL: Routes your /api/... endpoints)
app.MapControllers();

// 10. Map Default MVC Route (CRITICAL: Routes your browser HTML pages)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Feed}/{action=Index}/{id?}");

app.Run();
