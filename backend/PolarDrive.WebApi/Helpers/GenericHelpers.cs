namespace PolarDrive.WebApi.Helpers;

public static class GenericHelpers
{
    // Metodo per calcolare hash univoco
    public static string ComputeContentHash(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    // Overload per calcolare hash univoco da byte[]
    public static string ComputeContentHash(byte[] contentBytes)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(contentBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    // Overload per lo stream
    public static string ComputeContentHash(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
            stream.Position = 0;

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexStringLower(hashBytes);
    }

    // Helper locale per garantire lo slash finale
    public static string EnsureTrailingSlash(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("WebAPI:BaseUrl non configurato nelle variabili d’ambiente.");
        
        return baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
    }
}
