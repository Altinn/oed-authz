using Altinn.Dd.InternalEvents.Estate;

namespace oed_authz.Services;

public static class EstateCaseUpdatedEventExtensions
{
    extension(EstateCaseUpdatedEvent estateCaseUpdatedEvent)
    {
        public bool IsFeilfort() =>
            estateCaseUpdatedEvent.CaseStatus == CaseStatus.Feilfort;

        public bool IsProbateIssued() =>
            estateCaseUpdatedEvent.ProbateResultV2?.Result is { Length: > 0 } ||
            estateCaseUpdatedEvent.ResultType is { Length: > 0 };

        public bool ContainsProbateHeirs() =>
            estateCaseUpdatedEvent.ProbateResultV2?.Heirs?.Any() == true;
        
        public bool HasIncompleteRoleInformation() =>
            estateCaseUpdatedEvent.IsProbateIssued() &&
            !estateCaseUpdatedEvent.ContainsProbateHeirs();
    }
}