using System.Diagnostics;
using System.Linq;
using System.Threading.Channels;
using FaceAiSharp;
using Microsoft.Extensions.DependencyInjection;
using Monitoring.Application;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Monitoring.Infrastructure;

/// <summary>
/// <see cref="IPersonDetector"/> adapter reusing FaceAiSharp's SCRFD detector + ArcFace embedding
/// generator — the same API shape as <c>Enrollment.Infrastructure.FaceAiSharpEmbeddingExtractor</c>
/// (detect, reject/short-circuit when no usable face, align + embed), but a wholly separate
/// instance and registration: <c>Monitoring.Infrastructure</c> never references
/// <c>Enrollment.Infrastructure</c> (module-boundary rule), so this class constructs its own
/// detector/embeddings-generator via <see cref="FaceAiSharpBundleFactory"/> directly rather than
/// resolving Enrollment's DI-registered instances.
///
/// <para>
/// Per design.md's Concurrency Design, ONNX Runtime CPU <c>Run()</c> concurrency safety is only
/// medium-confidence (research.md), so every detect+embed call is serialized through a
/// single-consumer <see cref="Channel{T}"/>: <see cref="DetectAsync"/> enqueues a request with a
/// <see cref="TaskCompletionSource{TResult}"/> and returns immediately; one background consumer
/// loop (started once, in the constructor) drains the channel and is the only code path that ever
/// calls the detector/generator. This makes the serialization structural, not a discipline callers
/// must remember — an ordinary <c>async</c> method, impossible to bypass.
/// </para>
/// </summary>
public sealed class FaceAiSharpPersonDetector : IPersonDetector, IDisposable
{
    private readonly IFaceDetectorWithLandmarks _detector;
    private readonly IFaceEmbeddingsGenerator _embeddingsGenerator;
    private readonly ActivitySource _activitySource;
    private readonly Channel<PersonDetectionRequest> _requests;
    private readonly Task _consumerLoop;

    public FaceAiSharpPersonDetector(
        [FromKeyedServices(MonitoringObservability.ActivitySourceName)] ActivitySource activitySource)
    {
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _detector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
        _embeddingsGenerator = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();

        _requests = Channel.CreateUnbounded<PersonDetectionRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        _consumerLoop = Task.Run(ConsumeAsync);
    }

    public Task<PersonDetectionResult> DetectAsync(Stream image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);

        var completion = new TaskCompletionSource<PersonDetectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new PersonDetectionRequest(image, completion, cancellationToken);

        if (!_requests.Writer.TryWrite(request))
        {
            completion.TrySetException(new InvalidOperationException("Unable to enqueue person-detection request; the detector consumer has stopped."));
        }

        return completion.Task;
    }

    private async Task ConsumeAsync()
    {
        await foreach (var request in _requests.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (request.CancellationToken.IsCancellationRequested)
            {
                request.Completion.TrySetCanceled(request.CancellationToken);
                continue;
            }

            using var activity = _activitySource.StartActivity("Monitoring.DetectPerson");

            try
            {
                using var image = Image.Load<Rgb24>(request.Image);

                var faces = _detector.DetectFaces(image);

                // PersonDetectionResult models exactly one embedding per image (see
                // IPersonDetector.cs's scoping note), so faces.detected.count is approximated as
                // presence (0/1) rather than the raw SCRFD face count.
                if (faces.Count == 0)
                {
                    activity?.SetTag("faces.detected.count", 0);
                    activity?.SetTag("person.present", false);
                    request.Completion.TrySetResult(new PersonDetectionResult(false, null));
                    continue;
                }

                var face = faces.First();
                if (face.Landmarks is null || face.Landmarks.Count == 0)
                {
                    // A detected face without usable landmarks can't be embedded — treated as "no
                    // usable person present" per the person-detection spec's zero-face routing,
                    // never silently ignored.
                    activity?.SetTag("faces.detected.count", 0);
                    activity?.SetTag("person.present", false);
                    request.Completion.TrySetResult(new PersonDetectionResult(false, null));
                    continue;
                }

                _embeddingsGenerator.AlignFaceUsingLandmarks(image, face.Landmarks);
                var embedding = _embeddingsGenerator.GenerateEmbedding(image);

                activity?.SetTag("faces.detected.count", 1);
                activity?.SetTag("person.present", true);
                request.Completion.TrySetResult(new PersonDetectionResult(true, embedding));
            }
            catch (Exception ex)
            {
                activity?.SetTag("outcome", "Error");
                request.Completion.TrySetException(ex);
            }
        }
    }

    public void Dispose()
    {
        _requests.Writer.TryComplete();
    }

    private sealed record PersonDetectionRequest(
        Stream Image,
        TaskCompletionSource<PersonDetectionResult> Completion,
        CancellationToken CancellationToken);
}
