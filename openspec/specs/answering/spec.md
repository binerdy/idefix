# answering Specification

## Purpose
Answer questions from the relevant Markdown sections of the user's active documents, found by a local search and extended by the model when needed, with verifiable citations.

## Requirements

### Requirement: Local section search
When the active sections together exceed a small threshold (8k tokens), the system SHALL search them locally and send the best matches (at most eight) with the question. The search ranks by BM25 over stemmed words (70 %) and groups of three letters (30 %), counts titles twice, folds umlauts and ignores stop words. No model call and no embeddings are used. The index is kept in memory and rebuilt when the sections change. Small libraries are sent completely without this step.

#### Scenario: Only matching sections are sent
- **WHEN** the question mentions "Kündigungsfrist" and one of 42 sections contains it
- **THEN** the first answer request contains that section, with its page markers `--- [FileName.pdf, p. N] ---`, and no selection request was made

#### Scenario: Word forms and spellings
- **WHEN** the user asks for "Kuendigung" or "Abstellplatz"
- **THEN** sections about "Kündigung" or "Abstellplätze" are found

#### Scenario: Nothing in common
- **WHEN** no section shares a single word with the question, for example because the documents use another language
- **THEN** the model picks sections from the table of contents (id, file, pages, title, size, beginning of the text); a reply that cannot be read falls back to all sections that fit

#### Scenario: Small library
- **WHEN** all active sections together are smaller than the threshold
- **THEN** they are all sent and neither a search nor a selection request is made

#### Scenario: Disabled plugins stay private
- **WHEN** `Kochen` is switched off for the chat
- **THEN** none of its text is searched or sent

### Requirement: Ping-pong (Nachdenken)
With "Nachdenken" on (the default), the model MAY answer right away or use tools to search again, open the table of contents and read sections from it, for up to two further rounds. The round after that SHALL answer. Text written before a tool call is not part of the answer. With "Nachdenken" off the model is called exactly once after the local search. Every round that adds sections SHALL appear live in the conversation and be stored with the answer.

#### Scenario: The first search is enough
- **WHEN** the sections from the local search answer the question
- **THEN** the model answers in its first call

#### Scenario: Model asks for more
- **WHEN** the model searches for "Bruttogeschossfläche" after reading the first sections
- **THEN** the conversation shows "Runde 1: …" and "Runde 2: …", and the answer uses both

#### Scenario: Model without tool support
- **WHEN** the server rejects a request that offers tools
- **THEN** the question is answered from the local search without tools

#### Scenario: Nachdenken off
- **WHEN** Nachdenken is switched off
- **THEN** no tools are offered

### Requirement: Context window budget
The system SHALL estimate the token size of the active documents and SHALL NOT call the model when it exceeds the context window configured in the profile. The user SHALL see the estimated size and the limit.

#### Scenario: Library too large
- **WHEN** the active documents need about 700k tokens and the context window is 524k
- **THEN** no request is sent and the message explains the sizes and how to switch plugins off

### Requirement: Leaving a running chat
A question SHALL be saved and listed in the sidebar as soon as it is sent. Leaving the chat SHALL NOT stop the answer, and returning SHALL show the question with the answer so far. Renaming or moving the chat meanwhile SHALL be kept; deleting it SHALL stop the answer.

#### Scenario: Visit the library while an answer is running
- **WHEN** the user asks a question, opens the library and returns to the chat before the answer is complete
- **THEN** the question is still there and the answer continues

#### Scenario: Two chats
- **WHEN** one chat is answering and the user opens another
- **THEN** Send and Stop act on the chat that is shown

#### Scenario: Delete while an answer is running
- **WHEN** the user deletes the chat before the answer is complete
- **THEN** it does not reappear

### Requirement: Visible context
While sections are being chosen the conversation SHALL show it live, and each answer SHALL show which sections were sent, out of how many, and their estimated token count.

#### Scenario: Context summary
- **WHEN** an answer is produced from 3 of 42 sections
- **THEN** the conversation shows "3 von 42 Abschnitten gesendet (≈ 5k Tokens)" with the section list

### Requirement: Streaming answer with citations
The answer SHALL stream into the conversation. Citations in the form `[Document.pdf, p. 4]` SHALL be resolved to the document and to the lines on that page that best match the sentence before the citation.

#### Scenario: Citation lines
- **WHEN** the answer says "Die Kündigungsfrist beträgt drei Monate [Mietrecht.pdf, p. 2]."
- **THEN** the citation links to `Recht/Mietrecht.pdf`, page 2, at the line stating the three-month notice period
