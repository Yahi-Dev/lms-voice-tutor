using Microsoft.AspNetCore.DataProtection;

namespace LmsTutor.Server.Shared.Security;

public static class DataProtectionExtensions
{
    /// <summary>
    /// The session cookie is encrypted with the Data Protection key ring. Persisting it (in Docker, on the
    /// data volume) keeps students signed in when the container is recreated. Running several instances
    /// would need a shared key store instead of a local folder.
    /// </summary>
    public static IServiceCollection AddPersistentKeyRing(this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName("lms-voice-tutor");

        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        return services;
    }
}
