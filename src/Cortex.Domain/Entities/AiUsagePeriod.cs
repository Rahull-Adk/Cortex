namespace Cortex.Domain.Entities;

public class AiUsagePeriod
{
    private AiUsagePeriod() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public int AnswersUsed { get; private set; }
}
