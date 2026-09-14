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
        var command = new RegisterMemberCommand("Ada Lovelace", new MemoryStream());

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
        var command = new RegisterMemberCommand("Ada Lovelace", new MemoryStream());

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
        var command = new RegisterMemberCommand("Ada Lovelace", new MemoryStream());

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
        var command = new RegisterMemberCommand("   ", new MemoryStream());

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterMemberError.InvalidMember, result.Error);
        mocker.GetMock<IMemberVectorRepository>()
            .Verify(x => x.SaveAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
