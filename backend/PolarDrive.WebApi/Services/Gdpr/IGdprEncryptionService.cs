namespace PolarDrive.WebApi.Services.Gdpr;

/// <summary>
/// Interfaccia per il servizio di crittografia GDPR.
/// Gestisce cifratura AES-256-GCM per dati PII.
/// </summary>
public interface IGdprEncryptionService
{
    /// <summary>
    /// Cifra un valore PII.
    /// </summary>
    /// <param name="plainText">Testo in chiaro da cifrare</param>
    /// <returns>Stringa cifrata Base64 o null se input null/empty</returns>
    string? Encrypt(string? plainText);

    /// <summary>
    /// Decifra un valore PII.
    /// </summary>
    /// <param name="encryptedText">Stringa cifrata Base64</param>
    /// <returns>Testo in chiaro o null se input null/empty</returns>
    string? Decrypt(string? encryptedText);

    /// <summary>
    /// Calcola hash di lookup per ricerche esatte sui campi cifrati.
    /// </summary>
    /// <param name="plainText">Valore in chiaro</param>
    /// <returns>Hash SHA-256 lowercase hex (64 caratteri) o null</returns>
    string? ComputeLookupHash(string? plainText);

    /// <summary>
    /// Ottiene la chiave di crittografia (per Value Converter).
    /// </summary>
    byte[] GetEncryptionKey();
}
