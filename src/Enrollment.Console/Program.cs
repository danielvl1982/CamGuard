// Thin entry point dispatching Enrollment.Console's two commands:
//   spike     <imagePath>                — FaceAiSharp net10.0 compatibility check
//   register  "<Full Name>" <photo...>   — enrolls a member via RegisterMemberCommandHandler
// All decision logic lives in FaceAiSharpSpike/RegisterMemberCommandHandler (testable,
// injectable) — per dotnet-testable-code-design this Main never holds business logic inline.
using Enrollment.Application;
using Enrollment.Console;
using Enrollment.Infrastructure;
using FaceAiSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

return args[0] switch
{
    "spike" => RunSpike(args[1..]),
    "register" => await RunRegisterAsync(args[1..]),
    _ => PrintUsageAndFail(),
};

static int RunSpike(string[] spikeArgs)
{
    if (spikeArgs.Length != 1)
    {
        Console.Error.WriteLine("Usage: dotnet run --project src/Enrollment.Console -- spike <imagePath>");
        return 1;
    }

    var imagePath = spikeArgs[0];

    var detector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
    var embeddingsGenerator = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();

    var spike = new FaceAiSharpSpike(detector, embeddingsGenerator);
    var result = spike.Run(imagePath);

    Console.WriteLine($"Outcome: {result.Outcome}");
    Console.WriteLine($"Faces detected: {result.FacesDetected}");
    if (result.Embedding is not null)
    {
        Console.WriteLine($"Embedding dimension: {result.Embedding.Length}");
    }

    return result.Outcome == FaceAiSharpSpikeOutcome.Success ? 0 : 1;
}

static async Task<int> RunRegisterAsync(string[] registerArgs)
{
    if (registerArgs.Length < 2)
    {
        Console.Error.WriteLine("Usage: dotnet run --project src/Enrollment.Console -- register \"<Full Name>\" <photo1> [photo2 ...]");
        return 1;
    }

    var fullName = registerArgs[0];
    var photoPaths = registerArgs[1..];

    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .Build();

    var services = new ServiceCollection().AddEnrollment(configuration);
    await using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    var photoStreams = new List<FileStream>(photoPaths.Length);
    try
    {
        foreach (var path in photoPaths)
        {
            photoStreams.Add(File.OpenRead(path));
        }

        var command = new RegisterMemberCommand(fullName, photoStreams);
        var handler = scope.ServiceProvider.GetRequiredService<RegisterMemberCommandHandler>();
        var result = await handler.HandleAsync(command, CancellationToken.None);

        if (result.IsFailure)
        {
            Console.Error.WriteLine($"Registration failed: {result.Error}");
            return 1;
        }

        Console.WriteLine($"Member registered: {result.Value}");
        return 0;
    }
    finally
    {
        foreach (var stream in photoStreams)
        {
            await stream.DisposeAsync();
        }
    }
}

static void PrintUsage() =>
    Console.Error.WriteLine("""
        Usage:
          dotnet run --project src/Enrollment.Console -- spike <imagePath>
          dotnet run --project src/Enrollment.Console -- register "<Full Name>" <photo1> [photo2 ...]
        """);

static int PrintUsageAndFail()
{
    PrintUsage();
    return 1;
}
