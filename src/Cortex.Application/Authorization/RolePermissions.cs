using System.Collections.Frozen;
using Cortex.Domain.Enums;

namespace Cortex.Application.Authorization;

public static class RolePermissions
{
    private static readonly FrozenSet<Permission> Member =
    [
        Permission.OrganizationView,
        Permission.MembersView,
    ];

    private static readonly FrozenSet<Permission> Admin =
    [
        .. Member,
        Permission.OrganizationUpdate,
        Permission.MembersManage,
        Permission.BillingView,
    ];

    private static readonly FrozenSet<Permission> Owner =
    [
        .. Admin,
        Permission.OrganizationDelete,
        Permission.OwnershipTransfer,
        Permission.BillingManage,
    ];

    public static bool Has(OrganizationRole role, Permission permission) => role switch
    {
        OrganizationRole.Owner => Owner.Contains(permission),
        OrganizationRole.Admin => Admin.Contains(permission),
        OrganizationRole.Member => Member.Contains(permission),
        _ => false,
    };
}
