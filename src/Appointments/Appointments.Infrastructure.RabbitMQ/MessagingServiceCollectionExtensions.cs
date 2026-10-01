using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Appointments.Infrastructure.RabbitMQ
{
    /// <summary>
    /// Регистрация шины сообщений Appointments.
    /// Детали MassTransit инкапсулированы здесь, чтобы Program.cs оставался тонким.
    /// </summary>
    public static class MessagingServiceCollectionExtensions
    {
        public static IServiceCollection AddAppointmentsMessaging(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddMassTransit(bus =>
            {
                // Регистрируем consumer события об изменении специализации
                bus.AddConsumer<SpecializationChangedEventConsumer>();

                // Consumer результата приёма (US-68): рассылка заключения пациенту
                bus.AddConsumer<AppointmentResultReadyEventConsumer>();

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

                    // Собственная durable-очередь сервиса Appointments
                    cfg.ReceiveEndpoint("appointments-specialization-events", endpoint =>
                    {
                        endpoint.ConfigureConsumer<SpecializationChangedEventConsumer>(context);
                    });

                    // Очередь уведомлений: повторы с паузой, после исчерпания — DLQ (_error)
                    cfg.ReceiveEndpoint("appointments-notification-events", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Interval(5, TimeSpan.FromSeconds(2)));
                        endpoint.ConfigureConsumer<AppointmentResultReadyEventConsumer>(context);
                    });
                });
            });

            return services;
        }
    }
}