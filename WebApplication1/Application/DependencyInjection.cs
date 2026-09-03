using Application.BackgroundJobs;
using Application.Interfaces;
using Application.Interfaces.unitofwork;
using Application.Repositories;
using Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IEmailOtpService, EmailOtpService>();
            services.AddScoped<IFriendshipService, FriendshipService>();
            services.AddScoped<IPostService, PostService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
            services.AddScoped<ISearchService, SearchService>();
            services.AddScoped<IRealtimeNotificationService, RealtimeNotificationService>();
            services.AddScoped<IRealtimeChatService, RealtimeChatService>();
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
            services.AddSingleton<IEmailSender, SmtpEmailSender>();

            return services;
        }
    }
}
