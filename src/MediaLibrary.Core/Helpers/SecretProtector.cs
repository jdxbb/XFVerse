using System.Text;

namespace MediaLibrary.Core.Helpers;

public static class SecretProtector
{
    // First-round local placeholder: avoids persisting plain text while keeping the flow simple.
    public static string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
    }

    public static string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return string.Empty;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(protectedText));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
