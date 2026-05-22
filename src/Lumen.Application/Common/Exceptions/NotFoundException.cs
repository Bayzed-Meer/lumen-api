namespace Lumen.Application.Common.Exceptions;

using System.Net;

public class NotFoundException : AppException
{
    public NotFoundException(string name, object key)
        : base($"'{name}' ({key}) was not found.", (int)HttpStatusCode.NotFound) { }
}
