namespace Appointments.Application.Configuration
{
    /// <summary>
    /// Настройки уведомлений сервиса записей (секция "Notifications").
    /// </summary>
    public class NotificationOptions
    {
        public const string SectionName = "Notifications";

        /// <summary>Базовый адрес Documents API — он верстает PDF и кладёт его в MinIO.</summary>
        public string DocumentsApiBaseUrl { get; set; } = "http://localhost:5270";

        /// <summary>
        /// Cron-маска Quartz для задачи напоминаний (US-63).
        /// По умолчанию — ежедневно в 09:00.
        /// </summary>
        public string ReminderCron { get; set; } = "0 0 9 * * ?";

        /// <summary>
        /// Заглушка ФИО пациента: пока Profiles не отдаёт имя в сервис записей,
        /// в письмо подставляется это значение (TODO: заменить на реальные данные).
        /// </summary>
        public string PatientNamePlaceholder { get; set; } = "Patient";

        /// <summary>Заглушка ФИО врача (TODO: брать из Profiles API).</summary>
        public string DoctorNamePlaceholder { get; set; } = "Doctor";

        /// <summary>Заглушка названия услуги (TODO: брать из Services API).</summary>
        public string ServiceNamePlaceholder { get; set; } = "Medical service";
    }
}
