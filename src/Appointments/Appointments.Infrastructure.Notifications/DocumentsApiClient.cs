using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Appointments.Application.Configuration;
using Appointments.Application.Interfaces;

namespace Appointments.Infrastructure.Notifications
{
    /// <summary>
    /// HTTP-клиент Documents API.
    /// Сервис записей не верстает PDF сам: он просит Documents сгенерировать отчёт
    /// (QuestPDF + заливка в MinIO) и забирает готовый файл.
    /// </summary>
    public class DocumentsApiClient : IDocumentsApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly NotificationOptions _options;
        private readonly ILogger<DocumentsApiClient> _logger;

        public DocumentsApiClient(
            HttpClient httpClient,
            IOptions<NotificationOptions> options,
            ILogger<DocumentsApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<byte[]> GenerateMedicalReportAsync(
            Guid appointmentId,
            string patientName,
            string complaints,
            string conclusion,
            string recommendations,
            CancellationToken cancellationToken = default)
        {
            var baseUrl = _options.DocumentsApiBaseUrl.TrimEnd('/');

            // 1. Просим Documents сгенерировать отчёт и сохранить его в MinIO
            var generateUrl = $"{baseUrl}/api/documents/generate-report/{appointmentId}" +
                              $"?patientName={Uri.EscapeDataString(patientName)}" +
                              $"&complaints={Uri.EscapeDataString(complaints)}" +
                              $"&conclusion={Uri.EscapeDataString(conclusion)}" +
                              $"&recommendations={Uri.EscapeDataString(recommendations)}";

            using var generateResponse = await _httpClient.PostAsync(generateUrl, null, cancellationToken);
            generateResponse.EnsureSuccessStatusCode();

            // 2. Забираем готовый PDF
            var downloadUrl = $"{baseUrl}/api/documents/download-report/{appointmentId}";
            var pdfBytes = await _httpClient.GetByteArrayAsync(downloadUrl, cancellationToken);

            _logger.LogInformation("Documents API вернул PDF для приёма {AppointmentId} ({Size} байт).", appointmentId, pdfBytes.Length);

            return pdfBytes;
        }
    }
}
