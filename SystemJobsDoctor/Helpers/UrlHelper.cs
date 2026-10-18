using System;

namespace SystemJobsDoctor.Helpers
{
    /// <summary>
    /// Links to the organization web client.
    /// </summary>
    internal static class UrlHelper
    {
        public static string Record(string organizationUrl, string logicalName, Guid id)
        {
            if (string.IsNullOrEmpty(organizationUrl) || string.IsNullOrEmpty(logicalName) || id == Guid.Empty) return null;
            return $"{organizationUrl.TrimEnd('/')}/main.aspx?pagetype=entityrecord&etn={Uri.EscapeDataString(logicalName)}&id={id:D}";
        }

        public static string List(string organizationUrl, string logicalName)
        {
            if (string.IsNullOrEmpty(organizationUrl)) return null;
            return $"{organizationUrl.TrimEnd('/')}/main.aspx?pagetype=entitylist&etn={Uri.EscapeDataString(logicalName)}";
        }
    }
}
