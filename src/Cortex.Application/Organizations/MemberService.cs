using Cortex.Application.Authorization;
using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Organizations.Dtos;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Organizations;

public class MemberService
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OrganizationAuthorizer _authorizer;

    public MemberService(
        IOrganizationMembershipRepository memberships,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        OrganizationAuthorizer authorizer)
    {
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

    public async Task<Result<MemberDto>> AddAsync(
        Guid actorUserId,
        Guid organizationId,
        AddMemberRequest request,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.MembersManage, ct);
        if (access.IsFailure)
            return access.Error;

        if (!IsAssignable(request.Role))
            return OrganizationErrors.OwnerImmutable;

        var user = await _users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (user is null || !user.IsActive)
            return OrganizationErrors.UserNotRegistered;

        if (await _memberships.FindWithUserAsync(organizationId, user.Id, ct) is not null)
            return OrganizationErrors.AlreadyMember;

        var membership = OrganizationMembership.Create(organizationId, user.Id, request.Role);
        _memberships.Add(membership);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            _unitOfWork.DiscardChanges();
            return OrganizationErrors.AlreadyMember;
        }

        return MapToDto(membership, user);
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
            return OrganizationErrors.OwnerImmutable;

        target.ChangeRole(request.Role);
        await _unitOfWork.SaveChangesAsync(ct);

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
            return OrganizationErrors.OwnerImmutable;

        _memberships.Remove(target);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static bool IsAssignable(OrganizationRole role) =>
        role is OrganizationRole.Admin or OrganizationRole.Member;

    private static MemberDto MapToDto(OrganizationMembership membership, User user) =>
        new(user.Id, user.Name, user.Email, user.AvatarUrl, membership.Role, membership.JoinedAt);
}
