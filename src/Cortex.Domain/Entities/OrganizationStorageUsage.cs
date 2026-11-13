namespace Cortex.Domain.Entities;

public class OrganizationStorageUsage
{
    private OrganizationStorageUsage() { }

    public Guid OrganizationId { get; private set; }
    public int DocumentCount { get; private set; }
    public long StorageBytes { get; private set; }
}
