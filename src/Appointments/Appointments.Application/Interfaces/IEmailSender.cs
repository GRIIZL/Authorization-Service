using System.Threading;
using System.Threading.Tasks;
using Appointments.Application.Models;

namespace Appointments.Application.Interfaces
{
    /// <summary>
    /// Абстракция отправки писем пациенту.
    /// Application-слой не знает, кто именно отправляет: SendGrid в проде
    /// или лог-заглушка локально.
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>
        /// Отправляет пациенту письмо с результатом приёма и PDF-вложением (US-68).
        /// </summary>
        Task SendAppointmentResultEmailAsync(
            string recipientEmail,
            string patientFullName,
            byte[] pdfAttachment,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Отправляет напоминание о предстоящем приёме (US-63).
        /// </summary>
        Task SendAppointmentReminderEmailAsync(
            string recipientEmail,
            AppointmentReminderEmailModel model,
            CancellationToken cancellationToken = default);
    }
}
