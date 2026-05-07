using System;
using Serilog.Events;

namespace Translumo.Logging
{
    public sealed class RuntimeLogEntry
    {
        public long Sequence { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public LogEventLevel Level { get; set; }

        public string Source { get; set; }

        public string Message { get; set; }

        public string Details { get; set; }

        public string LevelName => Level.ToString();

        public string TimestampText => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

        public string FullText =>
            $"{Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [{Level}] [{Source}] {Message}" +
            (string.IsNullOrWhiteSpace(Details) ? string.Empty : $"{Environment.NewLine}{Details}");
    }
}
