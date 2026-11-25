using Cortex.Application.Documents;
using Cortex.Application.Documents.Dtos;
using Cortex.Application.Documents.Validators;

namespace Cortex.Application.Tests.Validators;

public class DocumentRequestValidatorTests
{
    private readonly StartUploadRequestValidator _validator = new();

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("text/plain")]
    [InlineData("TEXT/MARKDOWN")]
    [InlineData("text/csv")]
    public void AllowedContentType_IsValid(string contentType)
    {
        var result = _validator.Validate(new StartUploadRequest("file.bin", contentType, 1));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    [InlineData("application/octet-stream")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("")]
    public void OtherContentType_IsRejected(string contentType)
    {
        var result = _validator.Validate(new StartUploadRequest("file.txt", contentType, 1));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(StartUploadRequest.ContentType));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(DocumentRules.MaxFileSizeBytes + 1)]
    public void SizeOutOfRange_IsRejected(long size)
    {
        var result = _validator.Validate(new StartUploadRequest("file.pdf", "application/pdf", size));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(StartUploadRequest.SizeBytes));
    }

    [Fact]
    public void MaxSize_IsValid()
    {
        var result = _validator.Validate(new StartUploadRequest("file.pdf", "application/pdf", DocumentRules.MaxFileSizeBytes));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("folder/file.pdf")]
    [InlineData("..\file.pdf")]
    [InlineData("file\n.pdf")]
    public void UnsafeFileName_IsRejected(string fileName)
    {
        var result = _validator.Validate(new StartUploadRequest(fileName, "application/pdf", 1));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(StartUploadRequest.FileName));
    }

    [Fact]
    public void TooLongFileName_IsRejected()
    {
        var result = _validator.Validate(new StartUploadRequest(new string('a', 252) + ".pdf", "application/pdf", 1));

        Assert.False(result.IsValid);
    }
}
