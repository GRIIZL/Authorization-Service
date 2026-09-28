using System.ComponentModel.DataAnnotations;

namespace Documents.Application.Configuration
{
    /// <summary>
    /// Настройки S3-совместимого хранилища (локальный MinIO).
    /// Заполняются секцией "MinIO" в appsettings.json и могут быть переопределены
    /// переменными окружения, например MinIO__BucketName=clinic-reports-prod.
    /// </summary>
    public class MinioOptions
    {
        public const string SectionName = "MinIO";

        /// <summary>
        /// Адрес S3 API MinIO (порт 9000, не веб-консоли 9001).
        /// </summary>
        [Required]
        public string ServiceUrl { get; set; } = "http://localhost:9000";

        /// <summary>
        /// Имя бакета для медицинских отчётов. Создаётся автоматически при старте приложения.
        /// </summary>
        [Required]
        public string BucketName { get; set; } = "clinic-medical-reports";

        /// <summary>Логин (root user) MinIO.</summary>
        [Required]
        public string AccessKey { get; set; } = "minioadmin";

        /// <summary>
        /// Пароль MinIO. Значение по умолчанию намеренно пустое: секрет должен приходить
        /// из конфигурации (appsettings.Development.json, user-secrets или переменные окружения),
        /// а не быть прописан в коде.
        /// </summary>
        [Required]
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Регион, используется при подписи запросов по SigV4.
        /// Для MinIO реального значения не имеет, но должно быть не пустым.
        /// </summary>
        [Required]
        public string Region { get; set; } = "us-east-1";

        /// <summary>Сколько минут действует ссылка скачивания.</summary>
        [Range(1, 10080)]
        public int LinkLifetimeMinutes { get; set; } = 60;

        /// <summary>
        /// Сколько секунд браузер держит скачанный файл в своём кэше (Cache-Control: max-age).
        /// </summary>
        [Range(1, 86400)]
        public int BrowserCacheSeconds { get; set; } = 600;
    }
}
