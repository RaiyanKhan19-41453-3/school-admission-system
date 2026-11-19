namespace Admissions.Services;

/// <summary>Domain exceptions — caught by the global exception middleware.</summary>
public class DomainException(string message) : Exception(message)
{
    public virtual int Status => 400;
    public virtual string Code => "BAD_REQUEST";
}

public sealed class ValidationException(string message) : DomainException(message);

public sealed class NotFoundException(string message) : DomainException(message)
{
    public override int Status => 404;
    public override string Code => "NOT_FOUND";
}

public sealed class ConflictException(string code, string message) : DomainException(message)
{
    public override int Status => 409;
    public override string Code => code;
}

public sealed class ForbiddenException(string message) : DomainException(message)
{
    public override int Status => 403;
    public override string Code => "FORBIDDEN";
}

public sealed class UnauthorizedException(string message) : DomainException(message)
{
    public override int Status => 401;
    public override string Code => "UNAUTHORIZED";
}
