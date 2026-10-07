using BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web;

public static class ResultHttpExtensions
{
    /// <summary>Maps a domain/application <see cref="Error"/> to an RFC 7807 problem response.</summary>
    public static IResult ToProblem(this Error error) =>
        TypedResults.Problem(
            statusCode: error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status422UnprocessableEntity,
            },
            title: error.Code,
            detail: error.Description);

    public static IResult Match(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error.ToProblem();

    public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();
}
