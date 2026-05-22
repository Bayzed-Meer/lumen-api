namespace Lumen.Application.Common.Exceptions;

using System.Net;

public class ConflictException : AppException
{
    public ConflictException(string message)
        : base(message, (int)HttpStatusCode.Conflict) { }
}
