using AspNetCore.DataAccess.Abstractions;
using global::Dapper;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace AspNetCore.DataAccess.Dapper
{
    /// <summary>
    /// 基于 Dapper 的泛型仓储实现，提供对实体的基本 CRUD 操作
    /// </summary>
    /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
    public sealed class DapperRepository<TEntity> : IRepository<TEntity>
        where TEntity : class
    {
        private static readonly EntityMetadata Metadata = EntityMetadata.Create();
        private readonly IDapperContext _dapperContext;

        /// <summary>
        /// 初始化 DapperRepository 的新实例
        /// </summary>
        /// <param name="dapperContext">Dapper 数据库上下文</param>
        public DapperRepository(IDapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// 根据实体 ID 异步获取单个实体
        /// </summary>
        /// <param name="id">实体的唯一标识符</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>找到的实体，如果不存在则返回 null</returns>
        public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT * FROM {Metadata.TableName} WHERE {Metadata.KeyColumnName} = @Id";
            return await ExecuteConnectionAsync(
                connection => connection.QuerySingleOrDefaultAsync<TEntity>(
                    new CommandDefinition(sql, new { Id = id }, _dapperContext.Transaction, cancellationToken: cancellationToken)));
        }

        /// <summary>
        /// 异步获取所有实体
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>包含所有实体的只读列表</returns>
        public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT * FROM {Metadata.TableName}";
            var items = await ExecuteConnectionAsync(
                connection => connection.QueryAsync<TEntity>(
                    new CommandDefinition(sql, transaction: _dapperContext.Transaction, cancellationToken: cancellationToken)));

            return items.ToList();
        }

        /// <summary>
        /// 根据指定条件异步查找实体集合（在内存中过滤）
        /// </summary>
        /// <param name="predicate">用于筛选实体的表达式条件</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>符合条件实体的只读列表</returns>
        public Task<IReadOnlyList<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return FindInMemoryAsync(predicate, cancellationToken);
        }

        /// <summary>
        /// 异步添加新实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            var sql = $"INSERT INTO {Metadata.TableName} ({Metadata.InsertColumnList}) VALUES ({Metadata.InsertParameterList})";
            await EnsureTransactionAsync(cancellationToken);
            await ExecuteConnectionAsync(
                connection => connection.ExecuteAsync(
                    new CommandDefinition(sql, entity, _dapperContext.Transaction, cancellationToken: cancellationToken)));
        }

        /// <summary>
        /// 异步更新现有实体
        /// </summary>
        /// <param name="entity">要更新的实体</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            var sql = $"UPDATE {Metadata.TableName} SET {Metadata.UpdateSetClause} WHERE {Metadata.KeyColumnName} = @{Metadata.KeyProperty.Name}";
            await EnsureTransactionAsync(cancellationToken);
            await ExecuteConnectionAsync(
                connection => connection.ExecuteAsync(
                    new CommandDefinition(sql, entity, _dapperContext.Transaction, cancellationToken: cancellationToken)));
        }

        /// <summary>
        /// 异步删除指定 ID 的实体
        /// </summary>
        /// <param name="id">要删除实体的唯一标识符</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        public async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
        {
            var sql = $"DELETE FROM {Metadata.TableName} WHERE {Metadata.KeyColumnName} = @Id";
            await EnsureTransactionAsync(cancellationToken);
            await ExecuteConnectionAsync(
                connection => connection.ExecuteAsync(
                    new CommandDefinition(sql, new { Id = id }, _dapperContext.Transaction, cancellationToken: cancellationToken)));
        }

        /// <summary>
        /// 确保当前上下文存在活动事务，如果不存在则开始新事务
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        private async Task EnsureTransactionAsync(CancellationToken cancellationToken)
        {
            if (_dapperContext.Transaction is null)
            {
                await _dapperContext.BeginTransactionAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 在内存中根据指定条件查找实体集合
        /// </summary>
        /// <param name="predicate">用于筛选实体的表达式条件</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>符合条件实体的只读列表</returns>
        private async Task<IReadOnlyList<TEntity>> FindInMemoryAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken)
        {
            var entities = await GetAllAsync(cancellationToken);
            return entities.Where(predicate.Compile()).ToList();
        }

        /// <summary>
        /// 执行数据库连接操作，确保连接已打开
        /// </summary>
        /// <typeparam name="TResult">操作结果的类型</typeparam>
        /// <param name="executor">要执行的连接操作函数</param>
        /// <returns>操作的结果</returns>
        private async Task<TResult> ExecuteConnectionAsync<TResult>(Func<IDbConnection, Task<TResult>> executor)
        {
            if (_dapperContext.Connection.State != ConnectionState.Open &&
                _dapperContext.Connection is System.Data.Common.DbConnection dbConnection)
            {
                await dbConnection.OpenAsync();
            }

            return await executor(_dapperContext.Connection);
        }

        /// <summary>
        /// 实体元数据，存储实体映射到数据库表的相关信息
        /// </summary>
        private sealed class EntityMetadata
        {
            /// <summary>
            /// 获取数据库表名
            /// </summary>
            public required string TableName { get; init; }

            /// <summary>
            /// 获取实体的主键属性信息
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
            /// 创建实体元数据实例
            /// </summary>
            /// <returns>实体元数据实例</returns>
            public static EntityMetadata Create()
            {
                var entityType = typeof(TEntity);
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

                var insertProperties = generatedIdentity
                    ? properties.Where(x => x != keyProperty).ToArray()
                    : properties;
                var insertColumns = insertProperties.Select(GetColumnName).ToArray();
                var insertParameters = insertProperties.Select(x => $"@{x.Name}").ToArray();
                var updateProperties = properties.Where(x => x != keyProperty).ToArray();
                var updateSetClause = string.Join(", ", updateProperties.Select(x => $"{GetColumnName(x)} = @{x.Name}"));

                return new EntityMetadata
                {
                    TableName = tableName,
                    KeyProperty = keyProperty,
                    KeyColumnName = GetColumnName(keyProperty),
                    InsertColumnList = string.Join(", ", insertColumns),
                    InsertParameterList = string.Join(", ", insertParameters),
                    UpdateSetClause = updateSetClause
                };
            }

            /// <summary>
            /// 获取属性的数据库列名
            /// </summary>
            /// <param name="property">属性信息</param>
            /// <returns>数据库列名</returns>
            private static string GetColumnName(PropertyInfo property)
            {
                return property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
            }
        }
    }
}