using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Documents.Application.Configuration;
using Documents.Application.Services;
using Documents.Domain;

namespace DocumentsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly DocumentBusinessService _documentService;
        private readonly MinioOptions _minioOptions;

        public DocumentsController(DocumentBusinessService documentService, IOptions<MinioOptions> minioOptions)
        {
            _documentService = documentService;
            _minioOptions = minioOptions.Value;
        }

        // Эндпоинт генерации и сохранения PDF в MinIO
        [HttpPost("generate-report/{appointmentId}")]
        public async Task<IActionResult> GenerateReport(Guid appointmentId, [FromQuery] string patientName, [FromQuery] string complaints, [FromQuery] string conclusion, [FromQuery] string recommendations, CancellationToken cancellationToken)
        {
            var metadata = await _documentService.GenerateAndUploadReportAsync(appointmentId, patientName, complaints, conclusion, recommendations, cancellationToken);
            return Ok(metadata);
        }

        // Эндпоинт скачивания оригинального PDF потока из MinIO по ID приема (US-62)
        [HttpGet("download-report/{appointmentId}")]
        public async Task<IActionResult> DownloadReport(Guid appointmentId, CancellationToken cancellationToken)
        {
            var metadata = await _documentService.GetMetadataByEntityIdAsync(appointmentId, cancellationToken);
            if (metadata == null) return NotFound(new { message = "Document metadata not found." });

            var etag = ComputeETag(metadata);

            // Браузер прислал знакомый ETag — файл не менялся, отдаём 304 без тела.
            var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
            if (ifNoneMatch == "*" || ifNoneMatch == etag)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            // Разрешаем браузеру держать файл в кэше, чтобы не гонять PDF через API заново.
            Response.Headers["Cache-Control"] = $"public, max-age={_minioOptions.BrowserCacheSeconds}";
            Response.Headers.ETag = etag;
            Response.Headers.LastModified = metadata.CreatedAt.ToUniversalTime().ToString("R");

            var fileStream = await _documentService.DownloadDocumentStreamAsync(metadata.StorageKey, cancellationToken);
            return File(fileStream, metadata.ContentType, metadata.FileName);
        }

        // Эндпоинт подписанной ссылки на PDF в MinIO: скачивание идёт мимо API,
        // а повторное открытие той же ссылки браузер берёт из своего кэша.
        [HttpGet("report-link/{appointmentId}")]
        public async Task<IActionResult> GetReportLink(Guid appointmentId, CancellationToken cancellationToken)
        {
            var metadata = await _documentService.GetMetadataByEntityIdAsync(appointmentId, cancellationToken);
            if (metadata == null) return NotFound(new { message = "Document metadata not found." });

            var downloadUrl = await _documentService.GetDocumentDownloadLinkAsync(metadata.StorageKey, metadata.FileName, cancellationToken);

            return Ok(new
            {
                downloadUrl,
                fileName = metadata.FileName,
                cacheSeconds = _minioOptions.BrowserCacheSeconds,
                linkLifetimeMinutes = _minioOptions.LinkLifetimeMinutes
            });
        }

        /// <summary>
        /// ETag по неизменяемым признáкам записи: идентификатор, ключ в бакете, размер и дата.
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
