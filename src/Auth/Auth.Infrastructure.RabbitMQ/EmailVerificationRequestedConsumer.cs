using System.Threading.Tasks;
using Auth.Application.Interfaces;
using MassTransit;
using Shared.Events;

namespace Auth.Infrastructure.RabbitMQ
{
    /// <summary>
    /// Consumer запроса письма подтверждения. Отправка письма вынесена из потока
    /// регистрации: если SMTP недоступен, регистрация уже завершена успешно,
    /// а MassTransit повторит доставку письма по настройкам retry.
    /// </summary>
    public class EmailVerificationRequestedConsumer : IConsumer<EmailVerificationRequestedEvent>
    {
        private readonly IEmailSender _emailSender;

        public EmailVerificationRequestedConsumer(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        public Task Consume(ConsumeContext<EmailVerificationRequestedEvent> context)
        {
            // Ошибка отправки приведёт к повторам, а после исчерпания попыток
            // сообщение осядет в _error queue — письмо не потеряется молча.
            return _emailSender.SendVerificationEmailAsync(
                context.Message.RecipientEmail,
                context.Message.ConfirmationLink,
                context.CancellationToken);
        }
    }
}
