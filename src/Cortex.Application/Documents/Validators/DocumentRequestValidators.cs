using Cortex.Application.Documents.Dtos;
using FluentValidation;

namespace Cortex.Application.Documents.Validators;

public sealed class StartUploadRequestValidator : AbstractValidator<StartUploadRequest>
{
    public StartUploadRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(DocumentRules.MaxFileNameLength)
            .Must(BePlainFileName)
            .WithMessage("File name must not contain path separators or control characters.");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(type => type is not null && DocumentRules.AllowedContentTypes.Contains(type.Trim()))
            .WithMessage($"Content type must be one of: {string.Join(", ", DocumentRules.AllowedContentTypes.Order())}.");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(DocumentRules.MaxFileSizeBytes)
            .WithMessage($"File size must be at most {DocumentRules.MaxFileSizeBytes / (1024 * 1024)} MB.");
    }

    private static bool BePlainFileName(string? name) =>
        name is not null && !name.Any(c => c is '/' or '\\' || char.IsControl(c));
}
