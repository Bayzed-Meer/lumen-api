using System.Net;

namespace Lumen.Application.Common.Exceptions;

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message)
        : base(message, (int)HttpStatusCode.Unauthorized) { }
}
