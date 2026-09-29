namespace CulinaryBlog.Domain.Exceptions;

public abstract class DomainException(
    string errorCode,
    string? detail = null) : Exception(detail)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class NotFoundException(
    string errorCode,
    string? detail = null) : DomainException(errorCode, detail);

public sealed class ConflictException(
    string errorCode,
    string? detail = null) : DomainException(errorCode, detail);

public sealed class BusinessRuleException(
    string errorCode,
    string? detail = null) : DomainException(errorCode, detail);

public sealed class ForbiddenException(
    string errorCode,
    string? detail = null) : DomainException(errorCode, detail);

public sealed class ConcurrencyException(
    string errorCode,
    string? detail = null) : DomainException(errorCode, detail);
