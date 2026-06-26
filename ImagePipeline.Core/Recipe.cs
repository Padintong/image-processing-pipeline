namespace ImagePipeline.Core;

// A flat list of operation instances plus output destination fields
// (ADR-006, ADR-007). The same Recipe shape is used by Single mode, the
// Folder route, and both default/override slots in the Manifest route
// (ADR-009) — just instantiated a different number of times per route.
public sealed record Recipe(
    IReadOnlyList<OperationInstance> Operations,
    string OutputDestination,
    bool IsRelativeDestination);
