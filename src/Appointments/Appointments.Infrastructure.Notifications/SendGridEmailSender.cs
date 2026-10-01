using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using Appointments.Application.Configuration;
using Appointments.Application.Interfaces;
using Appointments.Application.Models;

namespace Appointments.Infrastructure.Notifications
{
    /// <summary>
    /// Отправка писем через SendGrid (продовый провайдер).
    /// Используется, когда в конфигурации задан SendGrid:ApiKey.
    /// </summary>
    public class SendGridEmailSender : IEmailSender
    {
        private readonly SendGridOptions _options;
        private readonly ILogger<SendGridEmailSender> _logger;

        public SendGridEmailSender(IOptions<SendGridOptions> options, ILogger<SendGridEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task SendAppointmentResultEmailAsync(
            string recipientEmail,
            string patientFullName,
            byte[] pdfAttachment,
            CancellationToken cancellationToken = default)
        {
            var message = new SendGridMessage
            {
                From = new EmailAddress(_options.FromEmail, _options.FromName),
                Subject = "Результаты вашего приёма"
            };

            message.AddTo(new EmailAddress(recipientEmail, patientFullName));
            message.AddContent("text/plain",
                $"Здравствуйте, {patientFullName}!\n\nВо вложении — медицинский отчёт по результатам вашего приёма.");

            // AC-2: заключение уходит как PDF-вложение утверждённого формата
            message.AddAttachment(
                "MedicalReport.pdf",
                Convert.ToBase64String(pdfAttachment),
                "application/pdf");

            await SendAsync(message, cancellationToken);
            _logger.LogInformation("Результат приёма отправлен через SendGrid на {Email}.", recipientEmail);
        }

        /// <inheritdoc/>
        public async Task SendAppointmentReminderEmailAsync(
            string recipientEmail,
            AppointmentReminderEmailModel model,
            CancellationToken cancellationToken = default)
        {
            var message = new SendGridMessage
            {
                From = new EmailAddress(_options.FromEmail, _options.FromName),
                Subject = "Напоминание о приёме"
            };

            message.AddTo(new EmailAddress(recipientEmail, model.PatientFullName));

            // AC-2: в письме обязательны ФИО пациента, дата, время, услуга и ФИО врача
            message.AddContent("text/plain",
                $"Здравствуйте, {model.PatientFullName}!\n\n" +
                "Напоминаем о вашем приёме:\n" +
                $"Дата: {model.AppointmentDate:dd.MM.yyyy}\n" +
                $"Время: {model.Timeslot}\n" +
                $"Услуга: {model.ServiceName}\n" +
                $"Врач: {model.DoctorFullName}\n");

            await SendAsync(message, cancellationToken);
            _logger.LogInformation("Напоминание о приёме отправлено через SendGrid на {Email}.", recipientEmail);
        }

        private async Task SendAsync(SendGridMessage message, CancellationToken cancellationToken)
        {
            var client = new SendGridClient(_options.ApiKey);
            var response = await client.SendEmailAsync(message, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Пробрасываем наверх: MassTransit повторит доставку, а после исчерпания — уйдёт в DLQ.
                var body = await response.Body.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"SendGrid вернул {(int)response.StatusCode}: {body}");
            }
        }
    }
}
