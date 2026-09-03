using Application.Interfaces;
using Application.Interfaces.unitofwork;
using Domain.Entites;
using Domain.Enums;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.BackgroundJobs
{
    public class BirthdayNotificationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BirthdayNotificationWorker> _logger;

        // Daily run target: 08:00 UTC
        private readonly TimeSpan _scheduledRunTime = new(8, 0, 0);

        public BirthdayNotificationWorker(
            IServiceProvider serviceProvider,
            ILogger<BirthdayNotificationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Birthday Notification Worker started.");

            try
            {
                await ProcessDailyBirthdaysAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Startup birthday check failed.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = CalculateDelayUntilNextRun();
                _logger.LogInformation(
                    "Next birthday check scheduled in {Hours} hours and {Minutes} minutes.",
                    delay.Hours, delay.Minutes);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // Graceful shutdown requested
                }

                try
                {
                    await ProcessDailyBirthdaysAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing daily birthday notifications.");
                }
            }

            _logger.LogInformation("Birthday Notification Worker is stopping.");
        }

        private async Task ProcessDailyBirthdaysAsync(CancellationToken ct)
        {
            _logger.LogInformation("Executing daily birthday check for {Date}...", DateTime.UtcNow.ToShortDateString());

            // BackgroundService is a Singleton, so we create a new scope to resolve Scoped services
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // 1. Query users born today (matching Month and Day)
            var birthdayUsers = await context.Users
                .AsNoTracking()
                .Where(u => u.DateOfBirth.Month == today.Month && u.DateOfBirth.Day == today.Day)
                .Select(u => new
                {
                    u.Id,
                    u.DisplayName,
                    u.ProfilePictureUrl
                })
                .ToListAsync(ct);

            if (!birthdayUsers.Any())
            {
                _logger.LogInformation("No birthdays found for today ({Date}).", today);
                return;
            }

            _logger.LogInformation("Found {Count} user(s) celebrating birthdays today.", birthdayUsers.Count);

            var notificationRepo = unitOfWork.Repository<Notification, int>();
            var todayDate = DateTime.UtcNow.Date;

            foreach (var birthdayPerson in birthdayUsers)
            {
                // 2. Query accepted friends using your Friendship repository
                var friendIds = await unitOfWork.Friendships.GetAcceptedFriendIdsAsync(birthdayPerson.Id, ct);

                if (!friendIds.Any())
                    continue;

                // 3. Prevent duplicate notifications if the service restarts today
                var existingNotifications = await notificationRepo.FindAsync(n =>
                    n.TriggeredById == birthdayPerson.Id &&
                    n.Type == NotificationType.BirthdayReminder &&
                    n.CreatedAt.Date == todayDate, ct);

                var alreadyNotifiedUserIds = existingNotifications.Select(n => n.RecipientId).ToHashSet();

                foreach (var friendId in friendIds)
                {
                    if (alreadyNotifiedUserIds.Contains(friendId))
                        continue;

                    await dispatcher.DispatchAsync(
                        friendId,
                        birthdayPerson.Id,
                        NotificationType.BirthdayReminder,
                        $"Today is {birthdayPerson.DisplayName}'s birthday! Wish them a happy birthday! 🎂",
                        $"/Profile?userId={Uri.EscapeDataString(birthdayPerson.Id)}",
                        ct);

                    alreadyNotifiedUserIds.Add(friendId);
                }
            }

            await unitOfWork.CompleteAsync(ct);
            _logger.LogInformation("Daily birthday notifications processed and dispatched successfully.");
        }

        private TimeSpan CalculateDelayUntilNextRun()
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.Add(_scheduledRunTime);

            if (now >= nextRun)
            {
                // Target tomorrow at 08:00 UTC if today's time has already passed
                nextRun = nextRun.AddDays(1);
            }

            return nextRun - now;
        }
    }
}
