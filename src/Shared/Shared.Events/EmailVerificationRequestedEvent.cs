namespace Shared.Events
{
    /// <summary>
    /// Событие запроса письма подтверждения email.
    /// Публикуется Auth сразу после регистрации аккаунта: сама отправка письма
    /// выполняется асинхронно консьюмером, чтобы регистрация не зависела
    /// от доступности SMTP-сервера.
    /// </summary>
    public class EmailVerificationRequestedEvent
    {
        /// <summary>Адрес получателя письма (нормализован в нижний регистр).</summary>
        public string RecipientEmail { get; set; } = string.Empty;

        /// <summary>
        /// Готовая ссылка подтверждения, собранная на стороне Auth из конфигурации
        /// (базовый URL берётся из секции "Email", токен уже экранирован).
        /// </summary>
        public string ConfirmationLink { get; set; } = string.Empty;

        /// <summary>Идентификатор аккаунта, для которого запрошено подтверждение.</summary>
        public Guid UserId { get; set; }

        /// <summary>Момент запроса (UTC).</summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }
}
