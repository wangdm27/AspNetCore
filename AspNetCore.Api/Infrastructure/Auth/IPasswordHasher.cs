namespace AspNetCore.Api.Infrastructure.Auth
{
    /// <summary>
    /// 密码哈希器接口
    /// 提供密码哈希和验证功能
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// 对密码进行哈希处理
        /// </summary>
        /// <param name="password">原始密码</param>
        /// <returns>包含哈希值和盐的元组</returns>
        (string Hash, string Salt) HashPassword(string password);

        /// <summary>
        /// 验证密码是否与哈希值匹配
        /// </summary>
        /// <param name="password">待验证的密码</param>
        /// <param name="passwordHash">存储的密码哈希值</param>
        /// <param name="passwordSalt">存储的密码盐</param>
        /// <returns>如果密码匹配返回 true，否则返回 false</returns>
        bool Verify(string password, string passwordHash, string passwordSalt);
    }
}