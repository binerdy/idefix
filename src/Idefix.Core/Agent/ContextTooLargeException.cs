namespace Idefix.Core.Agent;

public sealed class ContextTooLargeException : Exception
{
    public ContextTooLargeException()
    {
    }

    public ContextTooLargeException(string message)
        : base(message)
    {
    }

    public ContextTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ContextTooLargeException(int estimatedTokens, int limit)
        : base($"The active documents need about {estimatedTokens} tokens, but the model context allows {limit}.")
    {
        this.EstimatedTokens = estimatedTokens;
        this.Limit = limit;
    }

    public int EstimatedTokens { get; }

    public int Limit { get; }
}
