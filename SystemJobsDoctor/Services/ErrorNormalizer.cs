using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace SystemJobsDoctor.Services
{
    /// <summary>
    /// Turns raw system job errors into stable signatures: GUIDs, dates, numbers, URLs, e-mails and quoted values
    /// (record names, field values) are replaced by placeholders, so 1,240 failures with the same cause share one signature.
    /// </summary>
    public static class ErrorNormalizer
    {
        public const int MaxLength = 400;

        private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant;

        private static readonly Regex Url = new Regex(@"\b(?:https?|ftp)://[^\s'""<>]+", Options | RegexOptions.IgnoreCase);
        private static readonly Regex Email = new Regex(@"\b[\w.+-]+@[\w-]+(?:\.[\w-]+)+\b", Options);
        private static readonly Regex Guid = new Regex(@"\{?\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b\}?", Options);
        private static readonly Regex CompactGuid = new Regex(@"\b[0-9a-fA-F]{32}\b", Options);
        private static readonly Regex DateTimeValue = new Regex(
            @"\b\d{1,4}[/.-]\d{1,2}[/.-]\d{1,4}(?:[ T]\d{1,2}:\d{2}(?::\d{2})?(?:[.,]\d+)?(?:\s?[AaPp][Mm])?(?:Z|[+-]\d{2}:?\d{2})?)?",
            Options);
        private static readonly Regex Time = new Regex(@"\b\d{1,2}:\d{2}(?::\d{2})?(?:[.,]\d+)?(?:\s?[AaPp][Mm])?\b", Options);
        private static readonly Regex SingleQuoted = new Regex(@"'([^'\r\n]{1,300})'", Options);
        private static readonly Regex DoubleQuoted = new Regex(@"""([^""\r\n]{1,300})""", Options);
        private static readonly Regex Guillemets = new Regex(@"«\s*([^»\r\n]{1,300}?)\s*»", Options);
        private static readonly Regex Number = new Regex(@"(?<![\w.{])[-+]?\d+(?:[.,]\d+)*(?![\w}])", Options);
        private static readonly Regex Spaces = new Regex(@"\s+", Options);

        /// <summary>Quoted values that look like schema names (new_field, account) are kept: they identify the cause.</summary>
        private static readonly Regex SchemaName = new Regex(@"^[A-Za-z_][A-Za-z0-9_.]*$", Options);

        private static readonly string[] NoisePrefixes =
        {
            "at ", "Plugin Trace", "Error Message", "Unhandled exception", "Unhandled Exception", "Exception type:", "[", "---",
            "Server stack trace", "Inner Exception", "Exception rethrown", "Microsoft.Crm.CrmException:", "Detail:", "<"
        };

        /// <summary>
        /// The meaningful part of an error: the "Message:" line of a plugin trace when present,
        /// otherwise the first line that is neither a stack frame nor a header.
        /// </summary>
        public static string ExtractCause(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return string.Empty;

            var lines = message.Replace("\r\n", "\n").Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();
            if (lines.Count == 0) return string.Empty;

            var explicitMessage = lines.FirstOrDefault(l => l.StartsWith("Message:", StringComparison.OrdinalIgnoreCase) && l.Length > 9);
            string cause;
            if (explicitMessage != null)
            {
                cause = explicitMessage.Substring("Message:".Length).Trim();
            }
            else
            {
                cause = lines.FirstOrDefault(l => !IsNoise(l))
                        ?? StripPrefix(lines.FirstOrDefault(l => l.StartsWith("Microsoft.Crm.CrmException:", StringComparison.OrdinalIgnoreCase)))
                        ?? lines[0];
            }

            cause = StripPrefix(cause);
            return cause.Length > MaxLength ? cause.Substring(0, MaxLength) + "…" : cause;
        }

        /// <summary>Normalized signature of an error (empty string for an empty message).</summary>
        public static string Normalize(string cause)
        {
            if (string.IsNullOrWhiteSpace(cause)) return string.Empty;

            var text = cause;
            text = Url.Replace(text, "{url}");
            text = Email.Replace(text, "{email}");
            text = Guid.Replace(text, "{guid}");
            text = CompactGuid.Replace(text, "{guid}");
            text = DateTimeValue.Replace(text, "{date}");
            text = Time.Replace(text, "{time}");
            text = SingleQuoted.Replace(text, m => QuotedReplacement(m, "'"));
            text = DoubleQuoted.Replace(text, m => QuotedReplacement(m, "\""));
            text = Guillemets.Replace(text, m => SchemaName.IsMatch(m.Groups[1].Value) ? m.Value : "«{value}»");
            text = Number.Replace(text, "{n}");
            text = Spaces.Replace(text, " ").Trim().TrimEnd('.', ':', ';', ',', ' ');
            return text;
        }

        private static string QuotedReplacement(Match match, string quote)
        {
            var value = match.Groups[1].Value;
            if (value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal)) return match.Value;
            return SchemaName.IsMatch(value) ? match.Value : quote + "{value}" + quote;
        }

        private static bool IsNoise(string line)
        {
            if (line.Length < 3) return true;
            foreach (var prefix in NoisePrefixes)
            {
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string StripPrefix(string line)
        {
            if (line == null) return null;
            const string crm = "Microsoft.Crm.CrmException:";
            var text = line.StartsWith(crm, StringComparison.OrdinalIgnoreCase) ? line.Substring(crm.Length).Trim() : line;
            return text.Length == 0 ? line : text;
        }
    }
}
