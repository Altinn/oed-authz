using oed_authz.Models;

namespace oed_authz;
public static class Utils
{
    public static bool IsValidSsn(string estateSsnOnly)
    {
        return estateSsnOnly.Length == 11 && estateSsnOnly.All(t => t is >= '0' and <= '9');
    }

    public static string GetEstateSsnFromCloudEvent(CloudEvent daEvent)
    {
        if (daEvent.Subject == null)
        {
            throw new ArgumentNullException(nameof(daEvent.Subject));
        }

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
}
