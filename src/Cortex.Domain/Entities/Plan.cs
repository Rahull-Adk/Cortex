namespace Cortex.Domain.Entities;

public class Plan
{
    public const string FreeCode = "free";

    private Plan() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public long PriceMinor { get; private set; }
    public string Currency { get; private set; } = default!;
    public int AiAnswersPerPeriod { get; private set; }
    public int MaxDocuments { get; private set; }
    public long MaxStorageBytes { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public bool IsPaid => PriceMinor > 0;

    public static Plan Create(
        string code,
        string name,
        long priceMinor,
        string currency,
        int aiAnswersPerPeriod,
        int maxDocuments,
        long maxStorageBytes,
        int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentOutOfRangeException.ThrowIfNegative(priceMinor);
        ArgumentOutOfRangeException.ThrowIfNegative(aiAnswersPerPeriod);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDocuments);
        ArgumentOutOfRangeException.ThrowIfNegative(maxStorageBytes);

        return new Plan
        {
            Id = Guid.NewGuid(),
            Code = code.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            PriceMinor = priceMinor,
            Currency = currency.Trim().ToLowerInvariant(),
            AiAnswersPerPeriod = aiAnswersPerPeriod,
            MaxDocuments = maxDocuments,
            MaxStorageBytes = maxStorageBytes,
            SortOrder = sortOrder,
        };
    }
}
