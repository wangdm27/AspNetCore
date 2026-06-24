namespace AspNetCore.Api.Infrastructure.Services
{
    /// <summary>
    /// 邮件配置选项
    /// </summary>
    public sealed class EmailOptions
    {
        public const string SectionName = "Email";

        public string SmtpHost { get; set; } = "localhost";

        public int SmtpPort { get; set; } = 587;

        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string FromAddress { get; set; } = "noreply@example.com";

        public string FromName { get; set; } = "AspNetCore App";

        public bool UseSsl { get; set; } = true;

        /// <summary>
        /// 前端密码重置页面 URL 模板，{token} 会被替换为实际 token
        /// </summary>
        public string PasswordResetUrl { get; set; } = "http://localhost:3000/reset-password?token={token}";
    }
}
