using System.Globalization;
using System.Text;

namespace Idefix.Core.Agent;

public static class PromptComposer
{
    public const string NothingFound = "No section of the user's documents matched this question.";

    public static string SystemPrompt(string prePrompt, IReadOnlyCollection<string> plugins, bool mayRequestMore = false)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(prePrompt))
        {
            builder.AppendLine(prePrompt.Trim()).AppendLine();
        }

        builder.AppendLine("You are Idefix, an assistant that answers strictly from the user's documents.");
        builder.Append(CultureInfo.InvariantCulture, $"Active knowledge plugins: {(plugins.Count == 0 ? "(none)" : string.Join(", ", plugins))}.").AppendLine();
        builder.AppendLine("The relevant sections of the user's documents follow the question, each page introduced by a marker like --- [FileName.pdf, p. N] ---.");
        builder.AppendLine("Cite every factual statement directly after the sentence in the exact form [FileName.pdf, p. N], using the page marker the information comes from.");
        if (mayRequestMore)
        {
            builder.AppendLine("The sections come from a keyword search. If they answer the question, answer right away. If something is missing, search again with other words or look at the table of contents before you answer.");
        }

        builder.AppendLine("If the documents do not answer the question, tell the user in one short sentence, addressing them directly, that you could not find anything about it in their documents. Never answer from general knowledge and never guess.");
        builder.AppendLine("Answer in the language of the user's question.");
        return builder.ToString();
    }

    public static string SelectionPrompt(string question, string outline, int budgetTokens) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $$"""
            You help pick document sections for answering a question. Below is the table of contents of the user's documents.
            Each line is: id | file | pages | section title | size | beginning of the text.

            Question: {{question}}

            Table of contents:
            {{outline}}
            Reply with JSON only, in the form {"sections": ["S3", "S7"]}, listing every section needed to answer the question,
            most relevant first, with a combined size of at most {{budgetTokens}} tokens. Include sections in other languages and with
            synonymous wording when they are relevant. Reply {"sections": []} when no section is relevant.
            """);

    public static string Question(string question, DocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Summary.Documents.Count == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{question}\n\nDocuments: none. {NothingFound}")
            : string.Create(CultureInfo.InvariantCulture, $"{question}\n\nDocuments:\n{context.Text}");
    }
}
