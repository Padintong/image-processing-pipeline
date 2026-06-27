using Google.Protobuf;
using RabbitMQ.Client;
using ImagePipeline.Core;
using Wire = ImagePipeline.Messaging.Protos;

namespace ImagePipeline.Messaging;

// Splits a JobEnvelope across native AMQP BasicProperties and a Protobuf
// body, and reassembles it on the way back (ADR-016/017/018/020). Nothing
// outside this class should construct or inspect EnvelopeBody or any other
// Wire.* type directly (ADR-018) — Api/Worker only ever see JobEnvelope,
// BasicProperties/IReadOnlyBasicProperties, and raw bytes.
//
// Stateless and side-effect-free, so this stays a static class rather than
// an instance behind an interface — see the Learning Log for the DI'd
// alternative sketched but not taken.
//
// Scope note: only the AMQP properties already decided by ADR-017
// (MessageId, CorrelationId, Type, Timestamp) are set here. ContentType,
// DeliveryMode, Headers etc. are deliberately untouched — those are
// queue/durability concerns for a future decision, not this mapper's job.
public static class EnvelopeMapper
{
    public static (BasicProperties Properties, byte[] Body) ToWire(JobEnvelope envelope)
    {
        var properties = new BasicProperties
        {
            MessageId = envelope.JobId.ToString(),
            CorrelationId = envelope.CorrelationId.ToString(),
            Type = envelope.JobType,
            // AmqpTimestamp is whole seconds since the Unix epoch — SubmittedAt's
            // sub-second precision does not round-trip.
            Timestamp = new AmqpTimestamp(envelope.SubmittedAt.ToUnixTimeSeconds()),
        };

        var body = new Wire.EnvelopeBody
        {
            AssetRef = envelope.AssetRef,
            Recipe = ToProto(envelope.Recipe),
        };

        return (properties, body.ToByteArray());
    }

    // body is a caller-owned array, not the transient delivery buffer — per
    // RabbitMQ.Client v7's consumer-lifetime contract, Worker's consumer must
    // already have called eventArgs.Body.ToArray() before reaching this
    // method, since the original ReadOnlyMemory<byte> is only valid for the
    // duration of the Received event itself.
    public static JobEnvelope FromWire(IReadOnlyBasicProperties properties, byte[] body)
    {
        var jobId = ParseRequiredGuid(properties.MessageId, nameof(IReadOnlyBasicProperties.MessageId));
        var correlationId = ParseRequiredGuid(properties.CorrelationId, nameof(IReadOnlyBasicProperties.CorrelationId));
        var jobType = properties.Type
            ?? throw new InvalidDataException("Malformed envelope: missing the AMQP 'Type' property (JobType).");

        if (!properties.IsTimestampPresent())
        {
            throw new InvalidDataException("Malformed envelope: missing the AMQP 'Timestamp' property (SubmittedAt).");
        }

        var submittedAt = DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime);
        var envelopeBody = Wire.EnvelopeBody.Parser.ParseFrom(body);

        return new JobEnvelope(
            jobId,
            correlationId,
            jobType,
            envelopeBody.AssetRef,
            submittedAt,
            FromProto(envelopeBody.Recipe));
    }

    private static Guid ParseRequiredGuid(string? value, string propertyName) =>
        Guid.TryParse(value, out var guid)
            ? guid
            : throw new InvalidDataException($"Malformed envelope: missing or invalid AMQP '{propertyName}' property.");

    // ─── Recipe (ADR-006/007) ────────────────────────────────────────────

    private static Wire.Recipe ToProto(Recipe recipe)
    {
        var proto = new Wire.Recipe
        {
            OutputDestination = recipe.OutputDestination,
            IsRelativeDestination = recipe.IsRelativeDestination,
        };
        proto.Operations.AddRange(recipe.Operations.Select(op => ToProto(op)));
        return proto;
    }

    private static Recipe FromProto(Wire.Recipe proto) => new(
        proto.Operations.Select(op => FromProto(op)).ToList(),
        proto.OutputDestination,
        proto.IsRelativeDestination);

    // ─── OperationInstance (ADR-010/011/012/014/020) ─────────────────────
    // C# does not give true closed-hierarchy exhaustiveness here the way the
    // proto oneof's generated enum does: every concrete OperationInstance
    // subtype is sealed, but the abstract base itself is not, so the
    // compiler cannot prove no other subtype exists — both discard arms
    // below are real code paths, not just compiler-satisfying noise. Add a
    // case here whenever a new OperationInstance subtype or oneof member is
    // introduced.

    private static Wire.OperationInstance ToProto(OperationInstance op) => op switch
    {
        ResizeOperation o => new Wire.OperationInstance { Resize = ToProto(o.Params) },
        CropOperation o => new Wire.OperationInstance { Crop = ToProto(o.Params) },
        ColourTreatmentOperation o => new Wire.OperationInstance { ColourTreatment = ToProto(o.Params) },
        FormatConversionOperation o => new Wire.OperationInstance { FormatConversion = ToProto(o.Params) },
        OverlayOperation o => new Wire.OperationInstance { Overlay = ToProto(o.Params) },
        PuzzleOperation o => new Wire.OperationInstance { Puzzle = ToProto(o.Params) },
        ResizeAndConvertOperation o => new Wire.OperationInstance
        {
            ResizeAndConvert = new Wire.ResizeAndConvertParams
            {
                Resize = ToProto(o.Resize),
                FormatConversion = ToProto(o.FormatConversion),
            },
        },
        CropAndResizeOperation o => new Wire.OperationInstance
        {
            CropAndResize = new Wire.CropAndResizeParams
            {
                Crop = ToProto(o.Crop),
                Resize = ToProto(o.Resize),
            },
        },
        AchievementArtworkOperation o => new Wire.OperationInstance
        {
            AchievementArtwork = new Wire.AchievementArtworkParams
            {
                Crop = ToProto(o.Crop),
                Overlay = ToProto(o.Overlay),
                ColourTreatment = ToProto(o.ColourTreatment),
                Resize = ToProto(o.Resize),
                FormatConversion = ToProto(o.FormatConversion),
            },
        },
        _ => throw new NotSupportedException(
            $"Unhandled OperationInstance subtype '{op.GetType().Name}' — add a case here when a new operation type is introduced."),
    };

    private static OperationInstance FromProto(Wire.OperationInstance proto) => proto.OperationCase switch
    {
        Wire.OperationInstance.OperationOneofCase.Resize => new ResizeOperation(FromProto(proto.Resize)),
        Wire.OperationInstance.OperationOneofCase.Crop => new CropOperation(FromProto(proto.Crop)),
        Wire.OperationInstance.OperationOneofCase.ColourTreatment => new ColourTreatmentOperation(FromProto(proto.ColourTreatment)),
        Wire.OperationInstance.OperationOneofCase.FormatConversion => new FormatConversionOperation(FromProto(proto.FormatConversion)),
        Wire.OperationInstance.OperationOneofCase.Overlay => new OverlayOperation(FromProto(proto.Overlay)),
        Wire.OperationInstance.OperationOneofCase.Puzzle => new PuzzleOperation(FromProto(proto.Puzzle)),
        Wire.OperationInstance.OperationOneofCase.ResizeAndConvert => new ResizeAndConvertOperation(
            FromProto(proto.ResizeAndConvert.Resize),
            FromProto(proto.ResizeAndConvert.FormatConversion)),
        Wire.OperationInstance.OperationOneofCase.CropAndResize => new CropAndResizeOperation(
            FromProto(proto.CropAndResize.Crop),
            FromProto(proto.CropAndResize.Resize)),
        Wire.OperationInstance.OperationOneofCase.AchievementArtwork => new AchievementArtworkOperation(
            FromProto(proto.AchievementArtwork.Crop),
            FromProto(proto.AchievementArtwork.Overlay),
            FromProto(proto.AchievementArtwork.ColourTreatment),
            FromProto(proto.AchievementArtwork.Resize),
            FromProto(proto.AchievementArtwork.FormatConversion)),
        _ => throw new InvalidDataException(
            $"Malformed envelope: OperationInstance has no recognized operation set (case {proto.OperationCase})."),
    };

    // ─── Atomic axis params (ADR-010) ────────────────────────────────────

    private static Wire.ResizeParams ToProto(ResizeParams p) => new() { Width = p.Width, Height = p.Height };

    private static ResizeParams FromProto(Wire.ResizeParams p) => new(p.Width, p.Height);

    private static Wire.CropParams ToProto(CropParams p) => new() { X = p.X, Y = p.Y, Width = p.Width, Height = p.Height };

    private static CropParams FromProto(Wire.CropParams p) => new(p.X, p.Y, p.Width, p.Height);

    private static Wire.ColourTreatmentParams ToProto(ColourTreatmentParams p) => new()
    {
        Brightness = p.Brightness,
        Contrast = p.Contrast,
        Saturation = p.Saturation,
        Hue = p.Hue,
    };

    private static ColourTreatmentParams FromProto(Wire.ColourTreatmentParams p) =>
        new(p.Brightness, p.Contrast, p.Saturation, p.Hue);

    private static Wire.FormatConversionParams ToProto(FormatConversionParams p)
    {
        var proto = new Wire.FormatConversionParams { TargetFormat = p.TargetFormat };
        if (p.Quality is int quality) proto.Quality = quality;
        if (p.BackgroundColour is string backgroundColour) proto.BackgroundColour = backgroundColour;
        return proto;
    }

    private static FormatConversionParams FromProto(Wire.FormatConversionParams p) => new(
        p.TargetFormat,
        p.HasQuality ? p.Quality : null,
        p.HasBackgroundColour ? p.BackgroundColour : null);

    private static Wire.OverlayParams ToProto(OverlayParams p) => new()
    {
        OverlaySource = p.OverlaySource,
        PositionX = p.PositionX,
        PositionY = p.PositionY,
        Scale = p.Scale,
        Opacity = p.Opacity,
    };

    private static OverlayParams FromProto(Wire.OverlayParams p) =>
        new(p.OverlaySource, p.PositionX, p.PositionY, p.Scale, p.Opacity);

    private static Wire.PuzzleParams ToProto(PuzzleParams p) => new() { Rows = p.Rows, Columns = p.Columns };

    private static PuzzleParams FromProto(Wire.PuzzleParams p) => new(p.Rows, p.Columns);

    // ─── Achievement Artwork's overlay axis (ADR-014) ────────────────────

    private static Wire.AchievementOverlayParams ToProto(AchievementOverlayParams p)
    {
        var proto = new Wire.AchievementOverlayParams { OverlaySource = p.OverlaySource };
        proto.Tiers.AddRange(p.Tiers);
        return proto;
    }

    private static AchievementOverlayParams FromProto(Wire.AchievementOverlayParams p) =>
        new(p.OverlaySource, p.Tiers.ToList());
}
