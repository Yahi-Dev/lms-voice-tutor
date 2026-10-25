using Microsoft.AspNetCore.Identity;

namespace LmsTutor.Server.Modules.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}
