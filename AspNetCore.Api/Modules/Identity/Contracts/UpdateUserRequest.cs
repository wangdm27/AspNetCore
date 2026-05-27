using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class UpdateUserRequest
    {
        [Required]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
