using System.Security.Cryptography;

namespace CollaborativePuzzle.Api.Helpers;

/// <summary>
/// Provides cryptographically secure random number generation
/// </summary>
public static class SecureRandom
{
    /// <summary>
    /// Returns a random integer between 0 (inclusive) and maxValue (exclusive)
    /// </summary>
    public static int Next(int maxValue)
    {
        return Next(0, maxValue);
    }

    /// <summary>
    /// Returns a random integer between minValue (inclusive) and maxValue (exclusive)
    /// </summary>
    public static int Next(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
            throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be greater than minValue");

        var range = (uint)(maxValue - minValue);
        return minValue + (int)(NextUInt32() % range);
    }

    /// <summary>
    /// Returns a random double between 0.0 and 1.0
    /// </summary>
    public static double NextDouble()
    {
        return NextUInt32() / (double)uint.MaxValue;
    }

    /// <summary>
    /// Returns a random double between minValue and maxValue
    /// </summary>
    public static double NextDouble(double minValue, double maxValue)
    {
        return minValue + (NextDouble() * (maxValue - minValue));
    }

    private static uint NextUInt32()
    {
        var bytes = new byte[4];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }
}