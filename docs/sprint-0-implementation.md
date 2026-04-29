# Sprint 0 Implementation Notes

Data wykonania: 2026-04-29

## Co zostało zrobione

### 1. Utworzono solution i strukturę projektów

Powstało klasyczne solution `DesktopAutomationBot.sln` oraz cztery projekty produkcyjne:

- `src/DesktopAutomationBot.Core`
- `src/DesktopAutomationBot.Application`
- `src/DesktopAutomationBot.Infrastructure`
- `src/DesktopAutomationBot.Runner`

Dodano też dwa projekty testowe:

- `tests/DesktopAutomationBot.Core.Tests`
- `tests/DesktopAutomationBot.Application.Tests`

### 2. Ustawiono zależności między projektami

Zależności zostały ustawione zgodnie z założeniem:

- `Runner` referencjonuje `Core`, `Application` i `Infrastructure`
- `Application` referencjonuje `Core`
- `Infrastructure` referencjonuje `Core` i `Application`
- projekty testowe referencjonują odpowiednie projekty produkcyjne

### 3. Ujednolicono standardy repozytorium

Dodano:

- `.editorconfig`
- `.gitattributes`

W plikach projektów włączono:

- `Nullable`
- `ImplicitUsings`
- `LangVersion` ustawione na `latest`
- target `net8.0`

### 4. Przygotowano bazę domenową scenariusza

W `Core` dodano minimalny model:

- `StepType`
- `ScenarioDefinition`
- `ScenarioStep`
- `ScenarioValidationResult`
- `ScenarioValidationException`
- `ScenarioDefinitionValidator`

Walidator sprawdza na tym etapie:

- czy scenariusz istnieje
- czy ma nazwę
- czy ma przynajmniej jeden krok
- czy `OpenUrl` ma `url`
- czy `Click`, `FillText`, `PasteText` i `ReadText` mają `selector`
- czy `If` i `Loop` mają kroki potomne

### 5. Przygotowano warstwę Application

Dodano:

- `IScenarioValidationService`
- `ScenarioValidationService`

To daje punkt wejścia dla przyszłej orkiestracji logiki aplikacyjnej.

### 6. Podmieniono szablonowy Runner

`Program.cs` został zastąpiony prostym uruchomieniem walidacji przykładowego scenariusza. Dzięki temu Runner już teraz odwołuje się do warstw aplikacyjnych i domenowych.

### 7. Dodano testy

Zamiast pustych testów szablonu dodano:

- test walidatora w `Core.Tests`
- test usługi walidacji w `Application.Tests`

Testy sprawdzają:

- brak kroków w scenariuszu
- brak `url` w `OpenUrl`
- poprawny scenariusz przechodzi walidację

## Weryfikacja

Po zmianach uruchomiono:

- `dotnet build DesktopAutomationBot.sln`
- `dotnet test DesktopAutomationBot.sln`

## Uwagi

- `.gitignore` już zawierał kluczowe wpisy dla artefaktów runtime i lokalnej konfiguracji, więc nie było potrzeby znaczącej przebudowy tego pliku.
- Solution utworzono w klasycznym formacie `.sln`, żeby zachować kompatybilność z typowymi narzędziami repozytoryjnymi.
