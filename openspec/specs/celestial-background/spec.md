# celestial-background Specification

## Purpose
Make the workspace subtly alive without ever competing with content.

## Requirements

### Requirement: Non-intrusive layer
The sky SHALL be drawn with SkiaSharp behind the main stage, SHALL NOT receive input (`IsHitTestVisible=false`) and SHALL respect the system "animation effects" setting.

#### Scenario: Animations disabled
- **WHEN** Windows animation effects are off
- **THEN** stars do not blink and no shooting stars appear

### Requirement: Time arc
Positions SHALL follow the local time: day 06:00–18:00, evening 18:00–22:00, night 22:00–06:00. Sun and moon travel along the same arc, highest at the middle of their span.

#### Scenario: Sun at noon
- **WHEN** the local time is 12:00 in light mode
- **THEN** a soft sun halo is drawn at the top of the arc

### Requirement: Night sky (dark mode)
In dark mode the system SHALL draw dim, low-poly sparkle stars that blink slowly and lightly, and a muted moon between 18:00 and 06:00.

#### Scenario: No moon during the day
- **WHEN** the time is 15:00 in dark mode
- **THEN** stars are visible and the moon is not

### Requirement: Shooting stars
In dark mode a shooting star SHALL appear at random intervals of 25–60 seconds and travel towards the last known mouse pointer position before fading out.

#### Scenario: Direction follows the pointer
- **WHEN** the pointer was last seen at the bottom-left corner
- **THEN** the next shooting star moves towards the bottom-left
