using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Cortex.Application.Common;

public static class CortexTelemetry
{
    public const string Name = "Cortex";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);
}
