using Microsoft.EntityFrameworkCore;
using Appointments.Application.Configuration;
using Appointments.Application.Interfaces;
using Appointments.Application.Services;
using Appointments.Infrastructure.PostgreSql.Data;
using Appointments.Infrastructure.PostgreSql.Repositories;
using Appointments.Infrastructure.RabbitMQ;
using Appointments.Infrastructure.Notifications;
using Appointments.Infrastructure.Scheduling;

// «Прививка» от конфликтов UTC часовых поясов Postgres — один раз при старте приложения (SRP),
// а не при каждом создании модели DbContext
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Настройка CORS, чтобы фронтенд мог обращаться к порту 5250 без блокировок браузера
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Явно перенаправляем сборку миграций в слой Infrastructure
builder.Services.AddDbContext<AppointmentsDataContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("Appointments.Infrastructure.PostgreSql")
    ));

// Настройки расчёта слотов приёма (US-7) — читаются из секции "AppointmentSlots",
// переопределяются переменными окружения (AppointmentSlots__SlotMinutes и т.д.)
builder.Services.Configure<AppointmentSlotsOptions>(
    builder.Configuration.GetSection(AppointmentSlotsOptions.SectionName));

// Регистрация слоев Clean Architecture
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<ISlotService, SlotService>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();

// Регистрация обработчика событий специализаций (DIP: интерфейс → реализация)
builder.Services.AddScoped<ISpecializationEventHandlingService, SpecializationEventHandlingService>();

// Настройки уведомлений (US-63/US-68): адрес Documents API, cron напоминаний, заглушки имён
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.Configure<SendGridOptions>(
    builder.Configuration.GetSection(SendGridOptions.SectionName));

// Фасад уведомлений: связывает репозиторий, Documents API и отправку почты
builder.Services.AddScoped<IAppointmentNotificationService, AppointmentNotificationService>();

// Публикатор событий: адаптер над MassTransit IPublishEndpoint
builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

// Клиент Documents API: сервис записей не верстает PDF сам, а делегирует это Documents
builder.Services.AddHttpClient<IDocumentsApiClient, DocumentsApiClient>();

// Выбор почтового провайдера: с API-ключом — SendGrid, без него — лог-заглушка
var sendGridApiKey = builder.Configuration.GetSection(SendGridOptions.SectionName)["ApiKey"];
if (!string.IsNullOrWhiteSpace(sendGridApiKey))
{
    builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, LogEmailSender>();
}

// Регистрация шины MassTransit (RabbitMQ): consumer + endpoint
builder.Services.AddAppointmentsMessaging(builder.Configuration);

// Quartz: фоновая задача напоминаний с cron-триггером (US-63)
builder.Services.AddAppointmentsScheduling(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();

app.UseDefaultFiles(); // Ищет index.html в wwwroot по умолчанию
app.UseStaticFiles();  // Разрешает серверу отдавать HTML/JS файлы

app.MapControllers();

app.Run();
