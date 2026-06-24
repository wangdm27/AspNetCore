using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace AspNetCore.Api.Infrastructure.Services
{
    /// <summary>
    /// SMTP 邮件服务实现
    /// </summary>
    public sealed class SmtpEmailService : IEmailService
    {
        private readonly EmailOptions _options;

        public SmtpEmailService(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }

        /// <summary>
        /// 发送密码重置邮件
        /// </summary>
        public async Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default)
        {
            var resetUrl = _options.PasswordResetUrl.Replace("{token}", resetToken);
            var subject = "Password Reset Request";
            var body = $@"
                <h2>Password Reset</h2>
                <p>You have requested to reset your password.</p>
                <p>Click the link below to reset your password (link expires in 15 minutes):</p>
                <p><a href=""{resetUrl}"">Reset Password</a></p>
                <p>If you did not request this, please ignore this email.</p>";

            await SendAsync(email, subject, body, cancellationToken);
        }

        /// <summary>
        /// 发送邮件（通用）
        /// </summary>
        public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(to);

            using var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.UseSsl,
                Credentials = new NetworkCredential(_options.UserName, _options.Password)
            };

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
        }
    }
}
