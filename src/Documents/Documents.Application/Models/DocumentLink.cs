namespace Documents.Application.Models
{
    /// <summary>
    /// Подписанная ссылка на файл в MinIO. Скачивание идёт напрямую из хранилища,
    /// минуя API сервиса.
    /// </summary>
    public sealed record DocumentLink(string DownloadUrl, string FileName);
}
