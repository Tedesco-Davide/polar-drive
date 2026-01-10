using System.Security.Cryptography;

namespace PolarDrive.Data.Helpers;

/// <summary>
/// Metodi helper per crittografia GDPR AES-256-GCM.
/// Formato output: [12 bytes IV][16 bytes Auth Tag][N bytes Ciphertext] -> Base64
/// </summary>
public static class GdprHelpers
{
    private const int GdprIvSize = 12;      // GCM standard IV size
    private const int GdprTagSize = 16;     // GCM authentication tag size

    /// <summary>
    /// Cifra una stringa con AES-256-GCM.
    /// Output: Base64([IV 12 bytes][Tag 16 bytes][Ciphertext])
    /// </summary>
    /// <param name="plainText">Testo in chiaro da cifrare</param>
    /// <param name="key">Chiave AES-256 (32 bytes)</param>
    /// <returns>Stringa cifrata Base64 o null se input null/empty</returns>
    public static string? GdprEncrypt(string? plainText, byte[] key)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        if (key.Length != 32)
            throw new ArgumentException("La chiave deve essere di 32 bytes (256 bit)");

        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var iv = new byte[GdprIvSize];
        RandomNumberGenerator.Fill(iv);

        var cipherText = new byte[plainBytes.Length];
        var tag = new byte[GdprTagSize];

        using var aes = new AesGcm(key, GdprTagSize);
        aes.Encrypt(iv, plainBytes, cipherText, tag);

        // Formato: IV + Tag + CipherText
        var result = new byte[GdprIvSize + GdprTagSize + cipherText.Length];
        Buffer.BlockCopy(iv, 0, result, 0, GdprIvSize);
        Buffer.BlockCopy(tag, 0, result, GdprIvSize, GdprTagSize);
        Buffer.BlockCopy(cipherText, 0, result, GdprIvSize + GdprTagSize, cipherText.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decifra una stringa cifrata con AES-256-GCM.
    /// Input atteso: Base64([IV 12 bytes][Tag 16 bytes][Ciphertext])
    /// NOTA: Se il dato non e' cifrato (plain text), lo restituisce as-is.
    /// Questo permette la migrazione graduale dei dati esistenti.
    /// </summary>
    /// <param name="encryptedBase64">Stringa cifrata Base64 o plain text</param>
    /// <param name="key">Chiave AES-256 (32 bytes)</param>
    /// <returns>Testo in chiaro o null se input null/empty</returns>
    public static string? GdprDecrypt(string? encryptedBase64, byte[] key)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            return encryptedBase64;

        if (key.Length != 32)
            throw new ArgumentException("La chiave deve essere di 32 bytes (256 bit)");

        // Prova a decifrare - se fallisce, il dato e' probabilmente ancora in chiaro
        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);

            // Verifica lunghezza minima per dati cifrati validi
            if (encryptedBytes.Length < GdprIvSize + GdprTagSize + 1)
            {
                // Troppo corto per essere cifrato, restituisci as-is
                return encryptedBase64;
            }

            var iv = new byte[GdprIvSize];
            var tag = new byte[GdprTagSize];
            var cipherText = new byte[encryptedBytes.Length - GdprIvSize - GdprTagSize];

            Buffer.BlockCopy(encryptedBytes, 0, iv, 0, GdprIvSize);
            Buffer.BlockCopy(encryptedBytes, GdprIvSize, tag, 0, GdprTagSize);
            Buffer.BlockCopy(encryptedBytes, GdprIvSize + GdprTagSize, cipherText, 0, cipherText.Length);

            var plainBytes = new byte[cipherText.Length];
            using var aes = new AesGcm(key, GdprTagSize);
            aes.Decrypt(iv, cipherText, tag, plainBytes);

            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            // Non e' Base64 valido -> dato in chiaro, restituisci as-is
            return encryptedBase64;
        }
        catch (AuthenticationTagMismatchException)
        {
            // Base64 valido ma non cifrato con la nostra chiave -> dato in chiaro
            return encryptedBase64;
        }
        catch (ArgumentException)
        {
            // Altri errori di formato -> dato in chiaro
            return encryptedBase64;
        }
    }

    /// <summary>
    /// Calcola hash SHA-256 per lookup esatto sui campi cifrati.
    /// Usato per creare indici di ricerca sui dati PII.
    /// </summary>
    /// <param name="plainText">Valore in chiaro</param>
    /// <returns>Hash SHA-256 lowercase hex (64 caratteri) o null</returns>
    public static string? GdprComputeLookupHash(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return null;

        // Normalizza il valore prima dell'hash (lowercase, trim)
        var normalized = plainText.Trim().ToLowerInvariant();
        var bytes = System.Text.Encoding.UTF8.GetBytes(normalized);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
