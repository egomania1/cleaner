using System.Text.Json;
using Clean.Core.Licensing;

namespace Clean.Infrastructure.Licensing;

// Keeps the trial start on this PC. Deleting this file restarts the trial: that is accepted for a 3,90 €
// product, and the website can issue a short signed trial token later if it ever matters.
public sealed class TrialStore(string path)
{
    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clean", "trial.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // The first launch creates the record. LastSeenAt moves forward only, and is written at most once an hour.
    public TrialRecord LoadOrStart(DateTimeOffset now)
    {
        var record = Read();
        if (record is null)
        {
            record = new TrialRecord(now, now);
            Write(record);
            return record;
        }

        if (now - record.LastSeenAt > TimeSpan.FromHours(1))
        {
            record = record with { LastSeenAt = now };
            Write(record);
        }

        return record;
    }

    private TrialRecord? Read()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<TrialRecord>(File.ReadAllText(path), JsonOptions) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void Write(TrialRecord record)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(record, JsonOptions));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not being able to save must not lock the user out: the trial simply is not recorded.
        }
    }
}
