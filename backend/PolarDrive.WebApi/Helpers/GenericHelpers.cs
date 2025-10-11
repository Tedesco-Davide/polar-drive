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
}
