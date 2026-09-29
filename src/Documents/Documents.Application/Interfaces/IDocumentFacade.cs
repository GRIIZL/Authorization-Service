using System;
using System.Threading;
using System.Threading.Tasks;
using Documents.Application.Models;
using Documents.Domain;

namespace Documents.Application.Interfaces
{
    /// <summary>
    /// Фасад подсистемы документов: единая точка входа для контроллера.
    /// Прячет за собой генерацию PDF, хранение метаданных и работу с MinIO
    /// в несколько цельных операций.
    /// </summary>
    public interface IDocumentFacade
    {
        // Единая точка входа для генерации и архивации отчета (Паттерн Фасад)
        Task<DocumentMetadata> CreateAndStoreMedicalReportAsync(
            Guid appointmentId, 
            string patientName, 
            string complaints, 
            string conclusion, 
            string recommendations, 
            CancellationToken cancellationToken = default);

        // Версия документа (имя, тип, ETag, дата) для проверки кэша без чтения файла из S3.
        Task<DocumentVersion?> GetDocumentVersionAsync(Guid appointmentId, CancellationToken cancellationToken = default);

        // Тело документа для HTTP-ответа: поток из MinIO вместе с именем и типом файла.
        Task<DocumentContent?> DownloadDocumentAsync(Guid appointmentId, CancellationToken cancellationToken = default);

        // Подписанная ссылка на скачивание напрямую из MinIO, мимо API и с кэшированием в браузере.
        Task<DocumentLink?> GetDocumentLinkAsync(Guid appointmentId, CancellationToken cancellationToken = default);
    }
}
