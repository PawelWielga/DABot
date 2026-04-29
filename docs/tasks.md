# Lista zadan implementacyjnych DABot

Dokument powstal na podstawie `prd.md` i opisuje praktyczny backlog potrzebny do zbudowania MVP aplikacji Desktop Automation Bot w .NET 8 / C#. Zakladam, ze wersja 1.0 powstaje najpierw jako aplikacja uruchamiana z konsoli (`Runner`) dzialajaca na Linuxie, a panel UI zostaje przygotowany dopiero po stabilnym silniku scenariuszy.

## Zalozenia wykonawcze

- Glowny cel MVP: uruchomienie scenariusza JSON, wykonanie akcji w Chromium przez Playwright, komunikacja z lokalnym REST API, zapis logow i artefaktow bledow.
- Stack: .NET 8, C#, Playwright for .NET, HttpClient, Serilog, Polly, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Options.
- Glowny target runtime: Linux, najlepiej Ubuntu/Debian compatible, uruchamiany jako CLI/worker.
- Windows ma byc wspierany jako lokalny target developerski i testowy, o ile nie wymaga to Windows-only API.
- Browser domyslnie startuje w trybie headless. Tryb headed na Linuxie wymaga aktywnego X11/Wayland albo Xvfb, a na Windows aktywnej sesji graficznej.
- Architektura: `Core`, `Application`, `Infrastructure`, `Runner`, opcjonalnie cross-platform `UI`.
- Storage lokalny: `config.json`, `scenarios/`, `logs/`, `screenshots/`, `artifacts/html/`.
- Sekrety nie moga byc trzymane w kodzie ani commitowane w jawnej postaci.
- Wszystkie sciezki plikow budowac przez `Path.Combine`/`Path.Join`, nie przez reczne laczenie separatorem `\` albo `/`.

## Sprint 0 - Fundament repozytorium

### 1. Utworzenie solution i projektow

**Cel:** przygotowac fizyczna strukture aplikacji zgodna z PRD.

**Uzyc:** .NET 8 SDK na Linuxie i Windows, `dotnet CLI`, C# class libraries, console app.

**Jak zrobic:**

1. Utworzyc solution:
   ```bash
   dotnet new sln -n DesktopAutomationBot
   ```
2. Utworzyc katalogi i projekty:
   ```bash
   mkdir src
   dotnet new classlib -n DesktopAutomationBot.Core -o src/DesktopAutomationBot.Core
   dotnet new classlib -n DesktopAutomationBot.Application -o src/DesktopAutomationBot.Application
   dotnet new classlib -n DesktopAutomationBot.Infrastructure -o src/DesktopAutomationBot.Infrastructure
   dotnet new console -n DesktopAutomationBot.Runner -o src/DesktopAutomationBot.Runner
   ```
3. Dodac projekty do solution:
   ```bash
   dotnet sln add src/DesktopAutomationBot.Core/DesktopAutomationBot.Core.csproj
   dotnet sln add src/DesktopAutomationBot.Application/DesktopAutomationBot.Application.csproj
   dotnet sln add src/DesktopAutomationBot.Infrastructure/DesktopAutomationBot.Infrastructure.csproj
   dotnet sln add src/DesktopAutomationBot.Runner/DesktopAutomationBot.Runner.csproj
   ```
4. Ustawic referencje:
   ```bash
   dotnet add src/DesktopAutomationBot.Application/DesktopAutomationBot.Application.csproj reference src/DesktopAutomationBot.Core/DesktopAutomationBot.Core.csproj
   dotnet add src/DesktopAutomationBot.Infrastructure/DesktopAutomationBot.Infrastructure.csproj reference src/DesktopAutomationBot.Core/DesktopAutomationBot.Core.csproj
   dotnet add src/DesktopAutomationBot.Infrastructure/DesktopAutomationBot.Infrastructure.csproj reference src/DesktopAutomationBot.Application/DesktopAutomationBot.Application.csproj
   dotnet add src/DesktopAutomationBot.Runner/DesktopAutomationBot.Runner.csproj reference src/DesktopAutomationBot.Core/DesktopAutomationBot.Core.csproj
   dotnet add src/DesktopAutomationBot.Runner/DesktopAutomationBot.Runner.csproj reference src/DesktopAutomationBot.Application/DesktopAutomationBot.Application.csproj
   dotnet add src/DesktopAutomationBot.Runner/DesktopAutomationBot.Runner.csproj reference src/DesktopAutomationBot.Infrastructure/DesktopAutomationBot.Infrastructure.csproj
   ```

**Kryteria ukonczenia:** `dotnet build` przechodzi na Linuxie i Windows, solution ma cztery projekty, zaleznosci ida w jednym kierunku: Runner -> Application/Infrastructure -> Core.

### 2. Dodanie standardow repozytorium

**Cel:** ujednolicic styl kodu i uniknac przypadkowego commitowania logow, sekretow i artefaktow.

**Uzyc:** `.gitignore`, `.gitattributes`, `.editorconfig`, nullable reference types, implicit usings.

**Jak zrobic:**

1. Dodac `.gitignore` dla .NET z wpisami:
   ```gitignore
   bin/
   obj/
   logs/
   screenshots/
   artifacts/
   config.local.json
   *.user
   ```
2. Dodac `.gitattributes`, zeby wymusic przewidywalne konce linii dla kodu i dokumentacji:
   ```gitattributes
   * text=auto
   *.cs text eol=lf
   *.csproj text eol=lf
   *.sln text eol=crlf
   *.md text eol=lf
   *.json text eol=lf
   *.sh text eol=lf
   *.ps1 text eol=crlf
   ```
3. Dodac `.editorconfig` z reguly formatowania C#.
4. W kazdym `.csproj` wlaczyc:
   ```xml
   <Nullable>enable</Nullable>
   <ImplicitUsings>enable</ImplicitUsings>
   <LangVersion>latest</LangVersion>
   ```
5. Ustalic konwencje nazewnictwa:
   - modele domenowe w `Core`
   - orkiestracja przypadkow uzycia w `Application`
   - Playwright, HTTP, storage, logowanie w `Infrastructure`
   - argumenty CLI i start procesu w `Runner`
6. Unikac w kodzie zalozen o literach dyskow, backslashach i lokalizacjach typu `C:\...`.

**Kryteria ukonczenia:** repo nie sledzi plikow runtime, projekt buduje sie z wlaczonym nullable, a struktura nazw jest jednoznaczna.

### 3. Przygotowanie testow automatycznych

**Cel:** od poczatku miec miejsce na testy logiki scenariuszy, walidacji i klienta API.

**Uzyc:** xUnit, FluentAssertions, Microsoft.NET.Test.Sdk, coverlet.collector.

**Jak zrobic:**

1. Utworzyc katalog `tests/`.
2. Dodac projekty testowe:
   ```bash
   mkdir tests
   dotnet new xunit -n DesktopAutomationBot.Core.Tests -o tests/DesktopAutomationBot.Core.Tests
   dotnet new xunit -n DesktopAutomationBot.Application.Tests -o tests/DesktopAutomationBot.Application.Tests
   ```
3. Dodac referencje do projektow produkcyjnych.
4. Dodac paczki:
   ```bash
   dotnet add tests/DesktopAutomationBot.Core.Tests package FluentAssertions
   dotnet add tests/DesktopAutomationBot.Application.Tests package FluentAssertions
   ```
5. Dodac testy startowe, np. czy pusty scenariusz jest niepoprawny.

**Kryteria ukonczenia:** `dotnet test` dziala i ma przynajmniej jeden test dla modelu scenariusza.

## Sprint 1 - Silnik przegladarki i podstawowe kroki

### 4. Instalacja Playwright for .NET

**Cel:** dac aplikacji mozliwosc uruchamiania Chromium/Chrome i sterowania strona.

**Uzyc:** `Microsoft.Playwright`, Playwright CLI, Chromium.

**Jak zrobic:**

1. Dodac paczke do `Infrastructure`:
   ```bash
   dotnet add src/DesktopAutomationBot.Infrastructure package Microsoft.Playwright
   ```
2. Po pierwszym buildzie zainstalowac Chromium razem z zaleznosciami systemowymi dla Linuxa:
   ```bash
   dotnet build
   pwsh src/DesktopAutomationBot.Infrastructure/bin/Debug/net8.0/playwright.ps1 install --with-deps chromium
   ```
3. Na Linuxie upewnic sie, ze `pwsh` jest dostepne, bo Playwright .NET generuje skrypt `playwright.ps1`.
4. W CI lub kontenerze uruchamiac instalacje zaleznosci jako uzytkownik z uprawnieniami do instalacji pakietow systemowych.
5. Utworzyc interfejs w `Application`, np. `IBrowserAutomation`.
6. Implementacje Playwright trzymac w `Infrastructure`, np. `PlaywrightBrowserAutomation`.
7. Ustawienia `headless`, `slowMo`, viewport i timeout pobierac z konfiguracji.
8. Domyslnie ustawic `headless = true`; `headless = false` dopuszczac tylko dla lokalnego debugowania z X11/Wayland/Xvfb.

**Kryteria ukonczenia:** Runner potrafi na Linuxie otworzyc Chromium w trybie headless, wejsc na URL i zamknac przegladarke bez bledu.

### 5. Model domenowy scenariusza

**Cel:** opisac w kodzie JSON scenariusza z PRD.

**Uzyc:** C# records/classes, `System.Text.Json`, enum `StepType`.

**Jak zrobic:**

1. W `Core` utworzyc modele:
   - `ScenarioDefinition`
   - `ScenarioStep`
   - `StepType`
   - `ScenarioVariableBag`
   - `ScenarioExecutionResult`
   - `StepExecutionResult`
2. Pola minimalne dla kroku:
   - `Type`
   - `Selector`
   - `Url`
   - `Value`
   - `Output`
   - `TimeoutMs`
   - `RetryCount`
   - `Children` dla `If` i `Loop`
3. Dla elastycznosci dodac `Dictionary<string, JsonElement>? Parameters`, ale tylko jako rozszerzenie, nie jako glowny kontrakt.
4. Dodac walidacje wymaganych pol per typ kroku.

**Kryteria ukonczenia:** da sie zdeserializowac przyklad z PRD, a brak `url` przy `OpenUrl` lub brak `selector` przy `Click` zwraca czytelny blad walidacji.

### 6. Loader scenariuszy JSON

**Cel:** ladowac scenariusze z katalogu `scenarios/`.

**Uzyc:** `System.Text.Json`, `IFileSystem` albo zwykle `File`, `IOptions<BotOptions>`.

**Jak zrobic:**

1. W `Application` zdefiniowac `IScenarioLoader`.
2. W `Infrastructure` dodac `JsonScenarioLoader`.
3. Loader powinien przyjmowac nazwe pliku lub pelna sciezke.
4. Ustawic `JsonSerializerOptions`:
   - `PropertyNameCaseInsensitive = true`
   - konwerter enumow z tekstu, np. `JsonStringEnumConverter`
5. Po wczytaniu uruchomic walidator scenariusza.
6. Zwracac blad z nazwa pliku i numerem/sciezka kroku, np. `steps[2].selector`.

**Kryteria ukonczenia:** Runner uruchomiony z `--scenario scenarios/sample.json` wczytuje scenariusz i wypisuje jego nazwe.

### 7. Konfiguracja aplikacji

**Cel:** miec jedno miejsce na ustawienia API, browsera, logowania i sciezek.

**Uzyc:** `Microsoft.Extensions.Configuration.Json`, `Microsoft.Extensions.Options`, `config.json`, `config.local.json`.

**Jak zrobic:**

1. Dodac paczki do `Runner`:
   ```bash
   dotnet add src/DesktopAutomationBot.Runner package Microsoft.Extensions.Configuration
   dotnet add src/DesktopAutomationBot.Runner package Microsoft.Extensions.Configuration.Json
   dotnet add src/DesktopAutomationBot.Runner package Microsoft.Extensions.Options.ConfigurationExtensions
   ```
2. Utworzyc klasy opcji:
   - `BotOptions`
   - `BrowserOptions`
   - `ApiOptions`
   - `StorageOptions`
   - `RetryOptions`
3. Dodac `config.json` z domyslnymi wartosciami bez sekretow.
4. Dodac obsluge `config.local.json` jako pliku opcjonalnego poza gitem.
5. W przyszlosci pozwolic nadpisywac ustawienia zmiennymi srodowiskowymi.

**Kryteria ukonczenia:** aplikacja startuje z ustawieniami z `config.json`, a lokalne override dziala bez zmiany kodu.

### 8. Dependency Injection i composition root

**Cel:** polaczyc warstwy bez recznego tworzenia klas w wielu miejscach.

**Uzyc:** `Microsoft.Extensions.DependencyInjection`, extension methods `AddApplication`, `AddInfrastructure`.

**Jak zrobic:**

1. Dodac paczke:
   ```bash
   dotnet add src/DesktopAutomationBot.Runner package Microsoft.Extensions.DependencyInjection
   ```
2. W `Application` dodac `ServiceCollectionExtensions.AddApplication()`.
3. W `Infrastructure` dodac `ServiceCollectionExtensions.AddInfrastructure()`.
4. Rejestrowac interfejsy:
   - `IScenarioLoader`
   - `IScenarioExecutor`
   - `IBrowserAutomation`
   - `IApiClient`
   - `IArtifactStore`
   - `IClock`
5. W `Runner` zbudowac `ServiceProvider` tylko raz na starcie.

**Kryteria ukonczenia:** `Program.cs` zawiera tylko konfiguracje, DI, parsowanie argumentow i wywolanie glownego use case.

### 9. Implementacja kroku `OpenUrl`

**Cel:** uruchomic pierwszy realny krok automatyzacji.

**Uzyc:** Playwright `IBrowser`, `IPage`, `GotoAsync`.

**Jak zrobic:**

1. W `Application` dodac handler kroku, np. `IStepHandler`.
2. Utworzyc `OpenUrlStepHandler`.
3. Handler pobiera `step.Url`, podstawia zmienne, a potem wywoluje `page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle })`.
4. Timeout pobrac z kroku albo ustawien globalnych.
5. Wynik zapisac jako `StepExecutionResult`.

**Kryteria ukonczenia:** scenariusz z samym `OpenUrl` otwiera strone i konczy sie statusem `Success`.

### 10. Implementacja kroku `Click`

**Cel:** klikac elementy po selektorze CSS lub Playwright locatorze.

**Uzyc:** Playwright `Locator`, `ClickAsync`, timeouty.

**Jak zrobic:**

1. Dodac `ClickStepHandler`.
2. Wymagac `selector`.
3. Uzyc `page.Locator(selector).ClickAsync(new LocatorClickOptions { Timeout = timeout })`.
4. Przed kliknieciem opcjonalnie czekac az element bedzie widoczny.
5. W logu zapisac selektor i czas wykonania.

**Kryteria ukonczenia:** bot umie kliknac przycisk na testowej stronie HTML.

### 11. Implementacja krokow `FillText` i `PasteText`

**Cel:** wpisywac lub wklejac tekst do formularzy.

**Uzyc:** Playwright `FillAsync`, `PressAsync`, clipboard API tam, gdzie potrzebne.

**Jak zrobic:**

1. Dodac `FillTextStepHandler`.
2. Dla `FillText` uzyc `locator.FillAsync(value)`.
3. Dla `PasteText` preferowac Playwright `Keyboard.InsertTextAsync` albo kontrolowane ustawienie clipboardu w kontekscie przegladarki.
4. Nie polegac na systemowym schowku hosta, bo na Linuxie w trybie headless moze nie byc dostepnej sesji graficznej.
5. Jezeli scenariusz wymaga prawdziwego skrotu wklejania, uzyc `Control+V` na Linux/Windows i testowac to osobno w headed/Xvfb.
6. W obu przypadkach obslugiwac podstawianie zmiennych `{{name}}`.
7. Dla hasel nie logowac wartosci jawnie. W logu zapisac np. `***`.

**Kryteria ukonczenia:** bot wypelnia input tekstowy wartoscia ze scenariusza lub zmiennej.

### 12. Implementacja kroku `ReadText`

**Cel:** odczytac tekst ze strony i zapisac go jako zmienna wynikowa.

**Uzyc:** Playwright `InnerTextAsync` lub `TextContentAsync`.

**Jak zrobic:**

1. Dodac `ReadTextStepHandler`.
2. Wymagac `selector` i `output`.
3. Odczytac tekst z elementu.
4. Zapisac wartosc do `ScenarioVariableBag` pod kluczem `output`.
5. W logu pokazac nazwe outputu, a wartosc tylko jezeli nie jest oznaczona jako sekret.

**Kryteria ukonczenia:** po kroku `ReadText` kolejny krok moze uzyc `{{result}}`.

### 13. Implementacja kroku `WaitFor`

**Cel:** czekac na element, tekst, URL lub stan strony.

**Uzyc:** Playwright `WaitForSelectorAsync`, `WaitForURLAsync`, `WaitForLoadStateAsync`.

**Jak zrobic:**

1. Dodac warianty `WaitFor` przez parametr `mode`, np. `selector`, `url`, `loadState`, `text`.
2. Dla `selector` czekac na widocznosc elementu.
3. Dla `url` czekac na dopasowanie wzorca.
4. Dla `loadState` obslugiwac `domcontentloaded`, `load`, `networkidle`.
5. Timeout domyslny brac z konfiguracji.

**Kryteria ukonczenia:** bot stabilnie czeka na dynamicznie pojawiajacy sie element.

### 14. Implementacja kroku `Screenshot`

**Cel:** robic screenshot strony na zadanie oraz przy bledzie.

**Uzyc:** Playwright `ScreenshotAsync`, lokalny `screenshots/`.

**Jak zrobic:**

1. Dodac `ScreenshotStepHandler`.
2. Generowac nazwy plikow z timestampem, nazwa scenariusza i indeksem kroku.
3. Zapisywac pelna strone przez `FullPage = true`.
4. Sciezke do screenshotu dodac do `StepExecutionResult`.
5. Przy bledzie wykorzystywac ten sam mechanizm z typem artefaktu `error`.

**Kryteria ukonczenia:** screenshot jest zapisany w `screenshots/`, a jego sciezka trafia do raportu wykonania.

## Sprint 2 - API, logowanie i artefakty

### 15. Konfiguracja Serilog

**Cel:** miec czytelne logi startu, krokow, bledow, czasu wykonania i odpowiedzi API.

**Uzyc:** Serilog, `Serilog.Sinks.Console`, `Serilog.Sinks.File`, structured logging.

**Jak zrobic:**

1. Dodac paczki:
   ```bash
   dotnet add src/DesktopAutomationBot.Runner package Serilog.Extensions.Hosting
   dotnet add src/DesktopAutomationBot.Runner package Serilog.Sinks.Console
   dotnet add src/DesktopAutomationBot.Runner package Serilog.Sinks.File
   ```
2. Skonfigurowac logowanie do konsoli i pliku `logs/dabot-.log`.
3. Dodac `CorrelationId` dla pojedynczego uruchomienia scenariusza.
4. Logowac eventy:
   - start procesu
   - start/koniec kroku
   - blad kroku
   - retry
   - odpowiedz API bez wrazliwych danych
   - wynik koncowy
5. Ustawic rolling logs dziennie.

**Kryteria ukonczenia:** po uruchomieniu scenariusza w `logs/` jest plik z pelna historia wykonania.

### 16. Klient REST API

**Cel:** obslugiwac GET danych wejsciowych, POST wyniku, PUT statusu i autoryzacje bearer tokenem.

**Uzyc:** `HttpClientFactory`, `System.Net.Http.Json`, `ApiOptions`.

**Jak zrobic:**

1. Dodac paczke:
   ```bash
   dotnet add src/DesktopAutomationBot.Infrastructure package Microsoft.Extensions.Http
   ```
2. W `Application` zdefiniowac `IApiClient`.
3. W `Infrastructure` utworzyc `RestApiClient`.
4. Uzyc `IHttpClientFactory` i klienta nazwanego, np. `DABotApi`.
5. Base URL, token, timeout i endpointy pobierac z `config.json`.
6. Dla tokena ustawic naglowek:
   ```csharp
   request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
   ```
7. Odpowiedzi deserializowac jako JSON.

**Kryteria ukonczenia:** bot wykonuje GET/POST/PUT do lokalnego API i poprawnie obsluguje statusy HTTP.

### 17. Retry i timeout dla API

**Cel:** zwiekszyc stabilnosc komunikacji z lokalnym API.

**Uzyc:** Polly, `AddPolicyHandler`, exponential backoff.

**Jak zrobic:**

1. Dodac paczki:
   ```bash
   dotnet add src/DesktopAutomationBot.Infrastructure package Polly
   dotnet add src/DesktopAutomationBot.Infrastructure package Microsoft.Extensions.Http.Polly
   ```
2. Zdefiniowac polityke retry dla:
   - timeoutow
   - HTTP 408
   - HTTP 429
   - HTTP 5xx
3. Ustawic np. 3 proby z opoznieniami 1s, 2s, 4s.
4. Kazdy retry logowac z numerem proby.
5. Nie retry'owac bledow 400/401/403, bo wymagaja poprawy danych lub tokena.

**Kryteria ukonczenia:** przy chwilowym bledzie API bot ponawia request i konczy sukcesem, jezeli API wroci.

### 18. Implementacja kroku `CallApi`

**Cel:** pozwolic scenariuszowi wywolac API w dowolnym miejscu flow.

**Uzyc:** `IApiClient`, JSON payload, zmienne scenariusza.

**Jak zrobic:**

1. Dodac `CallApiStepHandler`.
2. Obslugiwac parametry:
   - `method`: GET/POST/PUT
   - `endpoint`
   - `body`
   - `output`
3. Przed wyslaniem podstawic zmienne w endpointzie i body.
4. Odpowiedz zapisac do zmiennej wskazanej przez `output`.
5. Dla POST/PUT wynikow scenariusza przygotowac osobne metody wysokiego poziomu, np. `PostResultAsync`, ale `CallApi` zostawic jako uniwersalny krok.

**Kryteria ukonczenia:** scenariusz moze pobrac dane z API i uzyc ich pozniej w `FillText`.

### 19. Raport koncowy wykonania

**Cel:** po kazdym uruchomieniu miec obiekt z wynikiem, czasem, krokami, bledami i artefaktami.

**Uzyc:** `ScenarioExecutionResult`, JSON serialization, opcjonalny POST do API.

**Jak zrobic:**

1. W `Application` rozbudowac `ScenarioExecutionResult`.
2. Pola raportu:
   - scenarioName
   - startedAt
   - finishedAt
   - durationMs
   - status
   - steps
   - variables outputowe
   - artifacts
   - errorMessage
3. Zapisac raport lokalnie do `artifacts/reports/`.
4. Jezeli konfiguracja ma endpoint wynikow, wyslac raport przez POST.

**Kryteria ukonczenia:** kazde wykonanie konczy sie raportem JSON lokalnie i opcjonalnie w API.

### 20. Zapisywanie HTML przy bledzie

**Cel:** ulatwic diagnoze sytuacji, kiedy selektor nie zostal znaleziony albo strona byla w innym stanie.

**Uzyc:** Playwright `ContentAsync`, lokalny katalog `artifacts/html/`.

**Jak zrobic:**

1. Dodac `IArtifactStore` w `Application`.
2. Implementacje `FileArtifactStore` trzymac w `Infrastructure`.
3. Przy wyjatku kroku pobrac `page.ContentAsync()`.
4. Zapisac HTML z nazwa powiazana ze scenariuszem i krokiem.
5. Sciezke dodac do raportu i logow.

**Kryteria ukonczenia:** blad kroku generuje screenshot oraz HTML w artefaktach.

### 21. Centralna obsluga bledow kroku

**Cel:** jeden sposob reagowania na bledy: log, screenshot, HTML, retry, status koncowy.

**Uzyc:** pipeline wykonania kroku w `ScenarioExecutor`, custom exceptions.

**Jak zrobic:**

1. Utworzyc typy bledow:
   - `ScenarioValidationException`
   - `StepExecutionException`
   - `ApiCallException`
   - `BrowserAutomationException`
2. W `ScenarioExecutor` opakowac wykonanie kroku w try/catch.
3. Przy bledzie zebrac artefakty diagnostyczne.
4. Zdecydowac, czy krok ma retry wedlug `RetryCount` albo konfiguracji globalnej.
5. Po wyczerpaniu prob ustawic status scenariusza na `Failed`.

**Kryteria ukonczenia:** blad jednego kroku daje kontrolowany raport, a aplikacja nie konczy sie nieobsluzonym wyjatkiem.

## Sprint 3 - Scenariusze, zmienne i sterowanie flow

### 22. Orkiestrator `ScenarioExecutor`

**Cel:** wykonywac kroki scenariusza w kolejnosci i zbierac wyniki.

**Uzyc:** wzorzec handlerow krokow, `IStepHandler`, DI, cancellation token.

**Jak zrobic:**

1. W `Application` utworzyc `IScenarioExecutor`.
2. Implementacja powinna:
   - utworzyc kontekst wykonania
   - wystartowac browser
   - przejsc po krokach
   - wywolac odpowiedni handler dla `StepType`
   - zapisac wynik kazdego kroku
   - zamknac browser w `finally`
3. Handler wybierac przez slownik `StepType -> IStepHandler`.
4. Wszedzie przekazywac `CancellationToken`.

**Kryteria ukonczenia:** pojedynczy scenariusz JSON wykonuje kilka roznych krokow w kolejnosci.

### 23. Mechanizm zmiennych `{{name}}`

**Cel:** pozwolic scenariuszom dzialac dynamicznie na danych z API i wynikach strony.

**Uzyc:** prosty resolver zmiennych, regex, `ScenarioVariableBag`.

**Jak zrobic:**

1. W `Application` dodac `IVariableResolver`.
2. Obslugiwac skladnie `{{variableName}}`.
3. Dane poczatkowe ladowac z:
   - API input
   - config
   - argumentow CLI
   - wynikow `ReadText` i `CallApi`
4. Dla brakujacej zmiennej zwracac czytelny blad z nazwa pola i kroku.
5. Dla JSON body rozwazyc resolver dzialajacy na stringu po serializacji albo na `JsonNode`.

**Kryteria ukonczenia:** wartosc pobrana z API moze byc wstawiona do URL, inputa i body requestu.

### 24. Implementacja kroku `Delay`

**Cel:** dodac jawna pauze tam, gdzie aplikacja webowa wymaga odczekania.

**Uzyc:** `Task.Delay`, `CancellationToken`.

**Jak zrobic:**

1. Dodac `DelayStepHandler`.
2. Wymagac parametru `milliseconds` albo `timeoutMs`.
3. Walidowac maksymalna wartosc, np. nie wiecej niz 5 minut.
4. Uzyc `Task.Delay(delay, cancellationToken)`.
5. Logowac czas pauzy.

**Kryteria ukonczenia:** scenariusz moze zatrzymac wykonanie na okreslona liczbe milisekund.

### 25. Implementacja kroku `If`

**Cel:** wykonywac rozne kroki w zaleznosci od danych ze strony, API lub zmiennych.

**Uzyc:** prosty evaluator warunkow, np. operatory `equals`, `contains`, `exists`, `notEmpty`.

**Jak zrobic:**

1. Rozszerzyc model kroku o:
   - `condition`
   - `thenSteps`
   - `elseSteps`
2. Dodac `ConditionEvaluator`.
3. Na start obslugiwac bezpieczne operatory:
   - `equals`
   - `notEquals`
   - `contains`
   - `exists`
   - `isEmpty`
4. Nie uruchamiac dowolnego kodu ani skryptow z JSON.
5. `IfStepHandler` powinien wywolac `ScenarioExecutor` dla zagniezdzonych krokow albo wspolna metode `ExecuteStepsAsync`.

**Kryteria ukonczenia:** scenariusz wykonuje `thenSteps` lub `elseSteps` na podstawie zmiennej.

### 26. Implementacja kroku `Loop`

**Cel:** powtarzac zestaw krokow dla listy danych lub dopoki spelniony jest warunek.

**Uzyc:** model `Loop`, `ScenarioVariableBag`, limit iteracji.

**Jak zrobic:**

1. Dodac tryby petli:
   - `forEach` po tablicy danych
   - `repeat` stala liczba razy
2. Wymagac `maxIterations`, nawet jezeli warunek wyglada bezpiecznie.
3. W kazdej iteracji ustawic zmienne specjalne:
   - `{{loop.index}}`
   - `{{loop.item}}`
4. Zagniezdzone kroki wykonywac przez wspolny mechanizm executorowy.
5. Przy bledzie zdecydowac, czy przerywac cala petle, czy tylko iteracje. W MVP domyslnie przerywac cala petle.

**Kryteria ukonczenia:** scenariusz potrafi wypelnic formularz dla kazdego elementu listy z API.

### 27. Retry dla krokow browsera

**Cel:** zmniejszyc liczbe falszywych bledow spowodowanych opoznieniem strony.

**Uzyc:** Polly albo wlasny retry pipeline w `ScenarioExecutor`.

**Jak zrobic:**

1. Dla kazdego kroku odczytywac `retryCount`.
2. Jezeli brak wartosci, uzyc globalnego ustawienia.
3. Retry stosowac dla bledow technicznych:
   - timeout selektora
   - element chwilowo niewidoczny
   - transient navigation failure
4. Nie retry'owac bledow walidacji scenariusza.
5. Kazda probe logowac i mierzyc.

**Kryteria ukonczenia:** krok `Click` moze zostac ponowiony po chwilowym timeoutcie i zakonczyc sie sukcesem.

### 28. Obsluga wielu kart i sesji uzytkownika

**Cel:** przygotowac silnik pod scenariusze wymagajace nowej karty, zachowania zalogowanej sesji oraz recznego logowania w trybie headed.

**Uzyc:** Playwright `IBrowserContext`, `IPage`, persistent context opcjonalnie.

**Jak zrobic:**

1. W `BrowserSession` trzymac:
   - browser
   - context
   - aktywna page
   - slownik nazwanych kart
2. Dodac ustawienie `userDataDir` dla sesji persistent.
3. Domyslna lokalizacje profilu na Linuxie trzymac pod katalogiem aplikacji albo `~/.local/share/dabot/profiles/{profileName}`.
4. Sciezke `userDataDir` budowac przez `Path.Combine` i pozwolic nadpisac ja w konfiguracji.
5. Dodac nazwe profilu w konfiguracji lub parametrze uruchomienia, zeby mozna bylo rozdzielic np. `default`, `google`, `prod`.
6. Dla pierwszego logowania uruchamiac przegladarke w trybie headed, pozwolic uzytkownikowi zalogowac sie recznie, a potem ponownie uruchamiac bot z tym samym profilem.
7. W scenariuszu przewidziec opcjonalne `pageName`.
8. Na MVP zaimplementowac minimum: jedna aktywna karta plus mozliwosc utworzenia nowej, przeĂ„Ä…Ă˘â‚¬ĹˇÄ‚â€žĂ˘â‚¬Â¦czania sie miedzy kartami oraz ich zamykania.
9. Przy zamknieciu zawsze sprzatac browser/context i zamykac nieuzywane karty.

**Kryteria ukonczenia:** bot moze na Linuxie zachowac sesje uzytkownika miedzy uruchomieniami, jezeli wlaczono `userDataDir`, a reczne logowanie w headed dziala jako sposob inicjalnego zbudowania profilu.

## Sprint 4 - Runner, UX developerski i UI opcjonalne

### 29. Argumenty CLI Runnera

**Cel:** wygodnie uruchamiac bota z terminala, skryptow i harmonogramu.

**Uzyc:** `System.CommandLine` albo proste parsowanie argumentow w MVP.

**Jak zrobic:**

1. Dodac obsluge argumentow:
   - `--scenario <path>`
   - `--config <path>`
   - `--headless true|false`
   - `--input <json>`
   - `--dry-run`
2. Dla braku scenariusza wypisac czytelny komunikat i kod wyjscia `2`.
3. Dla bledu wykonania zwrocic kod wyjscia `1`.
4. Dla sukcesu zwrocic kod `0`.
5. W `--dry-run` tylko wczytac i zwalidowac scenariusz bez startu browsera.
6. Dla Linuxa dodac przyklady uruchomienia w bashu i przewidziec uzycie w `cron`, `systemd timer` albo CI.

**Kryteria ukonczenia:** aplikacje da sie uruchomic na Linuxie komenda `dotnet run --project src/DesktopAutomationBot.Runner -- --scenario scenarios/sample.json`.

### 30. Przykladowe scenariusze

**Cel:** dac developerom gotowe pliki do testow i rozbudowy.

**Uzyc:** katalog `scenarios/`, JSON.

**Jak zrobic:**

1. Dodac `scenarios/sample-open-url.json`.
2. Dodac `scenarios/sample-form.json` dla `OpenUrl`, `FillText`, `Click`, `ReadText`.
3. Dodac `scenarios/sample-api.json` dla `CallApi` i zmiennych.
4. W komentarzach dokumentacyjnych albo README opisac, co robi kazdy plik.
5. Nie trzymac prawdziwych tokenow ani prywatnych URL-i.

**Kryteria ukonczenia:** nowy developer moze uruchomic sample bez znajomosci calego kodu.

### 31. Lokalna strona testowa dla Playwright

**Cel:** testowac kroki browsera bez zaleznosci od zewnetrznych serwisow.

**Uzyc:** prosty plik HTML albo minimalny ASP.NET Core test server.

**Jak zrobic:**

1. Dodac `tests/TestPages/form.html` albo testowy serwer w testach integracyjnych.
2. Strona powinna miec:
   - input `#title`
   - button `#save`
   - element `.result`
3. Po kliknieciu przycisku JS przepisuje wartosc inputa do `.result`.
4. Uzyc tej strony w testach `FillText`, `Click`, `ReadText`.
5. Test browsera uruchamiac headless, zeby dzialal na Linux CI bez serwera graficznego.

**Kryteria ukonczenia:** test integracyjny na Linuxie potwierdza, ze bot wypelnia formularz i odczytuje wynik.

### 32. Testy jednostkowe walidacji scenariusza

**Cel:** szybko wykrywac bledy w JSON przed startem browsera.

**Uzyc:** xUnit, FluentAssertions.

**Jak zrobic:**

1. Pokryc testami kazdy typ kroku MVP.
2. Testowac brak wymaganych pol.
3. Testowac nieznany typ kroku.
4. Testowac zagniezdzone `If` i `Loop`.
5. Testowac poprawne komunikaty bledow.

**Kryteria ukonczenia:** walidator ma testy dla wszystkich typow krokow z PRD.

### 33. Testy integracyjne API

**Cel:** sprawdzic klienta REST bez prawdziwego API biznesowego.

**Uzyc:** `WireMock.Net` albo `Microsoft.AspNetCore.TestHost`.

**Jak zrobic:**

1. Dodac projekt `DesktopAutomationBot.Infrastructure.Tests`.
2. Uruchomic fake API w testach.
3. Przetestowac:
   - GET input
   - POST result
   - PUT status
   - bearer token w naglowku
   - retry po 500
4. Nie zalezec od internetu.

**Kryteria ukonczenia:** `dotnet test` sprawdza najwazniejsze sciezki komunikacji API lokalnie.

### 34. Szyfrowanie sekretow

**Cel:** spelnic wymaganie braku hasel w kodzie i bezpiecznego trzymania tokenow.

**Uzyc:** zmienne srodowiskowe, plik lokalny z ograniczonymi uprawnieniami, opcjonalnie `Microsoft.AspNetCore.DataProtection` z cross-platform key ring; dla dev takze user-secrets.

**Jak zrobic:**

1. Na MVP nie wpisywac tokenow do `config.json`.
2. Wspierac token z:
   - zmiennej srodowiskowej
   - `config.local.json`
   - zaszyfrowanego pliku lokalnego
3. Dodac usluge `ISecretProvider`.
4. Na Linuxie dla plikow z sekretami wymagac uprawnien tylko dla wlasciciela, np. `0600`.
5. Jezeli uzywany jest `Microsoft.AspNetCore.DataProtection`, skonfigurowac katalog kluczy poza repo, np. `~/.local/share/dabot/keys`.
6. DPAPI moze byc opcjonalnym providerem Windows, ale nie moze byc jedynym mechanizmem sekretow.
7. W logach zawsze maskowac sekrety.

**Kryteria ukonczenia:** token API jest pobierany na Linuxie bez wpisywania go w kodzie, a logi nigdy go nie ujawniaja.

### 35. Dokumentacja uruchomienia

**Cel:** umozliwic instalacje i pierwsze uruchomienie bez wiedzy autora projektu.

**Uzyc:** `README.md`, przyklady komend, opis konfiguracji.

**Jak zrobic:**

1. Opisac wymagania:
   - .NET 8 SDK
   - Linux Ubuntu/Debian compatible jako docelowy runtime
   - PowerShell `pwsh` potrzebny do skryptu Playwright .NET
   - Playwright browsers
   - zaleznosci systemowe Chromium instalowane przez Playwright
2. Opisac instalacje:
   ```bash
   dotnet restore
   dotnet build
   pwsh src/DesktopAutomationBot.Infrastructure/bin/Debug/net8.0/playwright.ps1 install --with-deps chromium
   ```
3. Opisac wariant bez instalacji zaleznosci systemowych, jezeli obraz/kontener juz je ma:
   ```bash
   pwsh src/DesktopAutomationBot.Infrastructure/bin/Debug/net8.0/playwright.ps1 install chromium
   ```
4. Opisac `config.json`.
5. Opisac konfiguracje przez zmienne srodowiskowe w bashu.
6. Opisac uruchomienie sample scenario.
7. Opisac lokalizacje logow, screenshotow i raportow.
8. Opisac uruchomienie headless oraz debugowanie headed przez Xvfb.

**Kryteria ukonczenia:** osoba z czystym repo potrafi uruchomic sample na Linuxie na podstawie README.

### 36. Minimalny panel UI po MVP

**Cel:** przygotowac opcjonalny panel do wyboru scenariusza i podgladu statusu.

**Uzyc:** Avalonia. Nie uzywac WPF dla glownego UI, bo aplikacja ma dzialac na Linuxie.

**Jak zrobic:**

1. Utworzyc projekt UI dopiero po stabilizacji Runnera.
2. Funkcje pierwszej wersji UI:
   - lista scenariuszy z katalogu
   - przycisk start/stop
   - headless on/off
   - podglad logow
   - ostatni screenshot bledu
   - status wykonania
3. UI powinno uzywac tych samych serwisow `Application` i `Infrastructure`, bez duplikowania logiki.
4. Dlugie wykonanie uruchamiac w tle z `CancellationToken`.

**Kryteria ukonczenia:** uzytkownik moze na Linuxie wybrac scenariusz z UI, uruchomic go i zobaczyc wynik. UI pozostaje opcjonalne; MVP moze byc w pelni CLI/worker.

## Zadania przekrojowe

### 37. Utrzymanie modularnej architektury

**Cel:** zachowac rozszerzalnosc pod przyszle scenariusze, OCR, kolejki, AI decision engine i ewentualne aplikacje desktopowe jako osobne moduly zalezne od OS.

**Uzyc:** interfejsy w `Application`, implementacje w `Infrastructure`, modele w `Core`.

**Jak zrobic:**

1. Nie referencjonowac Playwright z `Core` ani modeli domenowych.
2. Nie wkladac HTTP ani filesystemu do `Core`.
3. Nowy typ kroku dodawac jako osobny handler.
4. Kazdy handler powinien byc maly i testowalny.
5. Nie uzalezniac scenariusza JSON od klas Playwright.
6. Nie wprowadzac obowiazkowych zaleznosci Windows-only do `Application` ani `Infrastructure`.
7. Platform-specific implementacje, jezeli beda potrzebne, ukrywac za interfejsem i rejestrowac warunkowo po wykryciu OS.

**Kryteria ukonczenia:** dodanie nowego kroku wymaga glownie nowego handlera i rejestracji w DI.

### 38. Standard statusow i kodow bledow

**Cel:** raporty z bota maja byc zrozumiale dla API i czlowieka.

**Uzyc:** enumy `ExecutionStatus`, `StepStatus`, kody bledow.

**Jak zrobic:**

1. Zdefiniowac statusy scenariusza:
   - `Success`
   - `Failed`
   - `Cancelled`
   - `ValidationFailed`
2. Zdefiniowac statusy kroku:
   - `Pending`
   - `Running`
   - `Success`
   - `Failed`
   - `Skipped`
   - `Retried`
3. Dodac kody bledow, np.:
   - `SCENARIO_INVALID`
   - `SELECTOR_TIMEOUT`
   - `API_UNAUTHORIZED`
   - `API_TIMEOUT`
   - `BROWSER_START_FAILED`
4. Uzywac tych kodow w logach, raportach i PUT status.

**Kryteria ukonczenia:** API moze jednoznacznie rozpoznac powod porazki bota.

### 39. Wydajnosc startu ponizej 5 sekund

**Cel:** spelnic wymaganie niefunkcjonalne z PRD.

**Uzyc:** pomiar czasu startu, lazy initialization, ograniczenie pracy przed startem scenariusza.

**Jak zrobic:**

1. Mierzyc czas od wejscia do `Program.Main` do startu pierwszego kroku.
2. Nie inicjalizowac browsera przed walidacja scenariusza.
3. Nie skanowac duzych katalogow przy starcie.
4. Playwright uruchamiac dopiero przed wykonaniem krokow browsera.
5. Dodac log `startupDurationMs`.

**Kryteria ukonczenia:** typowy start z sample scenario miesci sie ponizej 5 sekund przed rozpoczeciem wykonania.

### 40. Przygotowanie pod harmonogram i kolejke w v2

**Cel:** nie blokowac przyszlej rozbudowy o scheduler i wiele botow rownolegle.

**Uzyc:** izolowany `ScenarioRunRequest`, bezstanowe serwisy tam, gdzie to mozliwe.

**Jak zrobic:**

1. `ScenarioExecutor` powinien przyjmowac obiekt requestu, nie czytac globalnego stanu.
2. Wszystkie dane uruchomienia trzymac w `ScenarioExecutionContext`.
3. Artefakty zapisywac w katalogu per run, np. `artifacts/runs/{runId}/`.
4. Nie trzymac aktywnej sesji browsera jako singleton.
5. Przygotowac `runId` jako identyfikator mozliwy do przekazania z API.

**Kryteria ukonczenia:** da sie teoretycznie uruchomic dwa scenariusze w osobnych kontekstach bez konfliktu katalogow i zmiennych.

### 41. Walidacja runtime i deployment`r`n`r`n**Cel:** potwierdzic, ze MVP naprawde dziala na Linuxie jako runtime wdrozeniowy, a na Windows jako wspierane srodowisko developerskie/testowe.`r`n`r`n**Uzyc:** Ubuntu/Debian, Windows, bash albo PowerShell, `dotnet publish`, Playwright headless, opcjonalnie Docker albo GitHub Actions.`r`n`r`n**Jak zrobic:**`r`n`r`n1. Przygotowac instrukcje uruchomienia na czystym Linuxie:`r`n   - instalacja .NET 8 SDK albo runtime`r`n   - instalacja `pwsh`, jezeli nie ma go w systemie`r`n   - `dotnet restore``r`n   - `dotnet build``r`n   - `pwsh .../playwright.ps1 install --with-deps chromium``r`n2. Dodac smoke test CLI:`r`n   ```bash`r`n   dotnet run --project src/DesktopAutomationBot.Runner -- --scenario scenarios/sample-open-url.json --headless true`r`n   ````r`n3. Dodac smoke test po publikacji:`r`n   ```bash`r`n   dotnet publish src/DesktopAutomationBot.Runner -c Release -o ./publish/dabot`r`n   ./publish/dabot/DesktopAutomationBot.Runner --scenario scenarios/sample-open-url.json --headless true`r`n   ````r`n4. Sprawdzic, ze logi, screenshoty i raporty zapisuja sie do katalogow wzglednych albo skonfigurowanych sciezek bez problemow z uprawnieniami.`r`n5. Zweryfikowac, ze aplikacja nie wymaga aktywnej sesji graficznej dla domyslnego trybu headless.`r`n6. Opcjonalnie przygotowac plik `Dockerfile` albo workflow CI, ktory uruchamia `dotnet test` i smoke test na Linuxie oraz podstawowy smoke test na Windows.`r`n`r`n**Kryteria ukonczenia:** czysty Linux potrafi zbudowac, zainstalowac zaleznosci Playwright, uruchomic sample scenario headless i zapisac artefakty bez recznych poprawek, a Windows uruchamia sie lokalnie do testow i debugowania bez zmian architektury.
## Proponowana kolejnosc realizacji MVP

1. Zadania 1-3: solution, standardy, testy.
2. Zadania 4-14: Playwright i podstawowe kroki browsera.
3. Zadania 15-21: logowanie, API, retry, artefakty bledow.
4. Zadania 22-28: executor, zmienne, `If`, `Loop`, retry krokow.
5. Zadania 29-35: runner, sample, testy, sekrety, dokumentacja.
6. Zadanie 36: UI jako etap po stabilnym MVP konsolowym.
7. Zadania 37-41: trzymac jako zasady stale podczas calej implementacji, z walidacja Linux runtime przed uznaniem MVP za gotowe.

## Minimalna definicja MVP

Aplikacja moze byc uznana za MVP, gdy:

- uruchamia sie przez `DesktopAutomationBot.Runner`,
- dziala na Linuxie w trybie headless bez aktywnej sesji graficznej,
- wczytuje `config.json` i scenariusz JSON,
- otwiera Chromium przez Playwright,
- wykonuje `OpenUrl`, `Click`, `FillText`, `PasteText`, `WaitFor`, `ReadText`, `Screenshot`, `CallApi`, `Delay`,
- obsluguje zmienne `{{name}}`,
- komunikuje sie z lokalnym API przez GET/POST/PUT z bearer tokenem,
- loguje przebieg do konsoli i pliku,
- przy bledzie zapisuje screenshot, HTML i raport JSON,
- zwraca poprawny kod wyjscia procesu,
- ma testy walidacji scenariusza i minimum jeden test integracyjny browsera,
- ma wykonany smoke test na Linuxie po `dotnet publish`.
