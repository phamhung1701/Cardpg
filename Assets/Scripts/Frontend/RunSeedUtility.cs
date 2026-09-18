using System;

public static class RunSeedUtility
{
    public static string GenerateSeed() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
