using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Appointments.Application.Configuration;
using Appointments.Application.Interfaces;
using Appointments.Application.Models;
using Appointments.Domain;

namespace Appointments.Application.Services
{
    /// <summary>
    /// Фасад уведомлений: объединяет репозиторий, Documents API и отправку почты.
    /// Сценарий US-68 вызывается консьюмером события, US-63 — Quartz-задачей.
    /// </summary>
    public class AppointmentNotificationService : IAppointmentNotificationService
    {
        private readonly IAppointmentRepository _repository;
        private readonly IDocumentsApiClient _documentsApi;
        private readonly IEmailSender _emailSender;
        private readonly NotificationOptions _options;
        private readonly ILogger<AppointmentNotificationService> _logger;

        public AppointmentNotificationService(
            IAppointmentRepository repository,
            IDocumentsApiClient documentsApi,
            IEmailSender emailSender,
            IOptions<NotificationOptions> options,
            ILogger<AppointmentNotificationService> logger)
        {
            _repository = repository;
            _documentsApi = documentsApi;
            _emailSender = emailSender;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task SendResultEmailAsync(Guid appointmentId, CancellationToken cancellationToken = default)
        {
            var appointment = await _repository.GetByIdAsync(appointmentId, cancellationToken);
            var result = await _repository.GetResultByAppointmentIdAsync(appointmentId, cancellationToken);

            if (appointment == null || result == null)
            {
                _logger.LogWarning("Заключение для приёма {AppointmentId} не найдено, письмо не отправлено.", appointmentId);
                return;
            }

            if (string.IsNullOrWhiteSpace(appointment.PatientEmail))
            {
                _logger.LogWarning("У приёма {AppointmentId} не заполнен email пациента, письмо не отправлено.", appointmentId);
                return;
            }

            // AC-2: PDF верстает Documents API (QuestPDF) — данные берём из актуальной версии заключения.
            var pdf = await _documentsApi.GenerateMedicalReportAsync(
                appointmentId,
                _options.PatientNamePlaceholder, // TODO: заменить на ФИО из Profiles API
                result.Complaints,
                result.Conclusion,
                result.Recommendations,
                cancellationToken);

            await _emailSender.SendAppointmentResultEmailAsync(
                appointment.PatientEmail,
                _options.PatientNamePlaceholder,
                pdf,
                cancellationToken);

            _logger.LogInformation("Результат приёма {AppointmentId} отправлен на {Email}.", appointmentId, appointment.PatientEmail);
        }

        /// <inheritdoc/>
        public async Task SendRemindersForTomorrowAsync(CancellationToken cancellationToken = default)
        {
            var tomorrow = DateTime.UtcNow.Date.AddDays(1);
            var appointments = await _repository.GetForReminderAsync(tomorrow, cancellationToken);

            foreach (var appointment in appointments)
            {
                try
                {
                    // AC-2: письмо обязано содержать ФИО пациента, дату, время, услугу и ФИО врача.
                    var model = new AppointmentReminderEmailModel
                    {
                        PatientFullName = _options.PatientNamePlaceholder, // TODO: Profiles API
                        AppointmentDate = appointment.Date,
                        Timeslot = appointment.Timeslot,
                        ServiceName = _options.ServiceNamePlaceholder,     // TODO: Services API
                        DoctorFullName = _options.DoctorNamePlaceholder   // TODO: Profiles API
                    };

                    await _emailSender.SendAppointmentReminderEmailAsync(appointment.PatientEmail, model, cancellationToken);

                    // Помечаем факт отправки, чтобы повторный запуск задачи не отправил письмо дважды.
                    appointment.ReminderSentAt = DateTime.UtcNow;
                    appointment.UpdatedAt = DateTime.UtcNow;
                    await _repository.UpdateAsync(appointment, cancellationToken);

                    _logger.LogInformation("Напоминание о приёме {AppointmentId} отправлено на {Email}.", appointment.Id, appointment.PatientEmail);
                }
                catch (Exception exception)
                {
                    // Сбой по одному приёму не должен прерывать рассылку остальным.
                    _logger.LogError(exception, "Не удалось отправить напоминание о приёме {AppointmentId}.", appointment.Id);
                }
            }
        }
    }
}
