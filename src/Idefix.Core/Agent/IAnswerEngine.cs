namespace Idefix.Core.Agent;

public interface IAnswerEngine
{
    IAsyncEnumerable<AnswerEvent> AskAsync(AnswerRequest request, CancellationToken cancellationToken);
}
