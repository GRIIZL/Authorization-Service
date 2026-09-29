using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Documents.Application.Interfaces;
using Documents.Application.Models;
using Documents.Domain;

namespace Documents.Application.Services
{
    /// <summary>
    /// Реализация фасада подсистемы документов: объединяет генератор PDF,
    /// хранилище MinIO и репозиторий метаданных в несколько цельных операций.
    /// </summary>
    public class DocumentBusinessService : IDocumentFacade
    {
        private readonly IDocumentMetadataRepository _metadataRepository;
        private readonly IFileStorageService _storageService;
        private readonly PdfGeneratorService _pdfGenerator;

        public DocumentBusinessService(
            IDocumentMetadataRepository metadataRepository, 
            IFileStorageService storageService, 
            PdfGeneratorService pdfGenerator)
        {
            _metadataRepository = metadataRepository;
            _storageService = storageService;
            _pdfGenerator = pdfGenerator;
        }

        public async Task<DocumentMetadata> CreateAndStoreMedicalReportAsync(
            Guid appointmentId, 
            string patientName, 
            string complaints, 
            string conclusion, 
            string recommendations, 
            CancellationToken cancellationToken = default)
        {
            // 1. Генерируем красивый PDF через QuestPDF
            byte[] pdfBytes = _pdfGenerator.GenerateMedicalReportPdf(patientName, complaints, conclusion, recommendations);

            using (var stream = new MemoryStream(pdfBytes))
            {
                // 2. Отправляем поток байт в NoSQL MinIO S3 хранилище
                string storageKey = await _storageService.UploadFileAsync(
                    "MedicalReport.pdf", 
                    stream, 
                    "application/pdf", 
                    cancellationToken);

                // 3. Сохраняем паспорт файла в PostgreSQL
                var metadata = new DocumentMetadata
                {
                    Id = Guid.NewGuid(),
                    FileName = $"Report_{appointmentId}.pdf",
                    ContentType = "application/pdf",
                    FileSize = pdfBytes.Length,
                    StorageKey = storageKey,
                    RelatedEntityId = appointmentId,
                    CreatedAt = DateTime.UtcNow
                };

                await _metadataRepository.AddAsync(metadata, cancellationToken);
                return metadata;
            }
        }

        public async Task<DocumentVersion?> GetDocumentVersionAsync(Guid appointmentId, CancellationToken cancellationToken = default)
        {
            DocumentMetadata? metadata = await _metadataRepository.GetByEntityIdAsync(appointmentId, cancellationToken);
            if (metadata == null) return null;

            // Только метаданные: файл из MinIO здесь не читается, поэтому проверка кэша дешёвая.
            return new DocumentVersion(metadata.FileName, metadata.ContentType, ComputeETag(metadata), metadata.CreatedAt.ToUniversalTime());
        }

        public async Task<DocumentContent?> DownloadDocumentAsync(Guid appointmentId, CancellationToken cancellationToken = default)
        {
            DocumentMetadata? metadata = await _metadataRepository.GetByEntityIdAsync(appointmentId, cancellationToken);
            if (metadata == null) return null;

            Stream stream = await _storageService.DownloadFileAsync(metadata.StorageKey, cancellationToken);
            return new DocumentContent(stream, metadata.FileName, metadata.ContentType);
        }

        /// <summary>
        /// Возвращает ссылку на файл в MinIO: браузер скачивает документ напрямую из хранилища
        /// и кэширует её, не обращаясь к API сервиса при повторных загрузках.
        /// </summary>
        public async Task<DocumentLink?> GetDocumentLinkAsync(Guid appointmentId, CancellationToken cancellationToken = default)
        {
            DocumentMetadata? metadata = await _metadataRepository.GetByEntityIdAsync(appointmentId, cancellationToken);
            if (metadata == null) return null;

            string downloadUrl = _storageService.GetDownloadLink(metadata.StorageKey, metadata.FileName);
            return new DocumentLink(downloadUrl, metadata.FileName);
        }

        /// <summary>
        /// ETag по неизменяемым признакам записи: идентификатор, ключ в бакете, размер и дата.
        /// Новый файл — новый ETag, поэтому браузер сам поймёт, что кэш устарел.
        /// </summary>
        private static string ComputeETag(DocumentMetadata metadata)
        {
            var raw = $"{metadata.Id}|{metadata.StorageKey}|{metadata.FileSize}|{metadata.CreatedAt:O}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

            // ETag по стандарту обязан быть в кавычках.
            return $"\"{Convert.ToHexString(hash)[..16]}\"";
        }
    }
}
