using Cortex.Application.Authorization;
using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Organizations.Dtos;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cortex.Application.Organizations;

public class MemberService
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OrganizationAuthorizer _authorizer;

    private readonly ILogger<MemberService> _logger;

    public MemberService(
        IOrganizationMembershipRepository memberships,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        OrganizationAuthorizer authorizer,
        ILogger<MemberService> logger)
    {
        _logger = logger;
        _memberships = memberships;
        _users = users;
        _unitOfWork = unitOfWork;
        _authorizer = authorizer;
    }

    public async Task<Result<IReadOnlyList<MemberDto>>> ListAsync(
        Guid actorUserId,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.MembersView, ct);
        if (access.IsFailure)
            return access.Error;

        var members = await _memberships.ListByOrganizationAsync(organizationId, ct);
        return members.Select(m => MapToDto(m, m.User)).ToList();
    }

    public async Task<Result<MemberDto>> ChangeRoleAsync(
        Guid actorUserId,
        Guid organizationId,
        Guid targetUserId,
        ChangeMemberRoleRequest request,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.MembersManage, ct);
        if (access.IsFailure)
            return access.Error;

        var target = await _memberships.FindWithUserAsync(organizationId, targetUserId, ct);
        if (target is null)
            return OrganizationErrors.MemberNotFound;

        if (target.Role == OrganizationRole.Owner || !IsAssignable(request.Role))
        {
            _logger.OwnerChangeBlocked(organizationId, actorUserId);
            return OrganizationErrors.OwnerImmutable;
        }

        var oldRole = target.Role;
        target.ChangeRole(request.Role);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.MemberRoleChanged(organizationId, actorUserId, targetUserId, oldRole, target.Role);

        return MapToDto(target, target.User);
    }

    public async Task<Result> RemoveAsync(
        Guid actorUserId,
        Guid organizationId,
        Guid targetUserId,
        CancellationToken ct = default)
    {
        // Leaving only requires being a member.
        var required = actorUserId == targetUserId ? Permission.OrganizationView : Permission.MembersManage;

        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, required, ct);
        if (access.IsFailure)
            return access.Error;

        var target = await _memberships.FindWithUserAsync(organizationId, targetUserId, ct);
        if (target is null)
            return OrganizationErrors.MemberNotFound;

        if (target.Role == OrganizationRole.Owner)
        {
            _logger.OwnerChangeBlocked(organizationId, actorUserId);
            return OrganizationErrors.OwnerImmutable;
        }

        _memberships.Remove(target);
        await _unitOfWork.SaveChangesAsync(ct);

        if (actorUserId == targetUserId)
            _logger.MemberLeft(organizationId, actorUserId);
        else
            _logger.MemberRemoved(organizationId, actorUserId, targetUserId);

        return Result.Success();
    }

    private static bool IsAssignable(OrganizationRole role) =>
        role is OrganizationRole.Admin or OrganizationRole.Member;

    private static MemberDto MapToDto(OrganizationMembership membership, User user) =>
        new(user.Id, user.Name, user.Email, user.AvatarUrl, membership.Role, membership.JoinedAt);
}
