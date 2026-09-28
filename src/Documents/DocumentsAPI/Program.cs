using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Amazon.S3;
using Documents.Application.Configuration;
using Documents.Application.Interfaces;
using Documents.Application.Services;
using Documents.Infrastructure.Data;
using Documents.Infrastructure.Repositories;
using Documents.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();

// Настройки MinIO: секция "MinIO" из appsettings.json, переменных окружения (MinIO__BucketName=...)
// или user-secrets. Ошибки в настройках проверяются сразу при старте (ValidateOnStart),
// а не всплывают при первой загрузке файла.
builder.Services.AddOptions<MinioOptions>()
    .Bind(builder.Configuration.GetSection(MinioOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Подключаем PostgreSQL метаданных (Порт 5435)
builder.Services.AddDbContext<DocumentsDataContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("Documents.Infrastructure")
    ));

// S3-клиент настраивается из IOptions<MinioOptions> и живёт как singleton:
// пересоздавать его на каждый запрос дорого, он потокобезопасен.
builder.Services.AddSingleton<IAmazonS3>(serviceProvider =>
{
    var minioOptions = serviceProvider.GetRequiredService<IOptions<MinioOptions>>().Value;

    return new AmazonS3Client(minioOptions.AccessKey, minioOptions.SecretKey, new AmazonS3Config
    {
        ServiceURL = minioOptions.ServiceUrl,
        AuthenticationRegion = minioOptions.Region,
        ForcePathStyle = true // Обязательно для локального MinIO: адрес вида host/bucket/key
    });
});

// Регистрация инфраструктуры S3 и SQL
builder.Services.AddScoped<IDocumentMetadataRepository, DocumentMetadataRepository>();
builder.Services.AddScoped<IFileStorageService, AmazonS3StorageService>();

// Регистрация сервисов логики и QuestPDF
builder.Services.AddScoped<PdfGeneratorService>();
builder.Services.AddScoped<DocumentBusinessService>();

var app = builder.Build();

// Проверка бакета при старте приложения: если его нет — создаём один раз здесь,
// вместо проверки перед каждой загрузкой файла.
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MinioInitializer");
    var bucketName = serviceProvider.GetRequiredService<IOptions<MinioOptions>>().Value.BucketName;

    try
    {
        await serviceProvider.GetRequiredService<IFileStorageService>().EnsureBucketExistsAsync();
        logger.LogInformation("Бакет {BucketName} в MinIO проверен и готов к работе.", bucketName);
    }
    catch (Exception exception)
    {
        // Без хранилища сервис не может ни записать, ни отдать документ, поэтому останавливаемся сразу.
        logger.LogCritical(exception,
            "Не удалось проверить или создать бакет {BucketName}. Проверьте, что MinIO запущен (docker-compose up -d minio).",
            bucketName);
        throw;
    }
}

// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

app.UseAuthorization();
app.MapControllers();
app.Run();
