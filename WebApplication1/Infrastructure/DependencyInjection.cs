using Domain.Entites;
using Infrastructure.Configuration;
using Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            // 1. Bind and validate JWT settings used by both HTTP APIs and SignalR hubs.
            var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                ?? throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}' is missing.");

            if (string.IsNullOrWhiteSpace(jwtOptions.Key) ||
                string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
                string.IsNullOrWhiteSpace(jwtOptions.Audience))
            {
                throw new InvalidOperationException("JWT Key, Issuer, and Audience must be configured.");
            }

            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
            services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
            services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));

            services
                .AddAuthentication(options =>
                {
                    // REST APIs and SignalR continue to authenticate with Bearer tokens.
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            // Browser WebSockets cannot reliably send Authorization headers.
                            // Accept the query-string token only for our SignalR endpoints.
                            if (!string.IsNullOrWhiteSpace(accessToken) &&
                                (path.StartsWithSegments("/chatHub") ||
                                 path.StartsWithSegments("/notificationHub") ||
                                 path.StartsWithSegments("/hubs/chat") ||
                                 path.StartsWithSegments("/hubs/notifications")))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        }
                    };
                })
                .AddCookie("MvcCookie", options =>
                {
                    options.LoginPath = "/Auth/Login";
                    options.AccessDeniedPath = "/Auth/Login";
                    options.Cookie.Name = "Connectly.Mvc.Session";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.SlidingExpiration = true;
                    if (!environment.IsDevelopment())
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Events.OnRedirectToLogin = context =>
                    {
                        // API callers must receive a status code, never an HTML redirect.
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    };
                });

            services.AddAuthorization();

            // 2. Database Connection
            var connectionString = configuration.GetConnectionString("Conn")
                ?? throw new InvalidOperationException("Connection string 'Conn' was not found.");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString, b =>
                    b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                    .ConfigureWarnings(w => w.Ignore(
                        CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

            // 3. Identity Configuration
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
          
            // 3. Register Hosted Background Worker
           
            return services;
        }
    }
}
