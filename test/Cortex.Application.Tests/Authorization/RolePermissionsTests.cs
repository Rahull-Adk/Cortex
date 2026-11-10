using Cortex.Application.Authorization;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Authorization;

public class RolePermissionsTests
{
    private static readonly OrganizationRole[] RealRoles =
        [OrganizationRole.Owner, OrganizationRole.Admin, OrganizationRole.Member];

    // The approved v1 matrix: permission, owner, admin, member.
    public static TheoryData<Permission, bool, bool, bool> Matrix => new()
    {
        { Permission.OrganizationView, true, true, true },
        { Permission.OrganizationUpdate, true, true, false },
        { Permission.OrganizationDelete, true, false, false },
        { Permission.MembersView, true, true, true },
        { Permission.MembersManage, true, true, false },
        { Permission.OwnershipTransfer, true, false, false },
        { Permission.BillingView, true, true, false },
        { Permission.BillingManage, true, false, false },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Has_EachRole_MatchesApprovedMatrix(Permission permission, bool owner, bool admin, bool member)
    {
        var actualOwner = RolePermissions.Has(OrganizationRole.Owner, permission);
        var actualAdmin = RolePermissions.Has(OrganizationRole.Admin, permission);
        var actualMember = RolePermissions.Has(OrganizationRole.Member, permission);

        Assert.Equal(owner, actualOwner);
        Assert.Equal(admin, actualAdmin);
        Assert.Equal(member, actualMember);
    }

    [Fact]
    public void Matrix_CoversEveryPermission()
    {
        var covered = Matrix.Select(row => (Permission)row[0]).ToHashSet();

        Assert.Equal(Enum.GetValues<Permission>().ToHashSet(), covered);
    }

    [Theory]
    [InlineData(OrganizationRole.None)]
    [InlineData((OrganizationRole)99)]
    [InlineData((OrganizationRole)(-1))]
    public void Has_RoleThatIsNotARealRole_GrantsNothing(OrganizationRole role)
    {
        var granted = Enum.GetValues<Permission>().Where(permission => RolePermissions.Has(role, permission));

        Assert.Empty(granted);
    }

    [Fact]
    public void Has_UndefinedPermission_IsDeniedForEveryRole()
    {
        var undefined = (Permission)999;

        var grantedTo = RealRoles.Where(role => RolePermissions.Has(role, undefined));

        Assert.Empty(grantedTo);
    }

    [Fact]
    public void Has_Owner_HoldsEveryPermission()
    {
        var missing = Enum.GetValues<Permission>()
            .Where(permission => !RolePermissions.Has(OrganizationRole.Owner, permission));

        Assert.Empty(missing);
    }

    [Fact]
    public void Has_AdminPermissions_AreAllHeldByOwner()
    {
        var adminOnly = Enum.GetValues<Permission>().Where(permission =>
            RolePermissions.Has(OrganizationRole.Admin, permission)
            && !RolePermissions.Has(OrganizationRole.Owner, permission));

        Assert.Empty(adminOnly);
    }

    [Fact]
    public void Has_MemberPermissions_AreAllHeldByAdmin()
    {
        var memberOnly = Enum.GetValues<Permission>().Where(permission =>
            RolePermissions.Has(OrganizationRole.Member, permission)
            && !RolePermissions.Has(OrganizationRole.Admin, permission));

        Assert.Empty(memberOnly);
    }

    [Fact]
    public void Has_Member_CannotChangeAnything()
    {
        Permission[] readOnly = [Permission.OrganizationView, Permission.MembersView];

        var granted = Enum.GetValues<Permission>()
            .Where(permission => RolePermissions.Has(OrganizationRole.Member, permission));

        Assert.Equal(readOnly.ToHashSet(), granted.ToHashSet());
    }
}
