namespace ImagePipeline.Core;

// Resolved parameter shapes for each operation axis (ADR-010). Reused as-is
// by composites (ADR-011, ADR-012, ADR-014), nested per axis rather than
// flattened or renamed (Tech Doc, Composite Operation Parameter Shapes —
// general convention).

public sealed record ResizeParams(int Width, int Height);

public sealed record CropParams(int X, int Y, int Width, int Height);

public sealed record ColourTreatmentParams(int Brightness, int Contrast, int Saturation, int Hue);

// Quality is null for lossless targets (no placeholder, per ADR-010).
// BackgroundColour is null unless the target format lacks alpha support
// and the source actually carries transparency to flatten (ADR-013).
// TargetFormat is a plain string for now, not a closed C# enum — its value
// catalog is still an open Category 2 item (Tech Doc, Batch Submission and
// Open Items); revisit once that's pinned down.
public sealed record FormatConversionParams(string TargetFormat, int? Quality = null, string? BackgroundColour = null);

public sealed record OverlayParams(string OverlaySource, int PositionX, int PositionY, int Scale, int Opacity);

public sealed record PuzzleParams(int Rows, int Columns);

// Achievement Artwork's own overlay axis (ADR-014) — distinct from standalone
// OverlayParams: position/size per tier are baked, not user-configurable, and
// Tiers (a subset of 1..6) drives this composite's multi-output cardinality.
public sealed record AchievementOverlayParams(string OverlaySource, IReadOnlyList<int> Tiers)
{
    // Same collection-typed-member equality gap as Recipe.Operations — see
    // the comment there. Tiers needs value equality, not reference equality.
    public bool Equals(AchievementOverlayParams? other) =>
        other is not null
        && OverlaySource == other.OverlaySource
        && Tiers.SequenceEqual(other.Tiers);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(OverlaySource);
        foreach (var tier in Tiers)
        {
            hash.Add(tier);
        }
        return hash.ToHashCode();
    }
}
