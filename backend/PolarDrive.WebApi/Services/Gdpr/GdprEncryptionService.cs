using PolarDrive.WebApi.Helpers;

namespace PolarDrive.WebApi.Services.Gdpr;

/// <summary>
/// Servizio di crittografia GDPR per dati PII.
/// Utilizza AES-256-GCM con chiave da variabile ambiente.
/// </summary>
public class GdprEncryptionService : IGdprEncryptionService
{
    private readonly byte[] _encryptionKey;

    public GdprEncryptionService()
    {
        var keyHex = Environment.GetEnvironmentVariable("Gdpr__EncryptionKey")
            ?? throw new InvalidOperationException(
                "GDPR Encryption Key non configurata! " +
                "Impostare la variabile Gdpr__EncryptionKey (64 caratteri hex)");

        if (keyHex.Length != 64)
            throw new InvalidOperationException(
                $"GDPR Encryption Key deve essere di 64 caratteri hex (32 bytes). " +
                $"Lunghezza attuale: {keyHex.Length}");

        _encryptionKey = Convert.FromHexString(keyHex);
    }

    public string? Encrypt(string? plainText)
        => GenericHelpers.GdprEncrypt(plainText, _encryptionKey);

    public string? Decrypt(string? encryptedText)
        => GenericHelpers.GdprDecrypt(encryptedText, _encryptionKey);

    public string? ComputeLookupHash(string? plainText)
        => GenericHelpers.GdprComputeLookupHash(plainText);

    public byte[] GetEncryptionKey() => _encryptionKey;
}
