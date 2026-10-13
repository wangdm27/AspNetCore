namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 菜单实体（树形自关联）；数据库映射见 Configurations/MenuConfiguration
    /// </summary>
    public sealed class Menu
    {
        public Guid Id { get; set; }

        public Guid? ParentId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string Component { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public int Sort { get; set; }

        public string PermissionCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public Menu? Parent { get; set; }
        public ICollection<Menu> Children { get; set; } = new List<Menu>();
    }
}
