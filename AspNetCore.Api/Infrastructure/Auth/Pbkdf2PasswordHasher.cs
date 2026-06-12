using System.Security.Cryptography;

namespace AspNetCore.Api.Infrastructure.Auth
{
    /// <summary>
    /// PBKDF2 密码哈希器实现
    /// 使用 PBKDF2 (Password-Based Key Derivation Function 2) 算法对密码进行哈希处理
    /// </summary>
    public sealed class Pbkdf2PasswordHasher : IPasswordHasher
    {
        /// <summary>
        /// PBKDF2 迭代次数
        /// 较高的迭代次数增加破解难度，但会增加计算开销
        /// </summary>
        private const int Iterations = 100_000;

        /// <summary>
        /// 盐的大小（字节）
        /// 16 字节 = 128 位
        /// </summary>
        private const int SaltSize = 16;

        /// <summary>
        /// 生成的哈希密钥大小（字节）
        /// 32 字节 = 256 位
        /// </summary>
        private const int KeySize = 32;

        /// <summary>
        /// 对密码进行 PBKDF2 哈希处理
        /// </summary>
        /// <param name="password">原始密码</param>
        /// <returns>包含哈希值和盐的元组（均为 Base64 编码字符串）</returns>
        public (string Hash, string Salt) HashPassword(string password)
        {
            // 生成随机盐
            var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
            // 使用 PBKDF2 算法生成哈希
            var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            // 将字节数组转换为 Base64 字符串返回
            return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
        }

        /// <summary>
        /// 验证密码是否与存储的哈希值匹配
        /// </summary>
        /// <param name="password">待验证的密码</param>
        /// <param name="passwordHash">存储的密码哈希值（Base64 编码）</param>
        /// <param name="passwordSalt">存储的密码盐（Base64 编码）</param>
        /// <returns>如果密码匹配返回 true，否则返回 false</returns>
        public bool Verify(string password, string passwordHash, string passwordSalt)
        {
            // 将 Base64 编码的盐转换为字节数组
            var saltBytes = Convert.FromBase64String(passwordSalt);
            // 使用相同的参数重新计算哈希
            var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            // 使用固定时间比较防止时序攻击
            return CryptographicOperations.FixedTimeEquals(
                hashBytes,
                Convert.FromBase64String(passwordHash));
        }
    }
}