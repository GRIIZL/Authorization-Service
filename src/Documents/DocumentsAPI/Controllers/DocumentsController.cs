using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Documents.Application.Configuration;
using Documents.Application.Interfaces;

namespace DocumentsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        // Единственная бизнес-зависимость контроллера — фасад.
        // IOptions — это конфигурация, а не сервис, она остаётся.
        private readonly IDocumentFacade _documents;
        private readonly MinioOptions _minioOptions;

        public DocumentsController(IDocumentFacade documents, IOptions<MinioOptions> minioOptions)
        {
            _documents = documents;
            _minioOptions = minioOptions.Value;
        }

        // Эндпоинт генерации и сохранения PDF в MinIO
        [HttpPost("generate-report/{appointmentId}")]
        public async Task<IActionResult> GenerateReport(Guid appointmentId, [FromQuery] string patientName, [FromQuery] string complaints, [FromQuery] string conclusion, [FromQuery] string recommendations, CancellationToken cancellationToken)
        {
            var metadata = await _documents.CreateAndStoreMedicalReportAsync(appointmentId, patientName, complaints, conclusion, recommendations, cancellationToken);
            return Ok(metadata);
        }

        // Эндпоинт скачивания оригинального PDF потока из MinIO по ID приема (US-62)
        [HttpGet("download-report/{appointmentId}")]
        public async Task<IActionResult> DownloadReport(Guid appointmentId, CancellationToken cancellationToken)
        {
            // Сначала только версия: если кэш браузера свежий, отдаём 304, вообще не читая файл из MinIO.
            var version = await _documents.GetDocumentVersionAsync(appointmentId, cancellationToken);
            if (version == null) return NotFound(new { message = "Document metadata not found." });

            var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
            if (ifNoneMatch == "*" || ifNoneMatch == version.ETag)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            // Разрешаем браузеру держать файл в кэше, чтобы не гонять PDF через API заново.
            Response.Headers["Cache-Control"] = $"public, max-age={_minioOptions.BrowserCacheSeconds}";
            Response.Headers.ETag = version.ETag;
            Response.Headers.LastModified = version.LastModifiedUtc.ToString("R");

            var content = await _documents.DownloadDocumentAsync(appointmentId, cancellationToken);
            if (content == null) return NotFound(new { message = "Document metadata not found." });

            return File(content.Content, content.ContentType, content.FileName);
        }

        // Эндпоинт подписанной ссылки на PDF в MinIO: скачивание идёт мимо API,
        // а повторное открытие той же ссылки браузер берёт из своего кэша.
        [HttpGet("report-link/{appointmentId}")]
        public async Task<IActionResult> GetReportLink(Guid appointmentId, CancellationToken cancellationToken)
        {
            var link = await _documents.GetDocumentLinkAsync(appointmentId, cancellationToken);
            if (link == null) return NotFound(new { message = "Document metadata not found." });

            return Ok(new
            {
                downloadUrl = link.DownloadUrl,
                fileName = link.FileName,
                cacheSeconds = _minioOptions.BrowserCacheSeconds,
                linkLifetimeMinutes = _minioOptions.LinkLifetimeMinutes
            });
        }
    }
}
