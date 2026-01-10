using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace PolarDrive.Data.DbContexts.Gdpr;

/// <summary>
/// Value Comparer per stringhe cifrate.
/// Necessario perche ogni cifratura genera output diverso (IV random).
/// </summary>
public class GdprEncryptedStringComparer : ValueComparer<string?>
{
    public GdprEncryptedStringComparer()
        : base(
            // Comparazione: entrambi null o stesso valore
            (x, y) => (x == null && y == null) || (x != null && y != null && x == y),
            // Hash code
            v => v == null ? 0 : v.GetHashCode(),
            // Snapshot
            v => v)
    {
    }
}
