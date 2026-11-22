# profile-settings Specification

## Purpose
Keep personal configuration local and protected, separated into who the user is (Profile) and how the app behaves (Settings).

## Requirements

### Requirement: Protected local storage
Profile and settings SHALL be stored locally and encrypted with Windows DPAPI for the current user. `IDEFIX_DATA_DIR` MAY override the storage folder for development.

#### Scenario: API key is not stored in plain text
- **WHEN** the profile with an API key is saved
- **THEN** the key cannot be found in the stored file

### Requirement: Profile tab
The Profile tab SHALL contain first name, root folder (directory picker), LLM provider, endpoint, API key, chat model, context window (tokens) and pre-prompt.

#### Scenario: Greeting uses the first name
- **WHEN** the first name is "Anna"
- **THEN** the empty chat greets with "Hallo Anna, was schauen wir uns heute an."

### Requirement: Model discovery
The system SHALL list available models from the provider's OpenAI-compatible `/models` endpoint, including local Ollama models.

#### Scenario: Ollama models are offered
- **WHEN** the user clicks "Modelle suchen" with an Ollama endpoint
- **THEN** the installed models appear in the model pickers

### Requirement: API key is mandatory
Every provider, Ollama included, SHALL require a non-empty API key before chats can run.

#### Scenario: Missing key keeps setup mode
- **WHEN** no API key is entered
- **THEN** the app shows the setup hint and does not start indexing

### Requirement: Settings tab
The Settings tab SHALL contain theme (System, Light, Dark), language (System, English, Deutsch (Schweiz)) and the nightly-build opt-in. Theme and language apply immediately.

#### Scenario: Language switch
- **WHEN** the user selects "Deutsch (Schweiz)" and saves
- **THEN** the interface switches to Swiss German without a restart

### Requirement: Nightly opt-in
Users SHALL be able to opt in to nightly pre-releases; otherwise only stable releases are offered.

#### Scenario: Opted-in user
- **WHEN** nightly builds are enabled
- **THEN** the updater also considers GitHub pre-releases
