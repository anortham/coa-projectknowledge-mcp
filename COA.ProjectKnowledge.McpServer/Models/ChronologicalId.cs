namespace COA.ProjectKnowledge.McpServer.Models;

/// <summary>
/// Generates chronological IDs for natural time-based sorting
/// Similar to MongoDB ObjectIds but simpler
/// </summary>
public static class ChronologicalId
{
    private static int _counter = Random.Shared.Next(0, 0xFFFFFF);
    private static readonly object _lock = new();

    /// <summary>
    /// Generate a chronological ID with optional prefix
    /// Format: [prefix-]timestamp-counter
    /// Example: "CHECKPOINT-18C3A2B4F12-A3F2"
    /// </summary>
    public static string Generate()
    {
        lock (_lock)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var counter = Interlocked.Increment(ref _counter) & 0xFFFFFF; // 24-bit counter

            var id = $"{timestamp:X}-{counter:X6}";

            return id;
        }
    }

    /// <summary>
    /// Extract timestamp from a chronological ID
    /// </summary>
    public static DateTime? GetTimestamp(string id)
    {
        try
        {
            // Remove prefix if present
            var parts = id.Split('-');
            var timestampHex = parts.Length > 2 ? parts[1] : parts[0];

            if (long.TryParse(timestampHex, System.Globalization.NumberStyles.HexNumber, null, out var timestamp))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
            }
        }
        catch
        {
            // Invalid format
        }

        return null;
    }
}