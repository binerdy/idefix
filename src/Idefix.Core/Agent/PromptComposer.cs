using System.Globalization;
using System.Text;

namespace Idefix.Core.Agent;

public static class PromptComposer
{
    public static string SystemPrompt(string prePrompt, IReadOnlyCollection<string> plugins)
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
        builder.AppendLine("If the documents do not contain the answer, say so plainly instead of guessing.");
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

    public static string FollowUpPrompt(string question, string outline, DocumentContext gathered, int remainingTokens) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $$"""
            You are collecting document sections to answer a question. You already have the sections below.
            Decide whether they are enough. If something is missing, pick more sections from the table of contents.

            Question: {{question}}

            Sections you already have:
            {{gathered.Text}}
            Table of contents (id | file | pages | section title | size | beginning of the text):
            {{outline}}
            Reply with JSON only. Use {"sections": []} when the sections you have are enough, otherwise
            {"sections": ["S12"]} with the additional sections you need, at most {{remainingTokens}} tokens in total.
            """);

    public static string Question(string question, DocumentContext context) =>
        string.Create(CultureInfo.InvariantCulture, $"{question}\n\nDocuments:\n{context.Text}");
}
