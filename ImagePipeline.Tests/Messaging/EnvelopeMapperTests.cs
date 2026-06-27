using Google.Protobuf;
using ImagePipeline.Core;
using ImagePipeline.Messaging;
using RabbitMQ.Client;
using Wire = ImagePipeline.Messaging.Protos;

namespace ImagePipeline.Tests.Messaging;

public class EnvelopeMapperTests
{
    // ─── Shared envelope construction ──────────────────────────────────────

    private static JobEnvelope CreateEnvelope(IReadOnlyList<OperationInstance> operations) => new(
        JobId: Guid.NewGuid(),
        CorrelationId: Guid.NewGuid(),
        JobType: "process_recipe",
        AssetRef: "uploads/sample.jpg",
        // Whole seconds only — AmqpTimestamp doesn't carry sub-second
        // precision, so a fractional value here would fail the round-trip
        // assertion for a reason unrelated to what's actually being tested.
        SubmittedAt: DateTimeOffset.FromUnixTimeSeconds(1_780_000_000),
        Recipe: new Recipe(operations, OutputDestination: "processed/", IsRelativeDestination: true));

    private static readonly IReadOnlyList<OperationInstance> AllNineOperations = new OperationInstance[]
    {
        new ResizeOperation(new ResizeParams(800, 600)),
        new CropOperation(new CropParams(10, 20, 300, 200)),
        new ColourTreatmentOperation(new ColourTreatmentParams(120, 110, 90, -15)),
        new FormatConversionOperation(new FormatConversionParams("webp", Quality: 80, BackgroundColour: "#FFFFFF")),
        new OverlayOperation(new OverlayParams("overlay-catalog-1", 10, 10, 50, 75)),
        new PuzzleOperation(new PuzzleParams(3, 4)),
        new ResizeAndConvertOperation(new ResizeParams(1024, 768), new FormatConversionParams("avif", Quality: 90)),
        new CropAndResizeOperation(new CropParams(0, 0, 500, 500), new ResizeParams(250, 250)),
        new AchievementArtworkOperation(
            new CropParams(0, 0, 400, 400),
            new AchievementOverlayParams("achievement-overlay-1", new[] { 1, 3, 6 }),
            new ColourTreatmentParams(100, 100, 100, 0),
            new ResizeParams(512, 512),
            new FormatConversionParams("png")),
    };

    [Fact]
    public void RoundTrip_PreservesEnvelope_AcrossAllOperationInstanceSubtypes()
    {
        var original = CreateEnvelope(AllNineOperations);

        var (properties, body) = EnvelopeMapper.ToWire(original);
        var roundTripped = EnvelopeMapper.FromWire(properties, body);

        Assert.Equal(original, roundTripped);
    }

    // ─── FormatConversionParams: proto3 optional presence (ADR-010/020) ────
    // quality/background_colour use proto3's `optional` keyword specifically
    // so "absent" and "present with a falsy value" stay distinguishable —
    // these four cases are exactly the truth table that distinction exists
    // for. (0, "") is the important one: it would still round-trip "correctly"
    // by accident if EnvelopeMapper checked truthiness instead of HasQuality/
    // HasBackgroundColour, so it's not a redundant case.
    [Theory]
    [InlineData(null, null)]
    [InlineData(85, null)]
    [InlineData(null, "#000000")]
    [InlineData(0, "")]
    public void RoundTrip_PreservesFormatConversionPresence(int? quality, string? backgroundColour)
    {
        var original = CreateEnvelope(new OperationInstance[]
        {
            new FormatConversionOperation(new FormatConversionParams("avif", quality, backgroundColour)),
        });

        var (properties, body) = EnvelopeMapper.ToWire(original);
        var roundTripped = EnvelopeMapper.FromWire(properties, body);

        Assert.Equal(original, roundTripped);
    }

    // ─── FromWire: defensive throws on malformed wire data (ADR-021) ───────

    private static BasicProperties ValidProperties() => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        CorrelationId = Guid.NewGuid().ToString(),
        Type = "process_recipe",
        Timestamp = new AmqpTimestamp(1_780_000_000),
    };

    private static byte[] ValidBody() => new Wire.EnvelopeBody
    {
        AssetRef = "uploads/sample.jpg",
        Recipe = new Wire.Recipe { OutputDestination = "processed/", IsRelativeDestination = true },
    }.ToByteArray();

    [Fact]
    public void FromWire_Throws_WhenMessageIdMissing()
    {
        var properties = ValidProperties();
        properties.MessageId = null;

        Assert.Throws<InvalidDataException>(() => EnvelopeMapper.FromWire(properties, ValidBody()));
    }

    [Fact]
    public void FromWire_Throws_WhenMessageIdNotAGuid()
    {
        var properties = ValidProperties();
        properties.MessageId = "not-a-guid";

        Assert.Throws<InvalidDataException>(() => EnvelopeMapper.FromWire(properties, ValidBody()));
    }

    [Fact]
    public void FromWire_Throws_WhenCorrelationIdMissing()
    {
        var properties = ValidProperties();
        properties.CorrelationId = null;

        Assert.Throws<InvalidDataException>(() => EnvelopeMapper.FromWire(properties, ValidBody()));
    }

    [Fact]
    public void FromWire_Throws_WhenTypeMissing()
    {
        var properties = ValidProperties();
        properties.Type = null;

        Assert.Throws<InvalidDataException>(() => EnvelopeMapper.FromWire(properties, ValidBody()));
    }

    [Fact]
    public void FromWire_Throws_WhenTimestampMissing()
    {
        // Built directly, not via ValidProperties(), so Timestamp is never
        // touched at all — IsTimestampPresent() needs to see actual absence,
        // not a zero/default value that happens to look unset.
        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            Type = "process_recipe",
        };

        Assert.Throws<InvalidDataException>(() => EnvelopeMapper.FromWire(properties, ValidBody()));
    }

    [Fact]
    public void FromWire_Throws_WhenOperationInstanceHasNoCaseSet()
    {
        // ADR-018 boundary note: constructing Wire.* types directly is
        // exactly what application code (Api/Worker) is never supposed to
        // do — but it's necessary here to exercise FromWire's defensive
        // handling of wire data the mapper itself didn't produce.
        var body = new Wire.EnvelopeBody
        {
            AssetRef = "uploads/sample.jpg",
            Recipe = new Wire.Recipe
            {
                OutputDestination = "processed/",
                IsRelativeDestination = true,
                Operations = { new Wire.OperationInstance() },
            },
        }.ToByteArray();

        Assert.Throws<InvalidDataException>(() => EnvelopeMapper.FromWire(ValidProperties(), body));
    }

    // ─── ToWire: defensive throw on an unrecognized subtype (ADR-021) ──────
    // OperationInstance is abstract but deliberately not sealed (ADR-018), so
    // the compiler can't prove ToWire's switch is exhaustive — this is the
    // otherwise-unreachable discard arm that proves the throw actually fires,
    // using a subtype that exists only here, in this test.
    private sealed record FakeOperation : OperationInstance;

    [Fact]
    public void ToWire_Throws_OnUnrecognizedOperationInstanceSubtype()
    {
        var envelope = CreateEnvelope(new OperationInstance[] { new FakeOperation() });

        Assert.Throws<NotSupportedException>(() => EnvelopeMapper.ToWire(envelope));
    }
}
