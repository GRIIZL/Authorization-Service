using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure.RabbitMQ
{
    /// <summary>
    /// Точка подключения брокера сообщений к Auth.
    /// Провайдер остаётся заменяемым: реализация IEventPublisher знает только про MassTransit,
    /// а Application-слой — только про свой интерфейс.
    /// </summary>
    public static class MessagingServiceCollectionExtensions
    {
        public static IServiceCollection AddAuthMessaging(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddMassTransit(bus =>
            {
                // Consumer писем подтверждения: регистрация публикует событие,
                // а фактическая отправка идёт асинхронно здесь.
                bus.AddConsumer<EmailVerificationRequestedConsumer>();

                bus.UsingRabbitMq((context, cfg) =>
                {
                    var host = configuration["RabbitMQHost"] ?? "localhost";
                    var port = int.TryParse(configuration["RabbitMQPort"], out var parsedPort) ? parsedPort : 5672;

                    cfg.Host(
                        new Uri($"rabbitmq://{host}:{port}/"),
                        mqHost =>
                        {
                            mqHost.Username(configuration["RabbitMQUser"] ?? "guest");
                            mqHost.Password(configuration["RabbitMQPassword"] ?? "guest");
                        });

                    // Собственная durable-очередь Auth для писем подтверждения
                    cfg.ReceiveEndpoint("auth-email-verification-events", endpoint =>
                    {
                        // Повторы с паузой вместо бесконечного requeue-цикла:
                        // после исчерпания попыток сообщение уходит в _error queue (DLQ)
                        endpoint.UseMessageRetry(retry => retry.Interval(5, TimeSpan.FromSeconds(2)));

                        endpoint.ConfigureConsumer<EmailVerificationRequestedConsumer>(context);
                    });
                });
            });

            return services;
        }
    }
}
