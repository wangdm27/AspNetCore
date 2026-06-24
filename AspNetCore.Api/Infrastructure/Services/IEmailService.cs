namespace AspNetCore.Api.Infrastructure.Services
{
    /// <summary>
    /// 邮件服务接口
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// 发送密码重置邮件
        /// </summary>
        Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送邮件（通用）
        /// </summary>
        Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    }
}
