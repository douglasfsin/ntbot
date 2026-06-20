using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NtBot.Identity.Options;

namespace NtBot.Identity.Services;

public interface IEmailService
{
    Task SendRegistrationOtpAsync(string email, string otpCode, string tenantName);
    Task SendPasswordResetOtpAsync(string email, string otpCode, string userName);
    Task SendWelcomeEmailAsync(string email, string userName, string tenantName);
    Task SendPasswordChangedAsync(string email, string userName);
}

public class EmailService : IEmailService
{
    private readonly SmtpSettings _smtp;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpSettings> smtp, ILogger<EmailService> logger)
    {
        _smtp = smtp.Value;
        _logger = logger;
    }

    public Task SendRegistrationOtpAsync(string email, string otpCode, string tenantName) =>
        SendAsync(email, $"Código de verificação — {tenantName}",
            $"<p>Seu código NTBot: <strong>{otpCode}</strong></p><p>Válido por 10 minutos.</p>");

    public Task SendPasswordResetOtpAsync(string email, string otpCode, string userName) =>
        SendAsync(email, "Recuperação de senha — NTBot",
            $"<p>Olá {userName},</p><p>Código: <strong>{otpCode}</strong></p>");

    public Task SendWelcomeEmailAsync(string email, string userName, string tenantName) =>
        SendAsync(email, $"Bem-vindo ao NTBot, {userName}!",
            $"<p>Conta <strong>{tenantName}</strong> criada. Faça login e configure seus ativos.</p>");

    public Task SendPasswordChangedAsync(string email, string userName) =>
        SendAsync(email, "Senha alterada — NTBot",
            $"<p>Olá {userName}, sua senha foi alterada com sucesso.</p>");

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        if (!_smtp.IsConfigured)
        {
            _logger.LogWarning("[Email] SMTP not configured — OTP/log only. To={To} Subject={Subject} Body={Body}",
                to, subject, htmlBody);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtp.Host, _smtp.Port, _smtp.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
        await client.AuthenticateAsync(_smtp.Username, _smtp.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("[Email] Sent to {To}: {Subject}", to, subject);
    }
}
