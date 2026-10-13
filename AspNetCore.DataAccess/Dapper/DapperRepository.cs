using AspNetCore.DataAccess.Abstractions;
using global::Dapper;
using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace AspNetCore.DataAccess.Dapper
{
    /// <summary>
    /// 基于 Dapper 的泛型仓储实现，提供对实体的基本 CRUD 操作。
    /// 表名/列名等映射信息优先取自 EF Core 模型（与 EF 共用同一份配置），
    /// 实体不在 EF 模型中时（如测试桩实体）退回读取实体特性。
    /// </summary>
    /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
    public sealed class DapperRepository<TEntity> : IRepository<TEntity>
        where TEntity : class
    {
        /// <summary>
        /// 实体元数据缓存（按实体类型进程级只构建一次，由首个访问的实例执行构建）
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Lazy<EntityMetadata>> MetadataCache = new();

        private readonly IDapperContext _dapperContext;
        private readonly IEntityMappingProvider _mappingProvider;

        /// <summary>
        /// 初始化 DapperRepository 的新实例
        /// </summary>
        /// <param name="dapperContext">Dapper 数据库上下文</param>
        /// <param name="mappingProvider">EF Core 模型映射提供者</param>
        public DapperRepository(IDapperContext dapperContext, IEntityMappingProvider mappingProvider)
        {
            _dapperContext = dapperContext;
            _mappingProvider = mappingProvider;
        }

        /// <summary>
        /// 实体元数据，首次访问时构建并按列名注册查询结果映射，进程级缓存
        /// </summary>
        private EntityMetadata Metadata => MetadataCache.GetOrAdd(
            typeof(TEntity),
            _ => new Lazy<EntityMetadata>(BuildMetadata)).Value;

        /// <summary>
        /// 构建当前实体类型的元数据，并按列名注册 Dapper 查询结果映射
        /// </summary>
        /// <returns>实体元数据实例</returns>
        private EntityMetadata BuildMetadata()
        {
            var metadata = EntityMetadata.Create(_mappingProvider.GetModel(), typeof(TEntity));
            SqlMapper.SetTypeMap(typeof(TEntity), new ColumnNameTypeMap(typeof(TEntity), metadata.ColumnMappings));
            return metadata;
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
        /// <returns>符合条件的只读列表</returns>
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
        /// <returns>符合条件的只读列表</returns>
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
        /// 按数据库列名匹配属性的 Dapper 类型映射：
        /// 优先按"列名 → 属性"映射表解析（解决下划线列名装不回驼峰属性的问题），
        /// 未命中时退回 Dapper 默认的按属性名匹配
        /// </summary>
        private sealed class ColumnNameTypeMap : SqlMapper.ITypeMap
        {
            /// <summary>
            /// Dapper 默认类型映射，用于未命中列名映射表时的回退
            /// </summary>
            private readonly DefaultTypeMap _defaultMap;

            /// <summary>
            /// "数据库列名 → 实体属性"映射表
            /// </summary>
            private readonly IReadOnlyDictionary<string, PropertyInfo> _columnMappings;

            /// <summary>
            /// 初始化按列名匹配的类型映射
            /// </summary>
            /// <param name="type">实体 CLR 类型</param>
            /// <param name="columnMappings">"数据库列名 → 实体属性"映射表</param>
            public ColumnNameTypeMap(Type type, IReadOnlyDictionary<string, PropertyInfo> columnMappings)
            {
                _defaultMap = new DefaultTypeMap(type);
                _columnMappings = columnMappings;
            }

            /// <summary>
            /// 按参数名与类型查找匹配的构造函数
            /// </summary>
            /// <param name="names">参数名数组</param>
            /// <param name="types">参数类型数组</param>
            /// <returns>匹配的构造函数，找不到返回 null</returns>
            public ConstructorInfo? FindConstructor(string[] names, Type[] types)
            {
                return _defaultMap.FindConstructor(names, types);
            }

            /// <summary>
            /// 查找显式标注的构造函数
            /// </summary>
            /// <returns>显式构造函数，找不到返回 null</returns>
            public ConstructorInfo? FindExplicitConstructor()
            {
                return _defaultMap.FindExplicitConstructor();
            }

            /// <summary>
            /// 获取构造函数的参数映射
            /// </summary>
            /// <param name="constructor">构造函数</param>
            /// <param name="columnName">数据库列名</param>
            /// <returns>参数映射</returns>
            public SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName)
            {
                return _defaultMap.GetConstructorParameter(constructor, columnName);
            }

            /// <summary>
            /// 按数据库列名解析成员：先查列名映射表，未命中退回默认的按属性名匹配
            /// </summary>
            /// <param name="columnName">数据库列名</param>
            /// <returns>成员映射</returns>
            public SqlMapper.IMemberMap GetMember(string columnName)
            {
                if (_columnMappings.TryGetValue(columnName, out var property))
                {
                    return new ColumnMemberMap(columnName, property);
                }

                return _defaultMap.GetMember(columnName);
            }
        }

        /// <summary>
        /// 指向实体属性的成员映射实现
        /// </summary>
        private sealed class ColumnMemberMap : SqlMapper.IMemberMap
        {
            /// <summary>
            /// 目标实体属性
            /// </summary>
            private readonly PropertyInfo _property;

            /// <summary>
            /// 初始化成员映射
            /// </summary>
            /// <param name="columnName">数据库列名</param>
            /// <param name="property">目标实体属性</param>
            public ColumnMemberMap(string columnName, PropertyInfo property)
            {
                ColumnName = columnName;
                _property = property;
            }

            /// <summary>
            /// 获取数据库列名
            /// </summary>
            public string ColumnName { get; }

            /// <summary>
            /// 获取字段信息（本实现不基于字段，始终为 null）
            /// </summary>
            public FieldInfo? Field => null;

            /// <summary>
            /// 获取构造函数参数信息（本实现不基于构造函数参数，始终为 null）
            /// </summary>
            public ParameterInfo? Parameter => null;

            /// <summary>
            /// 获取成员类型
            /// </summary>
            public Type MemberType => _property.PropertyType;

            /// <summary>
            /// 获取目标属性信息
            /// </summary>
            public PropertyInfo? Property => _property;
        }
    }
}
