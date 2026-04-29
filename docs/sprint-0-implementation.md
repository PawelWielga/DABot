# Sprint 0 Implementation Notes

Data wykonania: 2026-04-29

## Co zostaĹ‚o zrobione

### 1. Utworzono solution i strukturÄ™ projektĂłw

PowstaĹ‚o klasyczne solution `DesktopAutomationBot.sln` oraz cztery projekty produkcyjne:

- `src/DesktopAutomationBot.Core`
- `src/DesktopAutomationBot.Application`
- `src/DesktopAutomationBot.Infrastructure`
- `src/DesktopAutomationBot.Runner`

Dodano teĹĽ dwa projekty testowe:

- `tests/DesktopAutomationBot.Core.Tests`
- `tests/DesktopAutomationBot.Application.Tests`

### 2. Ustawiono zaleĹĽnoĹ›ci miÄ™dzy projektami

ZaleĹĽnoĹ›ci zostaĹ‚y ustawione zgodnie z zaĹ‚oĹĽeniem:

- `Runner` referencjonuje `Core`, `Application` i `Infrastructure`
- `Application` referencjonuje `Core`
- `Infrastructure` referencjonuje `Core` i `Application`
- projekty testowe referencjonujÄ… odpowiednie projekty produkcyjne

### 3. Ujednolicono standardy repozytorium

Dodano:

- `.editorconfig`
- `.gitattributes`

W plikach projektĂłw wĹ‚Ä…czono:

- `Nullable`
- `ImplicitUsings`
- `LangVersion` ustawione na `latest`
- target `net8.0`

### 4. Przygotowano bazÄ™ domenowÄ… scenariusza

W `Core` dodano minimalny model:

- `StepType`
- `ScenarioDefinition`
- `ScenarioStep`
- `ScenarioValidationResult`
- `ScenarioValidationException`
- `ScenarioDefinitionValidator`

Walidator sprawdza na tym etapie:

- czy scenariusz istnieje
- czy ma nazwÄ™
- czy ma przynajmniej jeden krok
- czy `OpenUrl` ma `url`
- czy `Click`, `FillText`, `PasteText` i `ReadText` majÄ… `selector`
- czy `If` i `Loop` majÄ… kroki potomne

### 5. Przygotowano warstwÄ™ Application

Dodano:

- `IScenarioValidationService`
- `ScenarioValidationService`

To daje punkt wejĹ›cia dla przyszĹ‚ej orkiestracji logiki aplikacyjnej.

### 6. Podmieniono szablonowy Runner

`Program.cs` zostaĹ‚ zastÄ…piony prostym uruchomieniem walidacji przykĹ‚adowego scenariusza. DziÄ™ki temu Runner juĹĽ teraz odwoĹ‚uje siÄ™ do warstw aplikacyjnych i domenowych.

### 7. Dodano testy

Zamiast pustych testĂłw szablonu dodano:

- test walidatora w `Core.Tests`

Obecne zalozenia runtime sa nadal Linux-first dla wdrozen, ale repo ma pozostawac uruchamialne i testowalne lokalnie na Windows, pod warunkiem zachowania zgodnosci cross-platform.
- test usĹ‚ugi walidacji w `Application.Tests`

Testy sprawdzajÄ…:

- brak krokĂłw w scenariuszu
- brak `url` w `OpenUrl`
- poprawny scenariusz przechodzi walidacjÄ™

## Weryfikacja

Po zmianach uruchomiono:

- `dotnet build DesktopAutomationBot.sln`
- `dotnet test DesktopAutomationBot.sln`

## Uwagi

- `.gitignore` juĹĽ zawieraĹ‚ kluczowe wpisy dla artefaktĂłw runtime i lokalnej konfiguracji, wiÄ™c nie byĹ‚o potrzeby znaczÄ…cej przebudowy tego pliku.
- Solution utworzono w klasycznym formacie `.sln`, ĹĽeby zachowaÄ‡ kompatybilnoĹ›Ä‡ z typowymi narzÄ™dziami repozytoryjnymi.
