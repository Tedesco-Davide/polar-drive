using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PolarDrive.Data.Helpers;

namespace PolarDrive.Data.DbContexts.Gdpr;

/// <summary>
/// Value Converter EF Core per cifratura automatica PII.
/// Cifra in scrittura, decifra in lettura.
/// Supporta sia proprieta string che string? (nullable).
/// </summary>
public class GdprValueConverter(byte[] encryptionKey) : ValueConverter<string, string>(
            // Scrittura su DB: cifra (gestisce null internamente)
        v => GdprHelpers.GdprEncrypt(v, encryptionKey) ?? string.Empty,
            // Lettura da DB: decifra (gestisce null internamente)
        v => GdprHelpers.GdprDecrypt(v, encryptionKey) ?? string.Empty)
{
}

/// <summary>
/// Value Converter per proprieta nullable (string?).
/// </summary>
public class GdprNullableValueConverter(byte[] encryptionKey) : ValueConverter<string?, string?>(
            // Scrittura su DB: cifra
        v => GdprHelpers.GdprEncrypt(v, encryptionKey),
            // Lettura da DB: decifra
        v => GdprHelpers.GdprDecrypt(v, encryptionKey))
{
}

/// <summary>
/// Factory per creare GdprValueConverter con chiave iniettata.
/// </summary>
public static class GdprValueConverterFactory
{
    private static byte[]? _encryptionKey;

    /// <summary>
    /// Inizializza la factory con la chiave di crittografia.
    /// Da chiamare all'avvio dell'applicazione.
    /// </summary>
    public static void Initialize(byte[] encryptionKey)
    {
        _encryptionKey = encryptionKey;
    }

    /// <summary>
    /// Crea un nuovo converter per proprieta non-nullable (string).
    /// </summary>
    public static GdprValueConverter Create()
    {
        if (_encryptionKey == null)
            throw new InvalidOperationException(
                "GdprValueConverterFactory non inizializzata. " +
                "Chiamare Initialize() all'avvio dell'applicazione.");

        return new GdprValueConverter(_encryptionKey);
    }

    /// <summary>
    /// Crea un nuovo converter per proprieta nullable (string?).
    /// </summary>
    public static GdprNullableValueConverter CreateNullable()
    {
        if (_encryptionKey == null)
            throw new InvalidOperationException(
                "GdprValueConverterFactory non inizializzata. " +
                "Chiamare Initialize() all'avvio dell'applicazione.");

        return new GdprNullableValueConverter(_encryptionKey);
    }

    /// <summary>
    /// Verifica se la factory e stata inizializzata.
    /// </summary>
    public static bool IsInitialized => _encryptionKey != null;
}
