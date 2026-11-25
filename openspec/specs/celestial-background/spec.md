# celestial-background Specification

## Purpose
Make the workspace subtly alive without ever competing with content.

## Requirements

### Requirement: Non-intrusive layer
The sky SHALL be drawn with SkiaSharp behind the main stage, SHALL NOT receive input (`IsHitTestVisible=false`) and SHALL respect the system "animation effects" setting.

#### Scenario: Animations disabled
- **WHEN** Windows animation effects are off
- **THEN** stars do not blink and no shooting stars appear

### Requirement: Theme follows daylight
The app SHALL use the light theme between local sunrise and sunset and the dark theme otherwise; there is no manual theme setting. Sunrise and sunset are calculated for the user's time zone (Zurich for Central European zones, otherwise latitude 47° at the zone's base offset) and converted to local time, so summer and winter time shift them automatically. The theme is re-evaluated every minute.

#### Scenario: Winter evening
- **WHEN** it is 17:00 on 21 December in Zurich
- **THEN** the app is in dark mode

#### Scenario: Summer evening
- **WHEN** it is 17:00 on 21 June in Zurich
- **THEN** the app is in light mode

### Requirement: Time arc
The sun SHALL travel along an arc from sunrise to sunset and the moon from sunset to the next sunrise, each highest at the middle of its span.

#### Scenario: Sun at solar noon
- **WHEN** the local time is halfway between sunrise and sunset
- **THEN** a soft sun halo is drawn at the top of the arc

### Requirement: Night sky (dark mode)
In dark mode the system SHALL draw dim, low-poly sparkle stars that blink slowly and lightly, and a muted moon.

### Requirement: Shooting stars
In dark mode a shooting star SHALL appear at random intervals of 25–60 seconds and travel towards the last known mouse pointer position before fading out.

#### Scenario: Direction follows the pointer
- **WHEN** the pointer was last seen at the bottom-left corner
- **THEN** the next shooting star moves towards the bottom-left
