namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 示例实体；数据库映射见 Configurations/SampleEntityConfiguration
    /// </summary>
    public sealed class SampleEntity
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
