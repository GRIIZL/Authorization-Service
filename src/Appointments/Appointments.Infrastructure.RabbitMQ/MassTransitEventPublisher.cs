using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Appointments.Application.Interfaces;

namespace Appointments.Infrastructure.RabbitMQ
{
    /// <summary>
    /// Адаптер публикации событий поверх MassTransit.
    /// Application-слой Appointments зависит только от IEventPublisher (DIP).
    /// </summary>
    public class MassTransitEventPublisher : IEventPublisher
    {
        private readonly IPublishEndpoint _publishEndpoint;

        public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
        {
            _publishEndpoint = publishEndpoint;
        }

        /// <inheritdoc/>
        public Task PublishAsync<T>(T eventMessage, CancellationToken cancellationToken = default) where T : class
        {
            return _publishEndpoint.Publish(eventMessage, cancellationToken);
        }
    }
}
