using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Documents.Application.Interfaces
{
    public interface IFileStorageService
    {
        /// <summary>
        /// Проверяет, что бакет существует, и создаёт его при отсутствии.
        /// Вызывается один раз при старте приложения, а не на каждой загрузке файла.
        /// </summary>
        Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default);

        // Метод принимает поток файла и отправляет его в MinIO
        Task<string> UploadFileAsync(string fileName, Stream fileStream, string contentType, CancellationToken cancellationToken = default);

        // Метод скачивает файл обратно из MinIO в виде потока байт
        Task<Stream> DownloadFileAsync(string storageKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает подписанную ссылку на файл в MinIO. Скачивание идёт напрямую из хранилища
        /// мимо API, а заголовок Cache-Control внутри ссылки позволяет браузеру кэшировать файл
        /// и не скачивать его повторно.
        /// </summary>
        string GetDownloadLink(string storageKey, string fileName);
    }
}
