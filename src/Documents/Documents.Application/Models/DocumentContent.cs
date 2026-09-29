using System.IO;

namespace Documents.Application.Models
{
    /// <summary>
    /// Тело документа для отдачи клиенту: поток из MinIO плюс данные,
    /// нужные контроллеру для ответа (имя файла и его тип).
    /// </summary>
    public sealed record DocumentContent(Stream Content, string FileName, string ContentType);
}
