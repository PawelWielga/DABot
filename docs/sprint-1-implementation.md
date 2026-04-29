# Sprint 1 Implementation Notes

Data wykonania: 2026-04-29

## Zakres

Zrealizowalem Sprint 1 z `docs/tasks.md`, czyli fundament silnika przegladarki i podstawowe kroki scenariusza.

## Co zostalo dodane

### 1. Integracja Playwright

W `src/DesktopAutomationBot.Infrastructure` dodalem zaleznosc `Microsoft.Playwright` i przygotowalem implementacje `PlaywrightBrowserAutomation`.

Ta klasa:

- startuje Chromium przez Playwright,
- dziala w trybie headless z ustawien konfiguracji,
- otwiera jedna aktywna karte,
- wykonuje nawigacje, klikniecia, wpisywanie i odczyt tekstu,
- obsluguje czekanie na elementy, tekst, URL i stan ladowania,
- robi screenshoty do pliku,
- zamyka browser, context i page w `DisposeAsync`.

### 2. Warstwa abstrakcji w Application

W `src/DesktopAutomationBot.Application` dodalem interfejs `IBrowserAutomation`, zeby logika aplikacyjna nie zalezy od typow Playwright.

Dodalem tez:

- `IScenarioLoader`
- `IScenarioExecutor`
- `IStepHandler`
- `ScenarioExecutionContext`
- `ScenarioLoadException`
- `BotOptions`, `BrowserOptions`, `StorageOptions`

### 3. Executor scenariusza

Dodalem `ScenarioExecutor`, ktory:

- waliduje scenariusz przed startem,
- uruchamia browser,
- przechodzi po krokach w kolejnosci,
- wybiera handler po `StepType`,
- zapisuje wynik kazdego kroku,
- zamyka browser w `finally`.

### 4. Handlery krokow

Dodalem osobne handlery dla krokow:

- `OpenUrl`
- `Click`
- `FillText`
- `PasteText`
- `ReadText`
- `WaitFor`
- `Screenshot`

`ReadText` zapisuje wynik do `ScenarioVariableBag`, a `Screenshot` tworzy plik PNG w katalogu per uruchomienie.

### 5. Loader JSON

Dodalem `JsonScenarioLoader`, ktory:

- przyjmuje sciezke pelna albo wzgledna,
- potrafi uzyc katalogu `scenarios/`,
- deserializuje JSON z case-insensitive property names,
- czyta enumy jako tekst,
- waliduje scenariusz po wczytaniu,
- zwraca czytelny blad z lista problemow.

### 6. Konfiguracja

Dodalem `config.json` z domyslnymi ustawieniami browsera, sciezek i scenariusza.

### 7. Runner

`src/DesktopAutomationBot.Runner/Program.cs` zostal zamieniony z kodu testowego w prawdziwy composition root:

- laduje `config.json`,
- rejestruje uslugi przez DI,
- pobiera scenariusz,
- uruchamia executor,
- wypisuje podsumowanie w konsoli,
- zwraca odpowiedni kod procesu.

### 8. Testy

Rozszerzylem testy walidacji o przypadek `ReadText` bez `output`.

### 9. Przykladowy scenariusz

Dodalem `scenarios/sample-open-url.json`, ktory:

- otwiera `https://example.com`,
- czeka na `h1`,
- czyta tekst naglowka,
- robi screenshot.

## Uwagi techniczne

- Sciezki sa skladane przez `Path.Combine`.
- Domyslny tryb browsera jest headless.
- Architektura nadal trzyma browser w Infrastructure, a orkiestracje w Application.

## Weryfikacja

Po zmianach nalezy wykonac:

- `dotnet build DesktopAutomationBot.sln`
- `dotnet test DesktopAutomationBot.sln`

Jesli browsers Playwright nie sa jeszcze zainstalowane w srodowisku, trzeba dodatkowo uruchomic instalacje Playwright dla Chromium.
