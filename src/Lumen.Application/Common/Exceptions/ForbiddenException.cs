using System.Net;

namespace Lumen.Application.Common.Exceptions;

public class ForbiddenException : AppException
{
    public ForbiddenException(string message)
        : base(message, (int)HttpStatusCode.Forbidden) { }
}
