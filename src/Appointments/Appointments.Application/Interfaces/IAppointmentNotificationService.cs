using System.Threading;
using System.Threading.Tasks;

namespace Appointments.Application.Interfaces
{
    /// <summary>
    /// Фасад уведомлений сервиса записей.
    /// Инкапсулирует сценарии US-68 (результат на email) и US-63 (напоминание).
    /// </summary>
    public interface IAppointmentNotificationService
    {
        /// <summary>
        /// US-68: формирует PDF через Documents API и отправляет его пациенту.
        /// Вызывается консьюмером события AppointmentResultReadyEvent.
        /// </summary>
        Task SendResultEmailAsync(Guid appointmentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// US-63: рассылает напоминания по всем приёмам на завтра.
        /// Вызывается Quartz-задачей по cron-расписанию.
        /// </summary>
        Task SendRemindersForTomorrowAsync(CancellationToken cancellationToken = default);
    }
}
