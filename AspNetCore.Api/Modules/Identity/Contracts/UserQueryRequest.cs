using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class UserQueryRequest
    {
        [MaxLength(100)]
        public string? Keyword { get; set; }

        public bool? IsActive { get; set; }

        [Range(1, int.MaxValue)]
        public int PageIndex { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }
}
