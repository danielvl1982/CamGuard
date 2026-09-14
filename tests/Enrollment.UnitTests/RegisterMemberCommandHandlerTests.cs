using CSharpFunctionalExtensions;
using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>
/// Application handler tests covering <c>member-enrollment</c> spec scenarios:
/// zero/multiple-face rejection, atomic persistence, and the happy path (task 9.3).
/// </summary>
public sealed class RegisterMemberCommandHandlerTests
{
    private static float[] ValidVector()
    {
        var vector = new float[FaceEmbedding.ExpectedDimension];
        vector[0] = 0.9f;
        return vector;
    }

    [Fact]
    public async Task Enrollment_succeeds_and_persists_when_exactly_one_face_is_detected()
    {
        var mocker = new AutoMocker();
        var embedding = FaceEmbedding.FromVector(ValidVector()).Value;
        mocker.GetMock<IFaceEmbeddingExtractor>()
            .Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(embedding));
        var handler = mocker.CreateInstance<RegisterMemberCommandHandler>();
        var command = new RegisterMemberCommand("Ada Lovelace", [new MemoryStream()]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        mocker.GetMock<IMemberVectorRepository>()
            .Verify(x => x.SaveAsync(It.Is<Member>(m => m.FullName == "Ada Lovelace"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Enrollment_is_rejected_and_nothing_is_persisted_when_zero_faces_are_detected()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFaceEmbeddingExtractor>()
            .Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<FaceEmbedding, FaceExtractionError>(FaceExtractionError.NoFaceDetected));
        var handler = mocker.CreateInstance<RegisterMemberCommandHandler>();
        var command = new RegisterMemberCommand("Ada Lovelace", [new MemoryStream()]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.NoFaceDetected, result.Error);
        mocker.GetMock<IMemberVectorRepository>()
            .Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Enrollment_is_rejected_and_nothing_is_persisted_when_multiple_faces_are_detected()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFaceEmbeddingExtractor>()
            .Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<FaceEmbedding, FaceExtractionError>(FaceExtractionError.MultipleFacesDetected));
        var handler = mocker.CreateInstance<RegisterMemberCommandHandler>();
        var command = new RegisterMemberCommand("Ada Lovelace", [new MemoryStream()]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.MultipleFacesDetected, result.Error);
        mocker.GetMock<IMemberVectorRepository>()
            .Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Enrollment_is_rejected_and_nothing_is_persisted_when_the_member_invariant_fails()
    {
        var mocker = new AutoMocker();
        var embedding = FaceEmbedding.FromVector(ValidVector()).Value;
        mocker.GetMock<IFaceEmbeddingExtractor>()
            .Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(embedding));
        var handler = mocker.CreateInstance<RegisterMemberCommandHandler>();
        var command = new RegisterMemberCommand("   ", [new MemoryStream()]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.InvalidMember, result.Error);
        mocker.GetMock<IMemberVectorRepository>()
            .Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// member-enrollment spec, Atomic Vector Persistence requirement, "Successful
    /// end-to-end enrollment" scenario extended to multiple photos: when every
    /// submitted photo independently passes the face-detection gate, the enrollment
    /// succeeds and persists exactly once.
    /// </summary>
    [Fact]
    public async Task Enrollment_succeeds_and_persists_once_when_multiple_photos_all_pass_the_gate()
    {
        var mocker = new AutoMocker();
        var firstEmbeddingVector = ValidVector();
        var firstEmbedding = FaceEmbedding.FromVector(firstEmbeddingVector).Value;
        var secondVector = ValidVector();
        secondVector[1] = 0.7f;
        var secondEmbedding = FaceEmbedding.FromVector(secondVector).Value;
        var thirdVector = ValidVector();
        thirdVector[2] = 0.5f;
        var thirdEmbedding = FaceEmbedding.FromVector(thirdVector).Value;
        var firstPhoto = new MemoryStream();
        var secondPhoto = new MemoryStream();
        var thirdPhoto = new MemoryStream();
        var extractorMock = mocker.GetMock<IFaceEmbeddingExtractor>();
        extractorMock.Setup(x => x.ExtractAsync(firstPhoto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(firstEmbedding));
        extractorMock.Setup(x => x.ExtractAsync(secondPhoto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(secondEmbedding));
        extractorMock.Setup(x => x.ExtractAsync(thirdPhoto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(thirdEmbedding));
        var handler = mocker.CreateInstance<RegisterMemberCommandHandler>();
        var command = new RegisterMemberCommand("Ada Lovelace", [firstPhoto, secondPhoto, thirdPhoto]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        extractorMock.Verify(x => x.ExtractAsync(firstPhoto, It.IsAny<CancellationToken>()), Times.Once);
        extractorMock.Verify(x => x.ExtractAsync(secondPhoto, It.IsAny<CancellationToken>()), Times.Once);
        extractorMock.Verify(x => x.ExtractAsync(thirdPhoto, It.IsAny<CancellationToken>()), Times.Once);
        mocker.GetMock<IMemberVectorRepository>()
            .Verify(x => x.SaveAsync(It.Is<Member>(m => m.FullName == "Ada Lovelace" && m.Embedding == firstEmbedding), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// member-enrollment spec, Atomic Vector Persistence requirement, "Enrollment
    /// with one invalid photo persists nothing" scenario: at least one of several
    /// submitted photos fails the face-detection gate (zero faces detected here) —
    /// the entire enrollment is rejected and nothing is persisted for any photo.
    /// </summary>
    [Fact]
    public async Task Enrollment_is_rejected_atomically_when_one_of_several_photos_has_zero_faces_detected()
    {
        var mocker = new AutoMocker();
        var validEmbedding = FaceEmbedding.FromVector(ValidVector()).Value;
        var firstPhoto = new MemoryStream();
        var invalidPhoto = new MemoryStream();
        var extractorMock = mocker.GetMock<IFaceEmbeddingExtractor>();
        extractorMock.Setup(x => x.ExtractAsync(firstPhoto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(validEmbedding));
        extractorMock.Setup(x => x.ExtractAsync(invalidPhoto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<FaceEmbedding, FaceExtractionError>(FaceExtractionError.NoFaceDetected));
        var handler = mocker.CreateInstance<RegisterMemberCommandHandler>();
        var command = new RegisterMemberCommand("Ada Lovelace", [firstPhoto, invalidPhoto]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.NoFaceDetected, result.Error);
        mocker.GetMock<IMemberVectorRepository>()
            .Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Same atomic-rejection scenario as above, with the failing photo triggering
    /// multiple-face rejection instead of zero-face rejection.
    /// </summary>
    [Fact]
    public async Task Enrollment_is_rejected_atomically_when_one_of_several_photos_has_multiple_faces_detected()
    {
        var mocker = new AutoMocker();
        var validEmbedding = FaceEmbedding.FromVector(ValidVector()).Value;
        var firstPhoto = new MemoryStream();
        var invalidPhoto = new MemoryStream();
        var extractorMock = mocker.GetMock<IFaceEmbeddingExtractor>();
        extractorMock.Setup(x => x.ExtractAsync(firstPhoto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<FaceEmbedding, FaceExtractionError>(validEmbedding));
        extractorMock.Setup(x => x.ExtractAsync(invalidPhoto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<FaceEmbedding, FaceExtractionError>(FaceExtractionError.MultipleFacesDetected));
        var handler = mocker.CreateInstance<RegisterMemberCommandHandler>();
        var command = new RegisterMemberCommand("Ada Lovelace", [firstPhoto, invalidPhoto]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.MultipleFacesDetected, result.Error);
        mocker.GetMock<IMemberVectorRepository>()
            .Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void RegisterMemberCommand_rejects_a_null_photo_list()
    {
        Assert.Throws<ArgumentNullException>(() => new RegisterMemberCommand("Ada Lovelace", null!));
    }

    [Fact]
    public void RegisterMemberCommand_rejects_an_empty_photo_list()
    {
        Assert.Throws<ArgumentException>(() => new RegisterMemberCommand("Ada Lovelace", []));
    }
}
