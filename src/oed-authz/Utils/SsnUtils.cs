using oed_authz.Models;

namespace oed_authz.Utils;

public static class SsnUtils
{
    public static bool IsValidSsn(string estateSsnOnly)
    {
        return estateSsnOnly is not null
            && estateSsnOnly.Length == 11 
            && estateSsnOnly.All(Char.IsAsciiDigit);
    }

    public static string GetEstateSsnFromCloudEvent(CloudEvent daEvent)
    {
        ArgumentException.ThrowIfNullOrEmpty(daEvent.Subject);

        if (IsValidSsn(daEvent.Subject))
        {
            return daEvent.Subject;
        }

        var subject = daEvent.Subject.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (subject is not ["person", _] || !IsValidSsn(subject[1]))
        {
            throw new ArgumentException(nameof(daEvent.Subject) + " must be SSN with '/person/' prefix");
        }

        return subject[1];
    }

    public static string TruncateSsn(string ssn)
    {
        if (ssn.Length < 6)
        {
            return ssn;
        }

        return ssn[..6];
    }
}
