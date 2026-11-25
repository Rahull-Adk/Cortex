using Cortex.Application.Common;
using Cortex.Application.Documents;
using Cortex.Application.Documents.Dtos;
using Cortex.Application.Interfaces;
using Cortex.Application.Subscriptions;
using Cortex.Application.Tests.Support;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;
using Moq;

namespace Cortex.Application.Tests.Documents;

public class DocumentServiceTests
{
    private const long Megabyte = 1024 * 1024;

    private readonly DocumentFixture _fx = new();
    private readonly User _owner = TestData.VerifiedUser("owner@example.com", "Owner");
    private readonly User _admin = TestData.VerifiedUser("admin@example.com", "Admin");
    private readonly User _member = TestData.VerifiedUser("member@example.com", "Member");
    private readonly Organization _organization;

    public DocumentServiceTests()
    {
        _organization = _fx.Subs.HasOrganization(_owner);
        _fx.Subs.Orgs.HasMember(_organization, _admin, OrganizationRole.Admin);
        _fx.Subs.Orgs.HasMember(_organization, _member, OrganizationRole.Member);
        _fx.Subs.HasSubscription(_organization, _fx.Subs.Starter);
    }

    private DocumentService Service => _fx.Service;

    private static StartUploadRequest Pdf(long size = 2 * Megabyte, string name = "Menu 2026.pdf") =>
        new(name, "application/pdf", size);

    private async Task<UploadSessionDto> StartAsync(long size = 2 * Megabyte)
    {
        var result = await Service.StartUploadAsync(_owner.Id, _organization.Id, Pdf(size));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task<DocumentDto> UploadedAsync(long size = 2 * Megabyte)
    {
        var session = await StartAsync(size);
        _fx.ClientUploads(session.Document.Id, size);
        var completed = await Service.CompleteUploadAsync(_owner.Id, _organization.Id, session.Document.Id);
        Assert.True(completed.IsSuccess);
        return completed.Value;
    }

    private Task<StorageUsage> UsageAsync() => _fx.Subs.Usage.GetStorageAsync(_organization.Id);

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Admin)]
    public async Task StartUpload_OwnerOrAdmin_ReturnsSignedPutForStoredKey(OrganizationRole role)
    {
        var actor = role == OrganizationRole.Owner ? _owner : _admin;

        var result = await Service.StartUploadAsync(actor.Id, _organization.Id, Pdf());

        Assert.True(result.IsSuccess);
        var session = result.Value;
        var document = Assert.Single(_fx.StoredDocuments);
        Assert.Equal(document.Id, session.Document.Id);
        Assert.Equal(DocumentStatus.AwaitingUpload, session.Document.Status);
        Assert.Equal(actor.Id, session.Document.UploadedByUserId);
        Assert.Equal("PUT", session.Method);
        Assert.Contains(document.S3Key, session.UploadUrl);
        Assert.Equal("application/pdf", session.Headers["Content-Type"]);
        Assert.StartsWith($"orgs/{_organization.Id}/documents/{document.Id}", document.S3Key);
        _fx.Storage.Verify(s => s.CreateUploadUrlAsync(
            document.S3Key, "application/pdf", 2 * Megabyte, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task StartUpload_SignsNormalizedContentType()
    {
        var result = await Service.StartUploadAsync(
            _owner.Id, _organization.Id, new StartUploadRequest("notes.txt", " Text/Plain ", 10));

        Assert.Equal("text/plain", result.Value.Headers["Content-Type"]);
        _fx.Storage.Verify(s => s.CreateUploadUrlAsync(
            It.IsAny<string>(), "text/plain", 10, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task StartUpload_ReservesDocumentAndBytes()
    {
        await StartAsync(3 * Megabyte);

        Assert.Equal(new StorageUsage(1, 3 * Megabyte), await UsageAsync());
    }

    [Fact]
    public async Task StartUpload_Member_IsForbidden()
    {
        var result = await Service.StartUploadAsync(_member.Id, _organization.Id, Pdf());

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Empty(_fx.StoredDocuments);
        Assert.Equal(new StorageUsage(0, 0), await UsageAsync());
    }

    [Fact]
    public async Task StartUpload_NonMember_LooksLikeMissingBusiness()
    {
        var stranger = _fx.Subs.Orgs.HasUser("stranger@example.com");

        var result = await Service.StartUploadAsync(stranger.Id, _organization.Id, Pdf());

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task StartUpload_WithoutPaidSubscription_RequiresOne()
    {
        var other = _fx.Subs.HasOrganization(_admin);

        var result = await Service.StartUploadAsync(_admin.Id, other.Id, Pdf());

        Assert.Equal(SubscriptionErrors.SubscriptionRequired, result.Error);
        Assert.Empty(_fx.StoredDocuments);
    }

    [Fact]
    public async Task StartUpload_OverStorageQuota_CreatesNothing()
    {
        var result = await Service.StartUploadAsync(
            _owner.Id, _organization.Id, Pdf(2 * SubscriptionFixture.Gigabyte + 1));

        Assert.Equal(SubscriptionErrors.StorageQuotaExceeded, result.Error);
        Assert.Empty(_fx.StoredDocuments);
        _fx.Storage.Verify(s => s.CreateUploadUrlAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartUpload_SaveFails_ReleasesReservation()
    {
        _fx.Subs.Orgs.SaveHitsUniqueConstraint();

        await Assert.ThrowsAsync<UniqueConstraintException>(() =>
            Service.StartUploadAsync(_owner.Id, _organization.Id, Pdf()));

        Assert.Equal(new StorageUsage(0, 0), await UsageAsync());
        Assert.Equal(1, _fx.Subs.Orgs.DiscardCount);
    }

    [Fact]
    public async Task StartUpload_SigningFails_ReleasesReservationAndSavesNothing()
    {
        _fx.Storage.Setup(s => s.CreateUploadUrlAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("S3:BucketName is not configured."));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service.StartUploadAsync(_owner.Id, _organization.Id, Pdf()));

        Assert.Equal(new StorageUsage(0, 0), await UsageAsync());
        Assert.Equal(0, _fx.Subs.Orgs.SaveCount);
    }

    [Fact]
    public async Task CompleteUpload_FileStored_MovesToPending()
    {
        var session = await StartAsync();
        _fx.ClientUploads(session.Document.Id, 2 * Megabyte);

        var result = await Service.CompleteUploadAsync(_admin.Id, _organization.Id, session.Document.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Pending, result.Value.Status);
        Assert.Equal(DocumentStatus.Pending, _fx.StoredDocuments.Single().Status);
    }

    [Fact]
    public async Task CompleteUpload_Twice_ReturnsDocumentWithoutCheckingStorageAgain()
    {
        var uploaded = await UploadedAsync();
        _fx.Storage.Invocations.Clear();

        var again = await Service.CompleteUploadAsync(_owner.Id, _organization.Id, uploaded.Id);

        Assert.True(again.IsSuccess);
        Assert.Equal(DocumentStatus.Pending, again.Value.Status);
        _fx.Storage.Verify(s => s.GetObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteUpload_NothingStoredYet_KeepsWaiting()
    {
        var session = await StartAsync();

        var result = await Service.CompleteUploadAsync(_owner.Id, _organization.Id, session.Document.Id);

        Assert.Equal(DocumentErrors.UploadNotReceived, result.Error);
        Assert.Equal(DocumentStatus.AwaitingUpload, _fx.StoredDocuments.Single().Status);
        Assert.Equal(new StorageUsage(1, 2 * Megabyte), await UsageAsync());
    }

    [Fact]
    public async Task CompleteUpload_SizeMismatch_RemovesDocumentFileAndReservation()
    {
        var session = await StartAsync();
        _fx.ClientUploads(session.Document.Id, 2 * Megabyte + 1);
        var key = _fx.StoredDocuments.Single().S3Key;

        var result = await Service.CompleteUploadAsync(_owner.Id, _organization.Id, session.Document.Id);

        Assert.Equal(DocumentErrors.UploadMismatch, result.Error);
        Assert.Empty(_fx.StoredDocuments);
        Assert.Equal([key], _fx.DeletedKeys);
        Assert.Equal(new StorageUsage(0, 0), await UsageAsync());
    }

    [Fact]
    public async Task CompleteUpload_FailedDocument_IsRejected()
    {
        var session = await StartAsync();
        _fx.StoredDocuments.Single().MarkAsFailed("Upload expired.");

        var result = await Service.CompleteUploadAsync(_owner.Id, _organization.Id, session.Document.Id);

        Assert.Equal(DocumentErrors.UploadFailed, result.Error);
    }

    [Fact]
    public async Task CompleteUpload_ConcurrentChange_ReportsConflict()
    {
        var session = await StartAsync();
        _fx.ClientUploads(session.Document.Id, 2 * Megabyte);
        _fx.SaveHitsConcurrencyConflict();

        var result = await Service.CompleteUploadAsync(_owner.Id, _organization.Id, session.Document.Id);

        Assert.Equal(DocumentErrors.ConcurrentChange, result.Error);
        Assert.Equal(1, _fx.Subs.Orgs.DiscardCount);
    }

    [Fact]
    public async Task CompleteUpload_Member_IsForbidden()
    {
        var session = await StartAsync();
        _fx.ClientUploads(session.Document.Id, 2 * Megabyte);

        var result = await Service.CompleteUploadAsync(_member.Id, _organization.Id, session.Document.Id);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task CompleteUpload_DocumentOfAnotherBusiness_IsNotFound()
    {
        var session = await StartAsync();
        var other = _fx.Subs.HasOrganization(_admin);

        var result = await Service.CompleteUploadAsync(_admin.Id, other.Id, session.Document.Id);

        Assert.Equal(DocumentErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task List_Member_SeesEveryDocument()
    {
        var first = await StartAsync();
        var second = await StartAsync();

        var result = await Service.ListAsync(_member.Id, _organization.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, d => d.Id == first.Document.Id);
        Assert.Contains(result.Value, d => d.Id == second.Document.Id);
    }

    [Fact]
    public async Task Get_Member_SeesDocument()
    {
        var uploaded = await UploadedAsync();

        var result = await Service.GetAsync(_member.Id, _organization.Id, uploaded.Id);

        Assert.Equal(uploaded.Id, result.Value.Id);
        Assert.Equal("Menu 2026.pdf", result.Value.FileName);
    }

    [Fact]
    public async Task Download_UploadedDocument_SignsLinkWithOriginalName()
    {
        var uploaded = await UploadedAsync();

        var result = await Service.CreateDownloadLinkAsync(_member.Id, _organization.Id, uploaded.Id);

        Assert.True(result.IsSuccess);
        _fx.Storage.Verify(s => s.CreateDownloadUrlAsync(
            _fx.StoredDocuments.Single().S3Key, "Menu 2026.pdf", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Download_BeforeUpload_IsNotAvailable()
    {
        var session = await StartAsync();

        var result = await Service.CreateDownloadLinkAsync(_owner.Id, _organization.Id, session.Document.Id);

        Assert.Equal(DocumentErrors.NotDownloadable, result.Error);
    }

    [Fact]
    public async Task Delete_UploadedDocument_RemovesRowFileAndReservation()
    {
        var uploaded = await UploadedAsync(3 * Megabyte);
        var key = _fx.StoredDocuments.Single().S3Key;

        var result = await Service.DeleteAsync(_admin.Id, _organization.Id, uploaded.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_fx.StoredDocuments);
        Assert.Equal([key], _fx.DeletedKeys);
        Assert.Equal(new StorageUsage(0, 0), await UsageAsync());
    }

    [Fact]
    public async Task Delete_UploadInProgress_ReleasesReservation()
    {
        var session = await StartAsync();

        var result = await Service.DeleteAsync(_owner.Id, _organization.Id, session.Document.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(new StorageUsage(0, 0), await UsageAsync());
    }

    [Fact]
    public async Task Delete_StorageUnreachable_StillSucceeds()
    {
        var uploaded = await UploadedAsync();
        _fx.StorageDeleteFails();

        var result = await Service.DeleteAsync(_owner.Id, _organization.Id, uploaded.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_fx.StoredDocuments);
        Assert.Equal(new StorageUsage(0, 0), await UsageAsync());
    }

    [Fact]
    public async Task Delete_ConcurrentChange_KeepsReservationAndFile()
    {
        var uploaded = await UploadedAsync();
        _fx.SaveHitsConcurrencyConflict();

        var result = await Service.DeleteAsync(_owner.Id, _organization.Id, uploaded.Id);

        Assert.Equal(DocumentErrors.ConcurrentChange, result.Error);
        Assert.Empty(_fx.DeletedKeys);
        Assert.Equal(new StorageUsage(1, 2 * Megabyte), await UsageAsync());
    }

    [Fact]
    public async Task Delete_Member_IsForbidden()
    {
        var uploaded = await UploadedAsync();

        var result = await Service.DeleteAsync(_member.Id, _organization.Id, uploaded.Id);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Single(_fx.StoredDocuments);
    }

    [Fact]
    public async Task Delete_MissingDocument_IsNotFound()
    {
        var result = await Service.DeleteAsync(_owner.Id, _organization.Id, Guid.NewGuid());

        Assert.Equal(DocumentErrors.NotFound, result.Error);
    }
}
