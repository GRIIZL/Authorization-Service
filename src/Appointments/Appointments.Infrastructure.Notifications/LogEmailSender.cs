using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Appointments.Application.Interfaces;
using Appointments.Application.Models;

namespace Appointments.Infrastructure.Notifications
{
    /// <summary>
    /// Локальная заглушка отправки писем: пишет содержимое в лог.
    /// Используется, когда SendGrid:ApiKey не задан, — чтобы можно было
    /// проверить весь поток уведомлений без реального провайдера.
    /// </summary>
    public class LogEmailSender : IEmailSender
    {
        private readonly ILogger<LogEmailSender> _logger;

        public LogEmailSender(ILogger<LogEmailSender> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task SendAppointmentResultEmailAsync(
            string recipientEmail,
            string patientFullName,
            byte[] pdfAttachment,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[LOG EMAIL] Результат приёма для {Email} ({Name}), PDF вложен ({Size} байт).",
                recipientEmail, patientFullName, pdfAttachment.Length);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task SendAppointmentReminderEmailAsync(
            string recipientEmail,
            AppointmentReminderEmailModel model,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[LOG EMAIL] Напоминание для {Email}: {Name} | {Date:dd.MM.yyyy} | {Timeslot} | {Service} | {Doctor}",
                recipientEmail,
                model.PatientFullName,
                model.AppointmentDate,
                model.Timeslot,
                model.ServiceName,
                model.DoctorFullName);

            return Task.CompletedTask;
        }
    }
}
