using System.Linq;
using System.Text.RegularExpressions;

namespace SystemJobsDoctor.Services
{
    public enum HintKind
    {
        None,
        RecordNotFound,
        Privilege,
        Timeout,
        Throttling,
        Sql,
        NullReference,
        BusinessError,
        ExternalService,
        Duplicate,
        InfiniteLoop,
        Schema,
        DisabledUser,
        ProcessDeactivated
    }

    /// <summary>
    /// Recognizes the most frequent Dataverse system job errors and points to the usual cause.
    /// Order matters: the first matching pattern wins.
    /// </summary>
    public static class ErrorHints
    {
        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

        private static readonly (HintKind Kind, Regex Pattern)[] Rules =
        {
            (HintKind.InfiniteLoop, new Regex(@"infinite loop|maximum depth|depth .*(exceed|limit)", Options)),
            (HintKind.Throttling, new Regex(@"number of requests exceeded|combined execution time|service protection|throttl|\b429\b|too many requests", Options)),
            (HintKind.Sql, new Regex(@"\bsql\b|deadlock|generic sql error", Options)),
            (HintKind.Timeout, new Regex(@"time[ -]?out|timed out|exceeded .*(time|duration)|sandbox .*(time|limit)|2 minutes", Options)),
            (HintKind.Privilege, new Regex(@"privilege|\bprv[A-Z]\w+|seclib|does not have permission|access is denied|not have .* access|principal user", Options)),
            (HintKind.DisabledUser, new Regex(@"(user|owner|systemuser) .*(disabled|inactive)|disabled user", Options)),
            (HintKind.ProcessDeactivated, new Regex(@"(workflow|process|flow) .*(deactivated|not activated|unpublished|draft|is not active)", Options)),
            (HintKind.Duplicate, new Regex(@"duplicate|already exists|cannot insert duplicate key", Options)),
            (HintKind.Schema, new Regex(@"(attribute|field|column|property) .*(does not exist|not found|is not valid)|could not find .*(attribute|field)|invalid argument", Options)),
            (HintKind.RecordNotFound, new Regex(@"does not exist|not found|was not found|could not find", Options)),
            (HintKind.NullReference, new Regex(@"nullreferenceexception|object reference not set|keynotfoundexception|given key was not present", Options)),
            (HintKind.ExternalService, new Regex(@"\{url\}|webexception|remote server|endpoint|\b(401|403|500|502|503|504)\b|unable to connect|ssl|tls|socket", Options)),
            (HintKind.BusinessError, new Regex(@"invalidpluginexecutionexception|isv code|business process error|aborted the operation", Options))
        };

        public static HintKind Detect(string signature)
        {
            if (string.IsNullOrEmpty(signature)) return HintKind.None;
            return Rules.Where(r => r.Pattern.IsMatch(signature)).Select(r => r.Kind).FirstOrDefault();
        }
    }
}
