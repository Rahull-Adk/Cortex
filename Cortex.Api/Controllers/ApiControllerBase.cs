using Cortex.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult<T>(Result<T> result, Func<T, IActionResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : Problem(result.Error);

    protected IActionResult FromResult(Result result, Func<IActionResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : Problem(result.Error);

    protected IActionResult Problem(Error error) =>
        Problem(statusCode: ToStatusCode(error.Type), detail: error.Message);

    private static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };
}
