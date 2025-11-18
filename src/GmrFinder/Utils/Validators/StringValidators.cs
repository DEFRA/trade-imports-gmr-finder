using System.Text.RegularExpressions;

namespace GmrFinder.Utils.Validators;

public partial class StringValidators : IStringValidators
{
    [GeneratedRegex(
        pattern: "^\\d{2}[A-Z]{2}[A-Z0-9]{14}$",
        matchTimeoutMilliseconds: 2000,
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private partial Regex MrnRegex();

    public bool IsValidMrn(string mrn) => MrnRegex().IsMatch(mrn);
}
