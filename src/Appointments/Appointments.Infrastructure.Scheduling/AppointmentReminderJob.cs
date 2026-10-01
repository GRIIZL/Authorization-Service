using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Appointments.Application.Interfaces;

namespace Appointments.Infrastructure.Scheduling
{
    /// <summary>
    /// US-63: задача напоминаний о приёмах за день до визита.
    /// Запускается Quartz по cron-маске из конфигурации (секция "Notifications").
    /// </summary>
    public class AppointmentReminderJob : IJob
    {
        private readonly IAppointmentNotificationService _notificationService;
        private readonly ILogger<AppointmentReminderJob> _logger;

        public AppointmentReminderJob(
            IAppointmentNotificationService notificationService,
            ILogger<AppointmentReminderJob> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("Quartz: запуск рассылки напоминаний о приёмах на завтра.");

            await _notificationService.SendRemindersForTomorrowAsync(context.CancellationToken);
        }
    }
}
