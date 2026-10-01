using System.Threading.Tasks;
using MassTransit;
using Appointments.Application.Interfaces;
using Shared.Events;

namespace Appointments.Infrastructure.RabbitMQ
{
    /// <summary>
    /// Consumer события о готовности результата приёма (US-68).
    /// Это и есть фоновый обработчик: MassTransit держит шину как hosted service,
    /// а доставку/повторы/DLQ берёт на себя.
    /// </summary>
    public class AppointmentResultReadyEventConsumer : IConsumer<AppointmentResultReadyEvent>
    {
        private readonly IAppointmentNotificationService _notificationService;

        public AppointmentResultReadyEventConsumer(IAppointmentNotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public Task Consume(ConsumeContext<AppointmentResultReadyEvent> context)
        {
            // Если PDF или SendGrid упадут, MassTransit повторит доставку,
            // а после исчерпания попыток сообщение осядет в _error queue.
            return _notificationService.SendResultEmailAsync(
                context.Message.AppointmentId,
                context.CancellationToken);
        }
    }
}
