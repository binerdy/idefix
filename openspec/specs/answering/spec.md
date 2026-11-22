# answering Specification

## Purpose
Answer questions from the relevant Markdown sections of the user's active documents, chosen by the model from a table of contents, with verifiable citations.

## Requirements

### Requirement: Section selection from a table of contents
When the active sections together exceed a small threshold (8k tokens), the system SHALL first send the model a table of contents (id, file, pages, title, size, beginning of the text) and ask for the ids of the sections it needs, then send only those sections. Small libraries are sent completely without this step. No embeddings are used. A reply that cannot be read falls back to all sections that fit.

#### Scenario: Only chosen sections are sent
- **WHEN** the model picks `S2` out of 42 sections
- **THEN** the answer request contains only section `S2`, with its page markers `--- [FileName.pdf, p. N] ---`

#### Scenario: Small library
- **WHEN** all active sections together are smaller than the threshold
- **THEN** they are all sent and no selection request is made

#### Scenario: Disabled plugins stay private
- **WHEN** `Kochen` is switched off for the chat
- **THEN** none of its text is sent

### Requirement: Ping-pong (Nachdenken)
With "Nachdenken" on (the default), the system SHALL allow up to three selection rounds. From round two on, the model sees the sections it already has plus the table of contents and either requests more section ids or replies that it has enough. With "Nachdenken" off there is exactly one selection round. Every round SHALL appear live in the conversation and be stored with the answer.

#### Scenario: Model asks for more
- **WHEN** the model picks `S2` in round 1 and, after reading it, asks for `S7`
- **THEN** the conversation shows "Runde 1: …" and "Runde 2: …", and the answer uses both sections

#### Scenario: Nachdenken off
- **WHEN** Nachdenken is switched off
- **THEN** only one selection request is made before answering

### Requirement: Context window budget
The system SHALL estimate the token size of the active documents and SHALL NOT call the model when it exceeds the context window configured in the profile. The user SHALL see the estimated size and the limit.

#### Scenario: Library too large
- **WHEN** the active documents need about 700k tokens and the context window is 524k
- **THEN** no request is sent and the message explains the sizes and how to switch plugins off

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
