using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Translumo.Logging
{
    public sealed class RuntimeLogSink : ILogEventSink
    {
        private const int MaxDetailsLength = 20000;
        private static readonly Regex BearerTokenRegex = new Regex(@"Bearer\s+[A-Za-z0-9._\-+/=]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex KeyValueRegex = new Regex(@"(?<key>api[-_ ]?key|authorization|token)(?<separator>[""']?\s*[:=]\s*[""']?)(?<value>[^,\s""';}]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex OpenAiKeyRegex = new Regex(@"sk-[A-Za-z0-9_\-]{12,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly RuntimeLogStore _store;

        public RuntimeLogSink(RuntimeLogStore store)
        {
            _store = store;
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null)
            {
                return;
            }

            var entry = new RuntimeLogEntry()
            {
                Timestamp = logEvent.Timestamp,
                Level = logEvent.Level,
                Source = GetSource(logEvent),
                Message = Sanitize(logEvent.RenderMessage()),
                Details = BuildDetails(logEvent)
            };

            _store.Add(entry);
        }

        private static string GetSource(LogEvent logEvent)
        {
            if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
            {
                return TrimScalarValue(sourceContext);
            }

            return "Application";
        }

        private static string BuildDetails(LogEvent logEvent)
        {
            var builder = new StringBuilder();
            var properties = logEvent.Properties
                .Where(item => item.Key != "SourceContext")
                .OrderBy(item => item.Key)
                .ToArray();

            foreach (var property in properties)
            {
                builder.Append(property.Key);
                builder.Append(": ");
                builder.AppendLine(IsSensitiveProperty(property.Key)
                    ? "[hidden]"
                    : Sanitize(TrimScalarValue(property.Value)));
            }

            if (logEvent.Exception != null)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(Sanitize(logEvent.Exception.ToString()));
            }

            var details = builder.ToString().Trim();
            if (details.Length <= MaxDetailsLength)
            {
                return details;
            }

            return details.Substring(0, MaxDetailsLength) + Environment.NewLine + "...";
        }

        private static bool IsSensitiveProperty(string propertyName)
        {
            return propertyName?.IndexOf("apikey", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName?.IndexOf("api_key", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName?.IndexOf("authorization", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName?.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var sanitized = BearerTokenRegex.Replace(value, "Bearer [hidden]");
            sanitized = KeyValueRegex.Replace(sanitized, "${key}${separator}[hidden]");
            sanitized = OpenAiKeyRegex.Replace(sanitized, "sk-[hidden]");

            return sanitized;
        }

        private static string TrimScalarValue(LogEventPropertyValue value)
        {
            var rendered = value?.ToString() ?? string.Empty;
            if (rendered.Length >= 2 && rendered[0] == '"' && rendered[rendered.Length - 1] == '"')
            {
                rendered = rendered.Substring(1, rendered.Length - 2);
            }

            return rendered.Replace("\\r\\n", Environment.NewLine)
                .Replace("\\n", Environment.NewLine)
                .Replace("\\\"", "\"");
        }
    }
}
