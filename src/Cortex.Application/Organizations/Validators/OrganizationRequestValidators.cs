using Cortex.Application.Organizations.Dtos;
using Cortex.Domain.Enums;
using FluentValidation;

namespace Cortex.Application.Organizations.Validators;

public sealed class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Slug)
            .Length(3, 100)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug may contain only lowercase letters, numbers and single hyphens between them.")
            .When(x => x.Slug is not null);
    }
}

public sealed class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.LogoUrl)
            .MaximumLength(2048)
            .Must(BeHttpUrl)
            .WithMessage("Logo URL must be an absolute http or https URL.")
            .When(x => x.LogoUrl is not null);
    }

    private static bool BeHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

public sealed class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Role).Must(AssignableRoles.Contains).WithMessage(AssignableRoles.Message);
    }
}

public sealed class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator() =>
        RuleFor(x => x.Role).Must(AssignableRoles.Contains).WithMessage(AssignableRoles.Message);
}

internal static class AssignableRoles
{
    public const string Message = "Role must be Admin or Member.";

    public static bool Contains(OrganizationRole role) =>
        role is OrganizationRole.Admin or OrganizationRole.Member;
}
