using System;
using System.Threading;
using System.Threading.Tasks;
using Auth.Application.Configuration;
using Auth.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AuthorizationAPI.Services
{
    /// <summary>
    /// Отправка писем через SMTP на MailKit. Параметры берутся из секции "Email"
    /// (appsettings/ENV): локально это MailHog, в проде — реальный SMTP-релей.
    /// </summary>
    public class MailKitEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;
        private readonly ILogger<MailKitEmailSender> _logger;

        public MailKitEmailSender(IOptions<EmailOptions> options, ILogger<MailKitEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(
            string recipientEmail,
            string confirmationLink,
            CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_options.From));
            message.To.Add(MailboxAddress.Parse(recipientEmail));
            message.Subject = "Подтверждение регистрации в клинике";
            message.Body = new TextPart("plain")
            {
                Text = $"Чтобы завершить регистрацию, перейдите по ссылке:\n{confirmationLink}"
            };

            using var client = new SmtpClient();

            // UseSsl=true — implicit TLS (порт 465); иначе StartTls, а если сервер его
            // не поддерживает (MailHog) — MailKit сам выберет Auto и продолжит без шифрования.
            var secureSocketOptions = _options.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.Auto;

            try
            {
                await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, secureSocketOptions, cancellationToken);

                // Анонимная отправка (локальный MailHog) допустима: логин задан только для реального релея.
                if (!string.IsNullOrEmpty(_options.UserName))
                {
                    await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, cancellationToken);
                }

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                // Ссылку и токен в лог не пишем: токен подтверждает владение почтой,
                // его вывод в лог равняется компрометации.
                _logger.LogInformation("Verification email sent to {Email}", recipientEmail);
            }
            catch (Exception exception)
            {
                // Пробрасываем наверх: MassTransit повторит доставку, а после исчерпания
                // попыток сообщение уйдёт в _error queue — сбой почты не теряется молча.
                _logger.LogError(exception, "Failed to send verification email to {Email}", recipientEmail);
                throw;
            }
        }
    }
}
