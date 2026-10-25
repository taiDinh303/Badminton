namespace Badminton.Core.Base;

public sealed class UniqueConstraintException(string? constraintName, Exception innerException)
    : Exception("A unique persistence constraint was violated.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
