namespace Appointments.Application.Configuration
{
    /// <summary>
    /// Настройки SendGrid (секция "SendGrid").
    /// Если ApiKey пуст — используется локальная лог-заглушка вместо реальной отправки.
    /// </summary>
    public class SendGridOptions
    {
        public const string SectionName = "SendGrid";

        /// <summary>API-ключ SendGrid. Пусто — письма пишутся в лог (локальная разработка).</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Адрес отправителя.</summary>
        public string FromEmail { get; set; } = "noreply@clinic.local";

        /// <summary>Отображаемое имя отправителя.</summary>
        public string FromName { get; set; } = "Innowise Clinic";
    }
}
