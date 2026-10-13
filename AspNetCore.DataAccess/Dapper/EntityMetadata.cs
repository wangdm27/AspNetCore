using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AspNetCore.DataAccess.Dapper
{
    /// <summary>
    /// 实体元数据，存储实体映射到数据库表的相关信息。
    /// 优先从 EF Core 模型（映射总账）读取；实体不在模型中时（如测试桩实体）退回读取实体特性。
    /// internal 级别配合 InternalsVisibleTo 供单元测试直接验证映射契约。
    /// </summary>
    internal sealed class EntityMetadata
    {
        /// <summary>
        /// 获取数据库表名
        /// </summary>
        public required string TableName { get; init; }

        /// <summary>
        /// 获取实体的主键属性信息（复合主键时取第一个，与历史行为一致）
        /// </summary>
        public required PropertyInfo KeyProperty { get; init; }

        /// <summary>
        /// 获取主键列名
        /// </summary>
        public required string KeyColumnName { get; init; }

        /// <summary>
        /// 获取插入操作时的列名列表
        /// </summary>
        public required string InsertColumnList { get; init; }

        /// <summary>
        /// 获取插入操作时的参数列表
        /// </summary>
        public required string InsertParameterList { get; init; }

        /// <summary>
        /// 获取更新操作时的 SET 子句
        /// </summary>
        public required string UpdateSetClause { get; init; }

        /// <summary>
        /// 获取"数据库列名 → 实体属性"映射表，用于查询结果的列名匹配
        /// </summary>
        public required IReadOnlyDictionary<string, PropertyInfo> ColumnMappings { get; init; }

        /// <summary>
        /// 创建实体元数据：实体在 EF Core 模型中时从模型读取，否则退回读取实体特性
        /// </summary>
        /// <param name="model">EF Core 元数据模型，可为 null</param>
        /// <param name="entityType">实体 CLR 类型</param>
        /// <returns>实体元数据实例</returns>
        /// <exception cref="InvalidOperationException">实体无法确定主键或没有可映射属性时抛出</exception>
        public static EntityMetadata Create(IModel? model, Type entityType)
        {
            var modelEntityType = model?.FindEntityType(entityType);
            if (modelEntityType?.GetTableName() is { Length: > 0 } tableName)
            {
                return CreateFromModel(modelEntityType, tableName);
            }

            return CreateFromAttributes(entityType);
        }

        /// <summary>
        /// 从 EF Core 模型构建实体元数据
        /// </summary>
        /// <param name="entityType">EF Core 实体类型元数据</param>
        /// <param name="tableName">数据库表名</param>
        /// <returns>实体元数据实例</returns>
        /// <exception cref="InvalidOperationException">实体没有主键或主键无对应 CLR 属性时抛出</exception>
        private static EntityMetadata CreateFromModel(IEntityType entityType, string tableName)
        {
            var table = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            var mapped = entityType.GetProperties()
                .Select(x => (Property: x.PropertyInfo, ColumnName: x.GetColumnName(table)))
                .Where(x => x.Property is { } property && property.CanRead && property.CanWrite && x.ColumnName is not null)
                .Select(x => (Property: x.Property!, ColumnName: x.ColumnName!))
                .ToArray();

            var keyProperty = entityType.FindPrimaryKey()?.Properties.FirstOrDefault()
                ?? throw new InvalidOperationException($"Entity {entityType.DisplayName()} must define a primary key.");
            var keyPropertyInfo = keyProperty.PropertyInfo
                ?? throw new InvalidOperationException($"Entity {entityType.DisplayName()} has a shadow primary key, which is not supported.");

            return Build(
                tableName,
                keyPropertyInfo,
                keyProperty.GetColumnName(table) ?? keyPropertyInfo.Name,
                // Guid 主键由客户端生成（ValueGenerated.OnAdd 但需包含在 INSERT 中），仅数值自增主键视为 identity
                keyProperty.ValueGenerated == ValueGenerated.OnAdd && keyProperty.ClrType != typeof(Guid),
                mapped);
        }

        /// <summary>
        /// 从实体特性（Data Annotations）构建实体元数据，兼容不在 EF 模型中的实体
        /// </summary>
        /// <param name="entityType">实体 CLR 类型</param>
        /// <returns>实体元数据实例</returns>
        /// <exception cref="InvalidOperationException">实体无法确定主键时抛出</exception>
        private static EntityMetadata CreateFromAttributes(Type entityType)
        {
            var tableName = entityType.GetCustomAttribute<TableAttribute>()?.Name ?? entityType.Name;
            var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead && x.CanWrite && x.GetCustomAttribute<NotMappedAttribute>() is null)
                .ToArray();

            var keyProperty = properties.FirstOrDefault(x => x.GetCustomAttribute<KeyAttribute>() is not null)
                ?? properties.FirstOrDefault(x => string.Equals(x.Name, "Id", StringComparison.OrdinalIgnoreCase))
                ?? properties.FirstOrDefault(x => string.Equals(x.Name, $"{entityType.Name}Id", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Entity {entityType.Name} must define a key property.");

            var generatedIdentity = keyProperty.GetCustomAttribute<DatabaseGeneratedAttribute>()?.DatabaseGeneratedOption
                == DatabaseGeneratedOption.Identity;

            var mapped = properties
                .Select(x => (Property: x, ColumnName: x.GetCustomAttribute<ColumnAttribute>()?.Name ?? x.Name))
                .ToArray();

            return Build(
                tableName,
                keyProperty,
                mapped.First(x => x.Property == keyProperty).ColumnName,
                generatedIdentity,
                mapped);
        }

        /// <summary>
        /// 由映射好的"属性-列名"对组装元数据
        /// </summary>
        /// <param name="tableName">数据库表名</param>
        /// <param name="keyProperty">主键属性</param>
        /// <param name="keyColumnName">主键列名</param>
        /// <param name="generatedIdentity">主键是否为数据库自增（自增时 INSERT 排除主键列）</param>
        /// <param name="mapped">全部可映射的"属性-列名"对</param>
        /// <returns>实体元数据实例</returns>
        private static EntityMetadata Build(
            string tableName,
            PropertyInfo keyProperty,
            string keyColumnName,
            bool generatedIdentity,
            (PropertyInfo Property, string ColumnName)[] mapped)
        {
            var insertProperties = generatedIdentity
                ? mapped.Where(x => x.Property != keyProperty).ToArray()
                : mapped;
            var updateProperties = mapped.Where(x => x.Property != keyProperty).ToArray();

            return new EntityMetadata
            {
                TableName = tableName,
                KeyProperty = keyProperty,
                KeyColumnName = keyColumnName,
                InsertColumnList = string.Join(", ", insertProperties.Select(x => x.ColumnName)),
                InsertParameterList = string.Join(", ", insertProperties.Select(x => $"@{x.Property.Name}")),
                UpdateSetClause = string.Join(", ", updateProperties.Select(x => $"{x.ColumnName} = @{x.Property.Name}")),
                ColumnMappings = mapped.ToDictionary(x => x.ColumnName, x => x.Property, StringComparer.Ordinal)
            };
        }
    }
}
