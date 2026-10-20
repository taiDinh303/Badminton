namespace Badminton.Core.Base;

/// <summary>A provider-independent persistence conflict. No SQL or user data is exposed.</summary>
public sealed class UniqueConstraintException(string? constraintName, Exception innerException)
    : Exception("A unique persistence constraint was violated.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
