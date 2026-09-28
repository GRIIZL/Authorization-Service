using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Amazon.S3.Transfer;
using Documents.Application.Configuration;
using Documents.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Documents.Infrastructure.Storage
{
    public class AmazonS3StorageService : IFileStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly MinioOptions _options;

        // S3-клиент и настройки приходят из контейнера (регистрируются в Program.cs),
        // здесь нет ни IConfiguration, ни захардкоженных адресов и паролей.
        public AmazonS3StorageService(IAmazonS3 s3Client, IOptions<MinioOptions> options)
        {
            _s3Client = s3Client;
            _options = options.Value;
        }

        public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
        {
            if (await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _options.BucketName))
            {
                return;
            }

            await _s3Client.PutBucketAsync(_options.BucketName, cancellationToken);
        }

        public async Task<string> UploadFileAsync(string fileName, Stream fileStream, string contentType, CancellationToken cancellationToken = default)
        {
            // Генерируем уникальный ключ-путь для файла в S3
            var storageKey = $"{Guid.NewGuid()}_{fileName}";

            var fileTransferUtility = new TransferUtility(_s3Client);

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                Key = storageKey,
                BucketName = _options.BucketName,
                ContentType = contentType
            };

            await fileTransferUtility.UploadAsync(uploadRequest, cancellationToken);
            return storageKey;
        }

        public async Task<Stream> DownloadFileAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            var response = await _s3Client.GetObjectAsync(_options.BucketName, storageKey, cancellationToken);
            return response.ResponseStream;
        }

        public string GetDownloadLink(string storageKey, string fileName)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = storageKey,
                Verb = HttpVerb.GET,
                Protocol = Amazon.S3.Protocol.HTTP,
                Expires = GetStableLinkExpiry(),
                // Заголовки ответа «зашиваются» в подпись ссылки: браузер получит
                // имя файла и разрешение на кэширование прямо от MinIO.
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentType = "application/pdf",
                    ContentDisposition = $"attachment; filename=\"{fileName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}",
                    CacheControl = $"public, max-age={_options.BrowserCacheSeconds}"
                }
            };

            // Подпись считается на месте, без обращения к хранилищу, поэтому метод синхронный.
            return _s3Client.GetPreSignedURL(request);
        }

        /// <summary>
        /// Срок действия ссылки округляется вверх до шага кэша браузера. Без этого подпись
        /// менялась бы при каждом обращении: адрес всегда новый — кэш никогда не используется.
        /// </summary>
        private DateTime GetStableLinkExpiry()
        {
            var step = TimeSpan.FromSeconds(_options.BrowserCacheSeconds);
            var expiresAt = DateTime.UtcNow.AddMinutes(_options.LinkLifetimeMinutes);
            var roundedTicks = (expiresAt.Ticks / step.Ticks + 1) * step.Ticks;

            return new DateTime(roundedTicks, DateTimeKind.Utc);
        }
    }
}
