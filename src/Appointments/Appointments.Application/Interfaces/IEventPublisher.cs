using System.Threading;
using System.Threading.Tasks;

namespace Appointments.Application.Interfaces
{
    /// <summary>
    /// Абстракция публикации событий в шину сообщений (RabbitMQ).
    /// Application-слой Appointments зависит только от этого интерфейса (DIP),
    /// а не от MassTransit.
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>Публикует событие в шину сообщений.</summary>
        Task PublishAsync<T>(T eventMessage, CancellationToken cancellationToken = default) where T : class;
    }
}
