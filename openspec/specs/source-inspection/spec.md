# source-inspection Specification

## Purpose
Let users verify every statement against the original document.

## Requirements

### Requirement: Clickable citations
Citations in answers SHALL be rendered as links.

#### Scenario: Opening a citation
- **WHEN** the user clicks `[Mietrecht.pdf, p. 2]`
- **THEN** the in-app document viewer opens the document at page 2

### Requirement: Highlighted cited lines
The viewer SHALL highlight the cited lines in yellow (`#FFF59D`) in light mode and translucent amber in dark mode. PDF pages are rendered as images with highlights placed at the extracted line coordinates; DOCX documents show their paragraphs with the cited ones highlighted.

#### Scenario: PDF highlight position
- **WHEN** lines 2–3 of page 2 are cited
- **THEN** exactly those lines are highlighted on the rendered page and scrolled into view

### Requirement: Open original
The viewer SHALL offer "Open Original File", which opens the document in the user's default application.

#### Scenario: Original opens externally
- **WHEN** the user clicks "Originaldatei öffnen"
- **THEN** the file opens with its associated application
