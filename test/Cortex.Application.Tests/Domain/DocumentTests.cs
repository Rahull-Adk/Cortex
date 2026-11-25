using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Domain;

public class DocumentTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    private static Document Create(string fileName = " Menu.PDF ", string contentType = " Application/PDF ") =>
        Document.Create(OrganizationId, Guid.NewGuid(), fileName, contentType, 1024);

    [Fact]
    public void Create_NormalizesAndAwaitsUpload()
    {
        var document = Create();

        Assert.Equal("Menu.PDF", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(DocumentStatus.AwaitingUpload, document.Status);
        Assert.Equal($"orgs/{OrganizationId}/documents/{document.Id}.pdf", document.S3Key);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("notes")]
    [InlineData("report.p df")]
    [InlineData("archive.averyveryverylongextension")]
    public void Create_UnsafeOrMissingExtension_IsLeftOutOfKey(string fileName)
    {
        var document = Create(fileName);

        Assert.Equal($"orgs/{OrganizationId}/documents/{document.Id}", document.S3Key);
    }

    [Fact]
    public void Create_InvalidInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => Document.Create(Guid.Empty, Guid.NewGuid(), "a.pdf", "application/pdf", 1));
        Assert.Throws<ArgumentException>(() => Document.Create(OrganizationId, Guid.Empty, "a.pdf", "application/pdf", 1));
        Assert.Throws<ArgumentException>(() => Document.Create(OrganizationId, Guid.NewGuid(), " ", "application/pdf", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.Create(OrganizationId, Guid.NewGuid(), "a.pdf", "application/pdf", 0));
    }

    [Fact]
    public void Lifecycle_UploadedThenProcessedThenReady()
    {
        var document = Create();

        document.MarkUploaded(2048);
        Assert.Equal(DocumentStatus.Pending, document.Status);
        Assert.Equal(2048, document.SizeBytes);

        document.MarkProcessing();
        document.MarkProcessing();
        Assert.Equal(DocumentStatus.Processing, document.Status);

        document.MarkReady();
        Assert.Equal(DocumentStatus.Ready, document.Status);
    }

    [Fact]
    public void MarkUploaded_Twice_Throws()
    {
        var document = Create();
        document.MarkUploaded(1024);

        Assert.Throws<InvalidOperationException>(() => document.MarkUploaded(1024));
    }

    [Fact]
    public void MarkProcessing_BeforeUpload_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Create().MarkProcessing());
    }

    [Fact]
    public void MarkAsFailed_TruncatesLongErrorAndIsFinal()
    {
        var document = Create();

        document.MarkAsFailed(new string('x', 5000));

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal(2000, document.Error!.Length);
        Assert.Throws<InvalidOperationException>(() => document.MarkUploaded(1024));
        Assert.Throws<InvalidOperationException>(() => document.MarkAsFailed("again"));
    }
}
