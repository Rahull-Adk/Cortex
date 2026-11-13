using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api;

// Documents an error status in the OpenAPI schema with the problem details body every error uses.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesProblemAttribute(int statusCode)
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), statusCode, "application/problem+json");

[AttributeUsage(AttributeTargets.Method)]
public sealed class ProducesValidationProblemAttribute()
    : ProducesResponseTypeAttribute(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json");
