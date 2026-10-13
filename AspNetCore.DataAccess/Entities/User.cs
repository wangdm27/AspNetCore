namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 用户实体；数据库映射见 Configurations/UserConfiguration
    /// </summary>
    public sealed class User
    {
        public Guid Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string PasswordSalt { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
