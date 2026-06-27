namespace ImagePipeline.Core;

// A flat list of operation instances plus output destination fields
// (ADR-006, ADR-007). The same Recipe shape is used by Single mode, the
// Folder route, and both default/override slots in the Manifest route
// (ADR-009) — just instantiated a different number of times per route.
public sealed record Recipe(
    IReadOnlyList<OperationInstance> Operations,
    string OutputDestination,
    bool IsRelativeDestination)
{
    // Record-generated equality compares Operations via
    // EqualityComparer<IReadOnlyList<OperationInstance>>.Default, which is
    // reference equality for a List<T>/array — collection-typed members don't
    // get value equality for free from `record`. Overridden so two Recipes
    // with the same operations, in the same order, compare equal regardless
    // of which concrete list/array instance holds them (found while writing
    // EnvelopeMapper's round-trip tests — see the Learning Log).
    public bool Equals(Recipe? other) =>
        other is not null
        && Operations.SequenceEqual(other.Operations)
        && OutputDestination == other.OutputDestination
        && IsRelativeDestination == other.IsRelativeDestination;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var operation in Operations)
        {
            hash.Add(operation);
        }
        hash.Add(OutputDestination);
        hash.Add(IsRelativeDestination);
        return hash.ToHashCode();
    }
}
