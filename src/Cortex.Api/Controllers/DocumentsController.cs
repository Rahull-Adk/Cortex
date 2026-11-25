using Cortex.Application.Documents;
using Cortex.Application.Documents.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

[Authorize]
[Tags("Documents")]
[Route("api/organizations/{organizationId:guid}/documents")]
public class DocumentsController : ApiControllerBase
{
    private readonly DocumentService _documents;

    public DocumentsController(DocumentService documents) => _documents = documents;

    [HttpPost]
    [EndpointSummary("Start a document upload")]
    [EndpointDescription("Owner and Admin only. Counts the file against the plan and returns a short-lived upload URL. PUT the raw file bytes to uploadUrl with exactly the returned headers (the browser sets Content-Length itself, and it must equal sizeBytes), then call the complete endpoint. Allowed types: PDF, DOCX, plain text, Markdown and CSV, up to 50 MB.")]
    [ProducesResponseType<UploadSessionDto>(StatusCodes.Status201Created, "application/json", Description = "Document created. Upload the file before expiresAt.")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
    [ProducesProblem(StatusCodes.Status402PaymentRequired, Description = "No paid subscription, or the plan's document or storage limit is reached. Check code.")]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "The caller is a Member.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business does not exist or the caller is not a member of it.")]
    public async Task<IActionResult> StartUpload(
        Guid organizationId,
        [FromBody] StartUploadRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _documents.StartUploadAsync(userId, organizationId, request, ct);
        return FromResult(result, session => StatusCode(StatusCodes.Status201Created, session));
    }

    [HttpPost("{documentId:guid}/complete")]
    [EndpointSummary("Confirm a finished upload")]
    [EndpointDescription("Owner and Admin only. Call after the PUT to the upload URL succeeds. Checks the stored file and queues the document for processing. Calling it again returns the document unchanged.")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK, "application/json", Description = "The document, now Pending processing.")]
    [ProducesValidationProblem(Description = "The stored file does not match the declared size. The document was removed: start a new upload.")]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "The caller is a Member.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business or the document does not exist, or the caller is not a member.")]
    [ProducesProblem(StatusCodes.Status409Conflict, Description = "The file has not reached storage yet, the upload failed, or the document changed at the same time.")]
    public async Task<IActionResult> CompleteUpload(Guid organizationId, Guid documentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _documents.CompleteUploadAsync(userId, organizationId, documentId, ct);
        return FromResult(result, Ok);
    }

    [HttpGet]
    [EndpointSummary("List documents")]
    [EndpointDescription("Any member. Newest first, including uploads still in progress (status AwaitingUpload).")]
    [ProducesResponseType<IReadOnlyList<DocumentDto>>(StatusCodes.Status200OK, "application/json", Description = "Documents of the business.")]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business does not exist or the caller is not a member of it.")]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _documents.ListAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }

    [HttpGet("{documentId:guid}")]
    [EndpointSummary("Get a document")]
    [EndpointDescription("Any member. Poll it to follow the processing status.")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK, "application/json", Description = "The document.")]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business or the document does not exist, or the caller is not a member.")]
    public async Task<IActionResult> Get(Guid organizationId, Guid documentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _documents.GetAsync(userId, organizationId, documentId, ct);
        return FromResult(result, Ok);
    }

    [HttpGet("{documentId:guid}/download")]
    [EndpointSummary("Get a download link")]
    [EndpointDescription("Any member. Returns a link that downloads the original file and stops working after a few minutes. Request a new one each time.")]
    [ProducesResponseType<DownloadLinkDto>(StatusCodes.Status200OK, "application/json", Description = "A short-lived download link.")]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business or the document does not exist, or the caller is not a member.")]
    [ProducesProblem(StatusCodes.Status409Conflict, Description = "The file has not been uploaded, or the upload failed.")]
    public async Task<IActionResult> Download(Guid organizationId, Guid documentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _documents.CreateDownloadLinkAsync(userId, organizationId, documentId, ct);
        return FromResult(result, Ok);
    }

    [HttpDelete("{documentId:guid}")]
    [EndpointSummary("Delete a document")]
    [EndpointDescription("Owner and Admin only. Removes the document and its file and frees its share of the plan's limits. Also cancels an upload in progress.")]
    [ProducesResponseType(StatusCodes.Status204NoContent, Description = "Deleted.")]
    [ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
    [ProducesProblem(StatusCodes.Status403Forbidden, Description = "The caller is a Member.")]
    [ProducesProblem(StatusCodes.Status404NotFound, Description = "The business or the document does not exist, or the caller is not a member.")]
    [ProducesProblem(StatusCodes.Status409Conflict, Description = "The document changed at the same time. Reload and retry.")]
    public async Task<IActionResult> Delete(Guid organizationId, Guid documentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _documents.DeleteAsync(userId, organizationId, documentId, ct);
        return FromResult(result, NoContent);
    }
}
