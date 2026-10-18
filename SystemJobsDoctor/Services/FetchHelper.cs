using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.ServiceModel;
using System.Text;
using Microsoft.Xrm.Sdk;

namespace SystemJobsDoctor.Services
{
    /// <summary>
    /// FetchXML building blocks shared by the services.
    /// </summary>
    internal static class FetchHelper
    {
        public static string Escape(string value) => SecurityElement.Escape(value ?? string.Empty);

        /// <summary>UTC date in the format understood by FetchXML (the Z suffix marks it as UTC).</summary>
        public static string Date(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        public static string Condition(string attribute, string op, string value)
        {
            return $"<condition attribute=\"{attribute}\" operator=\"{op}\" value=\"{Escape(value)}\" />";
        }

        public static string In(string attribute, IEnumerable<int> values, bool negate = false)
        {
            var list = values?.ToList() ?? new List<int>();
            if (list.Count == 0) return string.Empty;

            var builder = new StringBuilder();
            builder.Append($"<condition attribute=\"{attribute}\" operator=\"{(negate ? "not-in" : "in")}\">");
            foreach (var value in list)
            {
                builder.Append("<value>").Append(value.ToString(CultureInfo.InvariantCulture)).Append("</value>");
            }
            builder.Append("</condition>");
            return builder.ToString();
        }

        /// <summary>
        /// The aggregate query limit (50,000 rows) was exceeded.
        /// </summary>
        public static bool IsAggregateLimit(Exception error)
        {
            for (var current = error; current != null; current = current.InnerException)
            {
                if (current is FaultException<OrganizationServiceFault> fault && fault.Detail != null)
                {
                    if (fault.Detail.ErrorCode == unchecked((int)0x8004E023)) return true;
                    if (Matches(fault.Detail.Message)) return true;
                }
                if (Matches(current.Message)) return true;
            }
            return false;
        }

        private static bool Matches(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            return message.IndexOf("AggregateQueryRecordLimit", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("8004E023", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("maximum record limit", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string ErrorMessage(Exception error)
        {
            if (error is FaultException<OrganizationServiceFault> fault && fault.Detail != null)
            {
                return fault.Detail.Message ?? fault.Message;
            }
            return error.InnerException != null ? $"{error.Message} {error.InnerException.Message}" : error.Message;
        }

        public static T Get<T>(Entity entity, string attribute)
        {
            if (entity == null || !entity.Attributes.TryGetValue(attribute, out var value) || value == null) return default(T);
            if (value is AliasedValue aliased) value = aliased.Value;
            return value is T typed ? typed : default(T);
        }

        public static int? GetOption(Entity entity, string attribute)
        {
            var value = Get<object>(entity, attribute);
            switch (value)
            {
                case OptionSetValue option: return option.Value;
                case int number: return number;
                default: return null;
            }
        }
    }
}
