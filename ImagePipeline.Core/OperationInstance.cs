namespace ImagePipeline.Core;

// One operation instance within a Recipe — a single atomic or composite
// operation with its resolved parameters (ADR-006 composition rules,
// ADR-010 atomic shapes, ADR-011/012/014 composite shapes).
//
// Sealed at every leaf: each subtype is a final, resolved parameter shape,
// not designed for further specialization (ADR-018). The base stays open —
// new sibling operation types are added as new sealed records here, not by
// subclassing an existing leaf.
public abstract record OperationInstance;

// Atomics — ADR-010
public sealed record ResizeOperation(ResizeParams Params) : OperationInstance;
public sealed record CropOperation(CropParams Params) : OperationInstance;
public sealed record ColourTreatmentOperation(ColourTreatmentParams Params) : OperationInstance;
public sealed record FormatConversionOperation(FormatConversionParams Params) : OperationInstance;
public sealed record OverlayOperation(OverlayParams Params) : OperationInstance;
public sealed record PuzzleOperation(PuzzleParams Params) : OperationInstance;

// Composites — ADR-011 (Resize and convert), ADR-012 (Crop and resize),
// ADR-014 (Achievement artwork)
public sealed record ResizeAndConvertOperation(ResizeParams Resize, FormatConversionParams FormatConversion) : OperationInstance;
public sealed record CropAndResizeOperation(CropParams Crop, ResizeParams Resize) : OperationInstance;
public sealed record AchievementArtworkOperation(
    CropParams Crop,
    AchievementOverlayParams Overlay,
    ColourTreatmentParams ColourTreatment,
    ResizeParams Resize,
    FormatConversionParams FormatConversion) : OperationInstance;
