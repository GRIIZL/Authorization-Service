using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Documents.Application.Interfaces;
using Documents.Domain;

namespace Documents.Application.Services
{
    public class DocumentBusinessService
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

        public async Task<DocumentMetadata> GenerateAndUploadReportAsync(
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

        public async Task<Stream> DownloadDocumentStreamAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            return await _storageService.DownloadFileAsync(storageKey, cancellationToken);
        }

        /// <summary>
        /// Возвращает ссылку на файл в MinIO: браузер скачивает документ напрямую из хранилища
        /// и кэширует её, не обращаясь к API сервиса при повторных загрузках.
        /// </summary>
        public Task<string> GetDocumentDownloadLinkAsync(string storageKey, string fileName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_storageService.GetDownloadLink(storageKey, fileName));
        }


        public async Task<DocumentMetadata?> GetMetadataByEntityIdAsync(Guid entityId, CancellationToken cancellationToken = default)
        {
            return await _metadataRepository.GetByEntityIdAsync(entityId, cancellationToken);
        }
    }
}
