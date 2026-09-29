using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Auth.Application.Configuration;
using Auth.Application.Interfaces;
using Auth.Application.Models;
using Auth.Domain;
using Shared.Events;


namespace Auth.Application.Services
{
    public class AuthService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITokenService _tokenService;
        private readonly IEventPublisher _eventPublisher;
        private readonly EmailOptions _emailOptions;

        public AuthService(
            IAccountRepository accountRepository,
            ITokenService tokenService,
            IEventPublisher eventPublisher,
            IOptions<EmailOptions> emailOptions)
        {
            _accountRepository = accountRepository;
            _tokenService = tokenService;
            _eventPublisher = eventPublisher;
            _emailOptions = emailOptions.Value;
        }

        public async Task<bool> IsEmailRegisteredAsync(string email, CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrWhiteSpace(email)) return false;
            return await _accountRepository.ExistByEmailAsync(email.ToLower().Trim(), cancellationToken);
        }

        public async Task<bool> RegisterPatientAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
        {
            if (await IsEmailRegisteredAsync(request.Email, cancellationToken)) return false;

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            string verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

            var newAccount = new Account
            {
                Email = request.Email.ToLower().Trim(),
                PasswordHash = passwordHash,
                PhoneNumber = request.PhoneNumber ?? string.Empty,
                Role = "Patient",
                VerificationToken = verificationToken,
                VerificationTokenExpires = DateTime.UtcNow.AddHours(24)
            };

await _accountRepository.AddAsync(newAccount, cancellationToken);

            // Публикуем событие в RabbitMQ: другие микросервисы (Profiles) узнают о новой регистрации
            // и смогут создать/связать профиль пациента (asynchronous, eventual consistency).
            await _eventPublisher.PublishAsync(new UserRegisteredEvent
            {
                UserId = newAccount.Id,
                Email = newAccount.Email,
                PhoneNumber = newAccount.PhoneNumber,
                Role = newAccount.Role,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            // Ссылка подтверждения собирается из конфигурации (секция "Email"), токен экранируется
            var confirmationLink = string.Format(
                _emailOptions.ConfirmationUrlTemplate,
                Uri.EscapeDataString(verificationToken));

            // Письмо отправляется асинхронно консьюмером из RabbitMQ: регистрация не ждёт
            // SMTP и не падает с 500, если почтовый сервер временно недоступен.
            await _eventPublisher.PublishAsync(new EmailVerificationRequestedEvent
            {
                UserId = newAccount.Id,
                RecipientEmail = newAccount.Email,
                ConfirmationLink = confirmationLink,
                RequestedAt = DateTime.UtcNow
            }, cancellationToken);

            return true;
        }

        public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
        {
            var account = await _accountRepository.GetByVerificationTokenAsync(token, cancellationToken);
            if (account == null || account.VerificationTokenExpires < DateTime.UtcNow) return false;

            account.IsEmailVerefied = true;
            account.VerificationToken = null;
            account.VerificationTokenExpires = null;
            account.UpdatedAt = DateTime.UtcNow;

            await _accountRepository.UpdateAsync(account, cancellationToken);
            return true;
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
        {
            var account = await _accountRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (account == null) return null;

            if(!BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash)) return null;

            var accessToken = _tokenService.GenerateAccessToken(account);
            var refreshToken = _tokenService.GenerateRefreshToken();

            account.RefreshToken = refreshToken;
            account.RefreshTokenExpires = DateTime.UtcNow.AddDays(7);
            account.UpdatedAt = DateTime.UtcNow;

            await _accountRepository.UpdateAsync(account, cancellationToken);
            
            return new AuthResponseDto { AccessToken = accessToken, RefreshToken = refreshToken };
        }

        public async Task<AuthResponseDto?> RefreshTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var account = await _accountRepository.GetByRefreshTokenAsync(token, cancellationToken);
            if (account == null || account.RefreshTokenExpires < DateTime.UtcNow) return null;

            var newAccessToken = _tokenService.GenerateAccessToken(account);
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            account.RefreshToken = newRefreshToken;
            account.RefreshTokenExpires = DateTime.UtcNow.AddDays(7);
            account.UpdatedAt = DateTime.UtcNow;

            await _accountRepository.UpdateAsync(account, cancellationToken);

            return new AuthResponseDto { AccessToken = newAccessToken, RefreshToken = newRefreshToken };
        }
        
    }
}