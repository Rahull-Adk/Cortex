using Cortex.Domain.Enums;

namespace Cortex.Application.Organizations.Dtos;

public sealed record CreateOrganizationRequest(string Name, string? Slug);

public sealed record UpdateOrganizationRequest(string Name, string? LogoUrl);

public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    OrganizationRole Role,
    DateTime CreatedAt);

public sealed record AddMemberRequest(string Email, OrganizationRole Role);

public sealed record ChangeMemberRoleRequest(OrganizationRole Role);

public sealed record MemberDto(
    Guid UserId,
    string Name,
    string Email,
    string? AvatarUrl,
    OrganizationRole Role,
    DateTime JoinedAt);
