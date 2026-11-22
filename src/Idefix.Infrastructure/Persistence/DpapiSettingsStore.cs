using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Idefix.Core.Settings;

namespace Idefix.Infrastructure.Persistence;

[SupportedOSPlatform("windows")]
public sealed class DpapiSettingsStore(string filePath) : ISettingsStore
{
    private static readonly byte[] Entropy = "Idefix.Profile.v1"u8.ToArray();

    public async Task<IdefixSettings?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var protectedBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var json = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize(json, IdefixJsonContext.Default.IdefixSettings);
    }

    public async Task SaveAsync(IdefixSettings settings, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(settings, IdefixJsonContext.Default.IdefixSettings);
        var protectedBytes = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporary = filePath + ".tmp";
        await File.WriteAllBytesAsync(temporary, protectedBytes, cancellationToken);
        File.Move(temporary, filePath, overwrite: true);
    }
}
