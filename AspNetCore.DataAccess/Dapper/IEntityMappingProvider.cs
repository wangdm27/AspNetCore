using Microsoft.EntityFrameworkCore.Metadata;

namespace AspNetCore.DataAccess.Dapper
{
    /// <summary>
    /// 实体映射信息提供者：向 Dapper 提供 EF Core 元数据模型（"实体↔表/列"映射总账），
    /// 使 Dapper 与 EF Core 共用同一份映射配置
    /// </summary>
    public interface IEntityMappingProvider
    {
        /// <summary>
        /// 获取 EF Core 元数据模型
        /// </summary>
        /// <returns>EF Core 元数据模型；不可用时返回 null（Dapper 将退回读取实体特性）</returns>
        IModel? GetModel();
    }
}
