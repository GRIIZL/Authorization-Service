using System;
using System.Threading;
using System.Threading.Tasks;

namespace Appointments.Application.Interfaces
{
    /// <summary>
    /// Клиент Documents API: сервис записей не верстает PDF сам,
    /// а делегирует это фасаду Documents (QuestPDF + MinIO).
    /// </summary>
    public interface IDocumentsApiClient
    {
        /// <summary>
        /// Просит Documents сгенерировать отчёт по приёму и возвращает готовый PDF.
        /// </summary>
        Task<byte[]> GenerateMedicalReportAsync(
            Guid appointmentId,
            string patientName,
            string complaints,
            string conclusion,
            string recommendations,
            CancellationToken cancellationToken = default);
    }
}
