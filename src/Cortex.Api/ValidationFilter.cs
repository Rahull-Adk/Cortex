using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Cortex.Api;

public sealed class ValidationFilter(IServiceProvider services, ProblemDetailsFactory problemDetailsFactory) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var modelState = new ModelStateDictionary();

        foreach (var argument in context.ActionArguments.Values.Where(value => value is not null))
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(argument!.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
                modelState.AddModelError(ToCamelCase(failure.PropertyName), failure.ErrorMessage);
        }

        if (modelState.IsValid)
        {
            await next();
            return;
        }

        var problem = problemDetailsFactory.CreateValidationProblemDetails(
            context.HttpContext, modelState, StatusCodes.Status400BadRequest);

        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
    }

    private static string ToCamelCase(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
