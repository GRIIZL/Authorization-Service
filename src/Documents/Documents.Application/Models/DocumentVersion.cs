using System;

namespace Documents.Application.Models
{
    /// <summary>
    /// Версия сохранённого документа: одних метаданных достаточно, чтобы проверить
    /// кэш (ETag / Last-Modified), не открывая сам файл в S3.
    /// </summary>
    public sealed record DocumentVersion(
        string FileName,
        string ContentType,
        string ETag,
        DateTime LastModifiedUtc);
}
