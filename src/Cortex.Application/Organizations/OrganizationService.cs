using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Cortex.Application.Authorization;
using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Organizations.Dtos;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Organizations;

public partial class OrganizationService
{
    private const int MaxDerivedSlugLength = 90;

    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OrganizationAuthorizer _authorizer;

    public OrganizationService(
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        OrganizationAuthorizer authorizer)
    {
        _organizations = organizations;
        _memberships = memberships;
        _users = users;
        _unitOfWork = unitOfWork;
        _authorizer = authorizer;
    }

    public async Task<Result<OrganizationDto>> CreateAsync(
        Guid userId,
        CreateOrganizationRequest request,
        CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            return OrganizationErrors.UserNotFound;

        string slug;
        if (request.Slug is not null)
        {
            slug = request.Slug.Trim().ToLowerInvariant();
            if (await _organizations.ExistsBySlugAsync(slug, ct))
                return OrganizationErrors.SlugTaken;
        }
        else
        {
            slug = DeriveSlug(request.Name);
            if (slug.Length < 3 || await _organizations.ExistsBySlugAsync(slug, ct))
                slug = WithRandomSuffix(slug);
        }

        var organization = Organization.Create(request.Name, slug, user.Id);
        _organizations.Add(organization);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            _unitOfWork.DiscardChanges();
            return OrganizationErrors.SlugTaken;
        }

        return MapToDto(organization, OrganizationRole.Owner);
    }

    public async Task<IReadOnlyList<OrganizationDto>> ListMineAsync(Guid userId, CancellationToken ct = default)
    {
        var memberships = await _memberships.ListByUserAsync(userId, ct);
        return memberships.Select(m => MapToDto(m.Organization, m.Role)).ToList();
    }

    public async Task<Result<OrganizationDto>> GetAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(userId, organizationId, Permission.OrganizationView, ct);
        if (access.IsFailure)
            return access.Error;

        return MapToDto(access.Value.Organization, access.Value.Role);
    }

    public async Task<Result<OrganizationDto>> UpdateAsync(
        Guid userId,
        Guid organizationId,
        UpdateOrganizationRequest request,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(userId, organizationId, Permission.OrganizationUpdate, ct);
        if (access.IsFailure)
            return access.Error;

        var organization = access.Value.Organization;
        organization.UpdateDetails(request.Name, request.LogoUrl);

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(organization, access.Value.Role);
    }

    private static OrganizationDto MapToDto(Organization organization, OrganizationRole role) =>
        new(organization.Id, organization.Name, organization.Slug, organization.LogoUrl, role, organization.CreatedAt);

    private static string DeriveSlug(string name)
    {
        var slug = NonSlugCharacters().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        return slug.Length > MaxDerivedSlugLength ? slug[..MaxDerivedSlugLength].Trim('-') : slug;
    }

    // Names with no Latin letters or digits derive to an empty slug.
    private static string WithRandomSuffix(string slug)
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();
        return slug.Length == 0 ? $"business-{suffix}" : $"{slug}-{suffix}";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
