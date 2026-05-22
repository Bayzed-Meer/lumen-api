namespace Lumen.Application.Common.Exceptions;

using System.Net;

public class ValidationException : AppException
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", (int)HttpStatusCode.BadRequest)
    {
        Errors = errors;
    }
}
