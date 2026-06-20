using System.Security.Cryptography;
using System.Text;

namespace MediaLibrary.Core.Helpers;

/// <summary>
/// Protects locally persisted secrets with the current Windows user's DPAPI key.
/// </summary>
public static class SecretProtector
{
    private const string CurrentFormatPrefix = "xfv1:dpapi-cu:";
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("XFVerse.SecretProtector.v1");

    /// <summary>
    /// Protects a non-empty secret by using Windows DPAPI CurrentUser scope.
    /// </summary>
    public static string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("XFVerse secret protection requires Windows.");
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        try
        {
            var protectedBytes = ProtectedData.Protect(
                plainBytes,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            return CurrentFormatPrefix + Convert.ToBase64String(protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    /// <summary>
    /// Reads the current DPAPI format or the legacy Base64-only credential format.
    /// Invalid or cross-user values return an empty string so application startup remains safe.
    /// </summary>
    public static string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return string.Empty;
        }

        if (protectedText.StartsWith(CurrentFormatPrefix, StringComparison.Ordinal))
        {
            return UnprotectCurrentFormat(protectedText[CurrentFormatPrefix.Length..]);
        }

        return DecodeLegacyBase64(protectedText);
    }

    /// <summary>
    /// Reads a protected diagnostic snapshot while preserving pre-1.0 plaintext snapshots.
    /// New snapshots are always written through <see cref="Protect"/>.
    /// </summary>
    public static string UnprotectDiagnosticValue(string? protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return string.Empty;
        }

        return protectedText.StartsWith(CurrentFormatPrefix, StringComparison.Ordinal)
            ? UnprotectCurrentFormat(protectedText[CurrentFormatPrefix.Length..])
            : protectedText;
    }

    /// <summary>
    /// Returns whether a stored value uses the current versioned DPAPI format.
    /// </summary>
    public static bool IsCurrentFormat(string? protectedText)
    {
        return !string.IsNullOrWhiteSpace(protectedText)
               && protectedText.StartsWith(CurrentFormatPrefix, StringComparison.Ordinal);
    }

    private static string UnprotectCurrentFormat(string payload)
    {
        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        byte[]? protectedBytes = null;
        byte[]? plainBytes = null;
        try
        {
            protectedBytes = Convert.FromBase64String(payload);
            plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return string.Empty;
        }
        finally
        {
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }

            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
    }

    private static string DecodeLegacyBase64(string protectedText)
    {
        byte[]? plainBytes = null;
        try
        {
            plainBytes = Convert.FromBase64String(protectedText);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            return string.Empty;
        }
        finally
        {
            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
    }
}
