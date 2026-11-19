using Cortex.Application.Authorization;
using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Organizations.Dtos;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cortex.Application.Organizations;

public class InvitationService
{
    public static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    private readonly IOrganizationInvitationRepository _invitations;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IUserRepository _users;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OrganizationAuthorizer _authorizer;
    private readonly TimeProvider _clock;

    private readonly ILogger<InvitationService> _logger;

    public InvitationService(
        IOrganizationInvitationRepository invitations,
        IOrganizationMembershipRepository memberships,
        IUserRepository users,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        OrganizationAuthorizer authorizer,
        TimeProvider clock,
        ILogger<InvitationService> logger)
    {
        _logger = logger;
        _invitations = invitations;
        _memberships = memberships;
        _users = users;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _authorizer = authorizer;
        _clock = clock;
    }

    public async Task<Result<InvitationDto>> InviteAsync(
        Guid actorUserId,
        Guid organizationId,
        InviteMemberRequest request,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.MembersManage, ct);
        if (access.IsFailure)
            return access.Error;

        if (!IsAssignable(request.Role))
        {
            _logger.InvitationRejected(organizationId, "the Owner role cannot be invited");
            return OrganizationErrors.OwnerImmutable;
        }

        var inviter = await _users.FindByIdAsync(actorUserId, ct);
        if (inviter is null)
            return OrganizationErrors.UserNotFound;

        var email = request.Email.Trim().ToLowerInvariant();

        // The invited person does not need an account yet. They create one before accepting.
        var existingUser = await _users.FindByEmailAsync(email, ct);
        if (existingUser is not null
            && await _memberships.FindWithUserAsync(organizationId, existingUser.Id, ct) is not null)
        {
            _logger.InvitationRejected(organizationId, "the email already belongs to a member");
            return OrganizationErrors.AlreadyMember;
        }

        var rawToken = SecureToken.Generate();
        var tokenHash = SecureToken.Hash(rawToken);

        var invitation = await _invitations.FindByEmailAsync(organizationId, email, ct);
        var renewed = invitation is not null;
        if (invitation is null)
        {
            invitation = OrganizationInvitation.Create(organizationId, email, request.Role, tokenHash, inviter.Id, Validity);
            _invitations.Add(invitation);
        }
        else
        {
            invitation.Renew(request.Role, tokenHash, inviter.Id, Validity);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            _unitOfWork.DiscardChanges();
            _logger.InvitationRejected(organizationId, "the same email was invited concurrently");
            return OrganizationErrors.InvitationConflict;
        }

        await _emailService.SendOrganizationInvitationAsync(
            email, access.Value.Organization.Name, inviter.Name, invitation.Role.ToString(), rawToken, ct);
        _logger.InvitationSent(organizationId, invitation.Id, inviter.Id, invitation.Role, renewed);

        return new InvitationDto(
            invitation.Id, invitation.Email, invitation.Role, inviter.Name, invitation.CreatedAt, invitation.ExpiresAt);
    }

    public async Task<Result<IReadOnlyList<InvitationDto>>> ListAsync(
        Guid actorUserId,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.MembersManage, ct);
        if (access.IsFailure)
            return access.Error;

        var invitations = await _invitations.ListByOrganizationAsync(organizationId, ct);
        return invitations
            .Select(i => new InvitationDto(i.Id, i.Email, i.Role, i.InvitedBy.Name, i.CreatedAt, i.ExpiresAt))
            .ToList();
    }

    public async Task<Result> RevokeAsync(
        Guid actorUserId,
        Guid organizationId,
        Guid invitationId,
        CancellationToken ct = default)
    {
        var access = await _authorizer.AuthorizeAsync(actorUserId, organizationId, Permission.MembersManage, ct);
        if (access.IsFailure)
            return access.Error;

        var invitation = await _invitations.FindByIdAsync(organizationId, invitationId, ct);
        if (invitation is null)
            return OrganizationErrors.InvitationNotFound;

        _invitations.Remove(invitation);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.InvitationRevoked(organizationId, invitation.Id, actorUserId);

        return Result.Success();
    }

    // Anonymous: lets the accept page say who invited whom before the person signs in.
    public async Task<Result<InvitationPreviewDto>> PreviewAsync(string rawToken, CancellationToken ct = default)
    {
        var invitation = await FindUsableAsync(rawToken, ct);
        if (invitation is null)
        {
            _logger.InvitationTokenRejected();
            return OrganizationErrors.InvalidInvitation;
        }

        // An account that never verified its email is replaced by the invited signup, so it does not count.
        var user = await _users.FindByEmailAsync(invitation.Email, ct);

        return new InvitationPreviewDto(
            invitation.Organization.Name,
            invitation.Organization.LogoUrl,
            invitation.Email,
            invitation.Role,
            invitation.InvitedBy.Name,
            invitation.ExpiresAt,
            AccountExists: user is not null && user.EmailVerifiedAt.HasValue);
    }

    public async Task<Result<OrganizationDto>> AcceptAsync(
        Guid userId,
        AcceptInvitationRequest request,
        CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            return OrganizationErrors.UserNotFound;

        var invitation = await FindUsableAsync(request.Token, ct);
        if (invitation is null)
        {
            _logger.InvitationTokenRejected();
            return OrganizationErrors.InvalidInvitation;
        }

        // Holding the link is not enough: it only works for the address it was sent to.
        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            _logger.InvitationEmailMismatch(invitation.Id, user.Id);
            return OrganizationErrors.InvitationForAnotherEmail;
        }

        var organization = invitation.Organization;
        var role = invitation.Role;

        var existing = await _memberships.FindWithUserAsync(organization.Id, user.Id, ct);
        if (existing is not null)
            role = existing.Role;
        else
            _memberships.Add(OrganizationMembership.Create(organization.Id, user.Id, role));

        _invitations.Remove(invitation);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is UniqueConstraintException or ConcurrencyConflictException)
        {
            _unitOfWork.DiscardChanges();
            _logger.InvitationRejected(organization.Id, "the user joined concurrently");
            return OrganizationErrors.AlreadyMember;
        }

        _logger.InvitationAccepted(organization.Id, invitation.Id, user.Id, role);
        return new OrganizationDto(
            organization.Id, organization.Name, organization.Slug, organization.LogoUrl, role, organization.CreatedAt);
    }

    private async Task<OrganizationInvitation?> FindUsableAsync(string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var invitation = await _invitations.FindByTokenHashAsync(SecureToken.Hash(rawToken.Trim()), ct);
        if (invitation is null || !invitation.IsUsable(_clock.GetUtcNow().UtcDateTime))
            return null;

        return invitation;
    }

    private static bool IsAssignable(OrganizationRole role) =>
        role is OrganizationRole.Admin or OrganizationRole.Member;
}
