// Thin entry point for the FaceAiSharp net10.0 compatibility spike.
// All decision logic lives in FaceAiSharpSpike (testable, injectable) —
// per dotnet-testable-code-design this Main never holds business logic inline.
using Enrollment.Console;
using FaceAiSharp;

if (args.Length != 1)
{
    System.Console.Error.WriteLine("Usage: dotnet run --project src/Enrollment.Console -- <imagePath>");
    return 1;
}

string imagePath = args[0];

IFaceDetectorWithLandmarks detector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
IFaceEmbeddingsGenerator embeddingsGenerator = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();

var spike = new FaceAiSharpSpike(detector, embeddingsGenerator);
FaceAiSharpSpikeResult result = spike.Run(imagePath);

System.Console.WriteLine($"Outcome: {result.Outcome}");
System.Console.WriteLine($"Faces detected: {result.FacesDetected}");
if (result.Embedding is not null)
{
    System.Console.WriteLine($"Embedding dimension: {result.Embedding.Length}");
}

return result.Outcome == FaceAiSharpSpikeOutcome.Success ? 0 : 1;
