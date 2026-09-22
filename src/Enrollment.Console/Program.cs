// Thin entry point now offering an interactive loop:
//   spike     <imagePath>                — FaceAiSharp net10.0 compatibility check
//   register  "<Full Name>" <photo...>   — enrolls a member via RegisterMemberCommandHandler
//   watch                                     — watches photos/enroll, prompting FullName per file
// Interactive menu (no args) lets the user run spike/register/watch repeatedly without restart.
// Watch mode pauses the watcher while blocked on Console.ReadLine to avoid DropOldest loss.
using CSharpFunctionalExtensions;
using Enrollment.Application;
using Enrollment.Console;
using Enrollment.Infrastructure;
using FaceAiSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var services = new ServiceCollection()
    .AddLogging()
    .AddEnrollment(configuration)
    .AddEnrollmentWatch(configuration);

await using var provider = services.BuildServiceProvider(validateScopes: true);

// One-shot backward-compatible dispatch when args are supplied
if (args.Length > 0)
{
    return args[0].ToLowerInvariant() switch
    {
        "spike" => RunSpike(args[1..]),
        "register" => await RunRegisterAsync(args[1..], provider),
        "watch" => await RunWatchAsync(provider, CancellationToken.None),
        _ => PrintUsageAndFail(),
    };
}

// Interactive loop
while (true)
{
    Console.WriteLine();
    Console.WriteLine("CamGuard Enrollment — choose an option:");
    Console.WriteLine("  1) spike    — test FaceAiSharp on an image");
    Console.WriteLine("  2) register — manual enroll (prompt FullName + photo)");
    Console.WriteLine("  3) watch    — watch photos/enroll (prompt per file)");
    Console.WriteLine("  0) exit");
    Console.Write("> ");
    var choice = Console.ReadLine()?.Trim().ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(choice))
    {
        continue;
    }

    if (choice is "0" or "exit" or "q" or "quit")
    {
        break;
    }

    switch (choice)
    {
        case "1":
        case "spike":
            Console.Write("Image path: ");
            var spikePath = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(spikePath))
            {
                Console.Error.WriteLine("No path entered.");
                break;
            }
            RunSpike([spikePath]);
            break;

        case "2":
        case "register":
            Console.Write("FullName: ");
            var fullName = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                Console.Error.WriteLine("FullName cannot be empty.");
                break;
            }
            Console.Write("Photo path(s) (space-separated): ");
            var photoLine = Console.ReadLine();
            var photoPaths = photoLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
            if (photoPaths.Length == 0)
            {
                Console.Error.WriteLine("At least one photo path is required.");
                break;
            }
            var exitCode = await RunRegisterAsync([fullName, .. photoPaths], provider);
            Console.WriteLine(exitCode == 0 ? "Registered." : "Register failed.");
            break;

        case "3":
        case "watch":
            {
                Console.WriteLine("Watching photos/enroll — drop an image file, then enter FullName when prompted.");
                Console.WriteLine("Press Ctrl+C to stop watching and return to menu.");
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
                try
                {
                    await RunWatchAsync(provider, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Watch stopped.");
                }
                break;
            }

        default:
            Console.Error.WriteLine($"Unknown option '{choice}'.");
            PrintUsage();
            break;
    }
}

return 0;

static int RunSpike(string[] spikeArgs)
{
    if (spikeArgs.Length != 1)
    {
        Console.Error.WriteLine("Usage: spike <imagePath>");
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

static async Task<int> RunRegisterAsync(string[] registerArgs, ServiceProvider provider)
{
    if (registerArgs.Length < 2)
    {
        Console.Error.WriteLine("Usage: register \"<Full Name>\" <photo1> [photo2 ...]");
        return 1;
    }

    var fullName = registerArgs[0];
    var photoPaths = registerArgs[1..];
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

static async Task<int> RunWatchAsync(ServiceProvider provider, CancellationToken cancellationToken)
{
    using var scope = provider.CreateScope();
    var source = scope.ServiceProvider.GetRequiredService<EnrollmentWatchIngestionSource>();
    var archiver = scope.ServiceProvider.GetRequiredService<EnrollmentWatchArchiver>();
    var enrollmentService = scope.ServiceProvider.GetRequiredService<EnrollmentService>();

    await foreach (var path in source.WatchAsync(cancellationToken).ConfigureAwait(false))
    {
        source.Pause();
        try
        {
            Console.Write($"Member FullName for '{Path.GetFileName(path)}': ");
            var fullName = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                Console.Error.WriteLine("Invalid name — quarantining.");
                await archiver.MoveToQuarantineAsync(path, cancellationToken).ConfigureAwait(false);
                continue;
            }

            Result<Enrollment.Domain.MemberId, RegisterMemberError> result;
            try
            {
                await using var stream = File.OpenRead(path);
                result = await enrollmentService.EnrollAsync(fullName, stream, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Failed to read file: {ex.Message}");
                await archiver.MoveToQuarantineAsync(path, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (result.IsSuccess)
            {
                Console.WriteLine($"Enrolled '{fullName.Trim()}' => {result.Value.Value} (replaced if existed).");
                await archiver.MoveToProcessedAsync(path, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Console.Error.WriteLine($"Enrollment failed: {result.Error}");
                await archiver.MoveToQuarantineAsync(path, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            source.Resume();
        }
    }

    return 0;
}

static void PrintUsage() =>
    Console.Error.WriteLine("""
        Usage (one-shot):
          dotnet run --project src/Enrollment.Console -- spike <imagePath>
          dotnet run --project src/Enrollment.Console -- register "<Full Name>" <photo1> [photo2 ...]
          dotnet run --project src/Enrollment.Console -- watch
        Or run without args for interactive menu (spike / register / watch / exit).
        """);

static int PrintUsageAndFail()
{
    PrintUsage();
    return 1;
}
