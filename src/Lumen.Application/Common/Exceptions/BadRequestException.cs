namespace Lumen.Application.Common.Exceptions;

using System.Net;

public class BadRequestException : AppException
{
    public BadRequestException(string message)
        : base(message, (int)HttpStatusCode.BadRequest) { }
}
