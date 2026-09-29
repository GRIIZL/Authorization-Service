using System;
using Auth.Infrastructure.Redis;
using Auth.Infrastructure.RabbitMQ;
using AuthorizationAPI.Services;
using Microsoft.EntityFrameworkCore;
using Auth.Application.Configuration;
using Auth.Application.Interfaces;
using Auth.Application.Services;
using Auth.Infrastructure.PostgreSql.Data;
using Auth.Infrastructure.PostgreSql.Repositories;

// Инфраструктура (Postgres, Redis, RabbitMQ, MailHog, MinIO) поднимается вручную:
// docker compose up -d. Автозапуск и авто-остановка контейнеров из приложения убраны —
// сервис не должен управлять чужим жизненным циклом.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
//builder.Services.AddSwashbuckleSwaggerUi(); // Наш UI

// 1. Подключаем PostgreSQL
builder.Services.AddDbContext<DataContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Подключаем Redis (Задаем локальный порт из docker-compose)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

// 3. Внедряем зависимости по SOLID (Интерфейс -> Реализация)
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();
// Шина сообщений: MassTransit + RabbitMQ (топология, повторы и соединения на стороне библиотеки)
builder.Services.AddAuthMessaging(builder.Configuration);
// Публикатор событий: адаптер над MassTransit IPublishEndpoint
builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
// Отправка писем: SMTP-настройки берутся из секции "Email" (appsettings / user-secrets).
// Письма уходят через MailKit на SMTP-сервер; локально это MailHog.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Authorization API v1"));
    
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        }
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();
