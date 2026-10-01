using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Appointments.Application.Configuration;

namespace Appointments.Infrastructure.Scheduling
{
    /// <summary>
    /// Подключение Quartz к сервису записей: одна задача напоминаний с cron-триггером.
    /// AddQuartzHostedService делает Quartz фоновым сервисом внутри приложения.
    /// </summary>
    public static class SchedulingServiceCollectionExtensions
    {
        public static IServiceCollection AddAppointmentsScheduling(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Cron берём из той же секции, что и остальные настройки уведомлений
            var cron = configuration.GetSection(NotificationOptions.SectionName)["ReminderCron"]
                       ?? new NotificationOptions().ReminderCron;

            services.AddQuartz(quartz =>
            {
                var jobKey = new JobKey(nameof(AppointmentReminderJob));

                quartz.AddJob<AppointmentReminderJob>(options => options.WithIdentity(jobKey));

                quartz.AddTrigger(options => options
                    .ForJob(jobKey)
                    .WithIdentity($"{nameof(AppointmentReminderJob)}-trigger")
                    .WithCronSchedule(cron));
            });

            // Quartz как hosted service: крутится в фоне вместе с приложением
            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

            return services;
        }
    }
}
