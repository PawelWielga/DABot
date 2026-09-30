# PRD - Desktop Automation Bot (DABot)

## 1. Cel produktu

DABot jest uniwersalnym silnikiem automatyzacji przegladarki i dlugotrwalych procesow.

System ma wykonywac deklaratywne scenariusze, sterowac przegladarka przez Playwright, komunikowac sie z zewnetrznymi systemami oraz potrafic bezpiecznie zatrzymac wykonanie i wznowic je po wystapieniu zdarzenia.

DABot nie moze byc projektowany pod jedna strone, jednego dostawce ani jeden konkretny proces biznesowy.

## 2. Glowne zasady

1. **Uniwersalnosc** - mechanizmy domenowe nie moga zalezec od konkretnej strony internetowej.
2. **Deklaratywne scenariusze** - podstawowa automatyzacja jest opisana jako lista krokow.
3. **Krotkie i dlugie oczekiwanie to rozne mechanizmy** - oczekiwanie na element DOM pozostaje blokujace, a oczekiwanie na zewnetrzne zdarzenie moze zawiesic run.
4. **Trwaly stan** - run moze zostac wznowiony po restarcie procesu lub maszyny.
5. **Panel webowy jest opcjonalnym klientem** - CLI i worker musza dzialac bez panelu.
6. **Linux-first** - glownym runtime pozostaje Linux; Windows jest wspieranym srodowiskiem developerskim i testowym.
7. **Brak zaleznosci od jednego transportu** - GitHub, HTTP, kolejka czy lokalny event sa adapterami infrastrukturalnymi.
8. **Brak sekretow w repozytorium i logach**.
9. **Jedna warstwa wykonawcza** - CLI, HTTP, panel webowy, MCP i agenci korzystaja z tych samych use case'ow; MCP nie tworzy osobnego silnika wykonawczego.
10. **AI sklada bezpieczne prymitywy** - dynamiczne narzedzia sa deklaratywnymi workflow z dozwolonych Actions, a nie nieograniczonym kodem wykonywanym automatycznie.

## 3. Problem do rozwiazania

Wiele procesow wymaga wykonywania powtarzalnych czynnosci w przegladarce, szczegolnie gdy:

- system nie ma odpowiedniego API,
- proces wymaga zalogowanej sesji,
- wynik pojawia sie dopiero po pewnym czasie,
- czesc procesu jest wykonywana w zewnetrznym systemie,
- trzeba wznowic prace po zmianie statusu, odpowiedzi, akceptacji lub webhooku,
- potrzebna jest diagnostyka i mozliwosc odtworzenia przebiegu.

DABot ma umozliwic automatyzacje takich procesow bez utrzymywania jednego dlugo dzialajacego wywolania dla calego workflow.

## 4. Zakres funkcjonalny

### 4.1 Scenariusze

Scenariusz jest deklaratywna definicja krokow.

Podstawowe typy krokow:

- `OpenUrl`
- `Click`
- `FillText`
- `PasteText`
- `ReadText`
- `WaitFor`
- `Screenshot`
- `CallApi`
- `Delay`
- `If`
- `Loop`
- `Suspend`
- opcjonalnie w przyszlosci: `EmitEvent`, `SetVariable`, `ExecuteScenario`

Scenariusz nie powinien zawierac logiki specyficznej dla konkretnego serwisu, jezeli ta logika moze zostac opisana standardowymi krokami.

### 4.2 Model wykonania

DABot wspiera dwa zachowania w ramach tego samego silnika.

#### Wykonanie ciagle

Proces uruchamia scenariusz i wykonuje wszystkie kroki do zakonczenia:

```text
Start -> Step -> Step -> Step -> Completed
```

#### Wykonanie trwale

Scenariusz moze zapisac stan i zakonczyc proces bez oznaczania runu jako zakonczonego:

```text
Start -> Step -> Suspend
                    |
               state persisted
                    |
                process exits
                    |
             external event
                    |
                  Resume
                    |
                 Step...
```

Po wznowieniu run kontynuuje wykonanie od zapisanego miejsca.

### 4.3 WaitFor i Suspend

`WaitFor` sluzy do krotkich oczekiwan wewnatrz aktywnej sesji przegladarki, np.:

- element staje sie widoczny,
- zmienia sie URL,
- pojawia sie tekst,
- dokument osiaga okreslony stan ladowania.

`Suspend` sluzy do dlugich lub zewnetrznych oczekiwan, np.:

- zewnetrzny proces zakonczyl przetwarzanie,
- operator zatwierdzil operacje,
- pojawil sie webhook,
- zmienil sie status,
- obserwator strony wykryl oczekiwana zmiane.

`Suspend` zapisuje stan runu i pozwala zakonczyc aktualny proces.

### 4.4 Run

Scenariusz opisuje co wykonac. Run opisuje konkretny przebieg wykonania.

Minimalne dane runu:

- `RunId`
- identyfikator scenariusza lub jego wersji
- status
- aktualny krok
- zmienne
- dane korelacyjne
- oczekiwane zdarzenie, jezeli run jest zawieszony
- czas utworzenia i ostatniej aktualizacji
- licznik prob i bledow
- opcjonalne ograniczenia, np. maksymalna liczba krokow

Podstawowe statusy:

- `Queued`
- `Running`
- `Suspended`
- `WaitingForEvent`
- `Completed`
- `Failed`
- `Cancelled`
- `WaitingForHuman`

### 4.5 Zdarzenia

DABot posiada neutralny model zdarzenia.

Przykladowe pola:

- `EventId`
- `Type`
- `RunId`
- `CorrelationId`
- `CreatedAt`
- `Data`

Zdarzenie moze:

- wznowic zawieszony run,
- zmienic stan runu,
- dostarczyc nowe dane do zmiennych,
- uruchomic nowy scenariusz,
- zostac zapisane jako element historii.

System musi byc odporny na ponowne dostarczenie tego samego zdarzenia. Wymagana jest idempotencja.

### 4.6 Obserwatory stron

DABot moze obserwowac strone niezaleznie od glownego wykonania scenariusza.

Obserwator jest konfigurowalny i powinien wspierac co najmniej:

- wykrycie pojawienia sie selektora,
- wykrycie znikniecia selektora,
- porownanie tekstu,
- wykrycie zmiany tekstu,
- wykrycie zmiany URL,
- wykrycie zmiany wybranego fragmentu DOM.

Po spelnieniu warunku obserwator publikuje zwykle zdarzenie automatyzacji.

Obserwator nie moze zawierac specjalnej wiedzy o konkretnej stronie w warstwie Core.

### 4.7 Integracja z API

DABot wspiera:

- GET
- POST
- PUT
- konfigurowalny timeout
- retry
- bearer token lub inny rozszerzalny mechanizm autoryzacji
- mapowanie odpowiedzi do zmiennych scenariusza

### 4.8 Sesje przegladarki

System wspiera:

#### Sesje efemeryczne

- tworzone na czas jednego wykonania,
- zamykane po zakonczeniu,
- bez zalozenia zachowania logowania.

#### Sesje trwale

- przechowuja profil przegladarki,
- zachowuja cookies i local storage,
- moga byc nazwane, np. `default`, `portal-prod`, `test`,
- moga zostac otwarte w trybie headed do recznego logowania lub diagnostyki,
- musza byc chronione przed jednoczesnym uzyciem tego samego profilu przez konfliktujace procesy.

### 4.9 Artefakty i diagnostyka

Dla kazdego runu system powinien umiec zapisac:

- log wykonania,
- wynik kazdego kroku,
- screenshot,
- snapshot HTML,
- blad i stack trace,
- czasy wykonania,
- istotne zdarzenia,
- opcjonalne dane diagnostyczne przegladarki.

Artefakty powinny byc grupowane per `RunId`.

### 4.10 Actions, Tools i MCP

DABot rozroznia dwa poziomy rozszerzalnosci:

- **Action** - niski poziom, implementowany i testowany w kodzie, np. otwarcie URL, klikniecie, HTTP request, odczyt/zapis pliku, warunek, opoznienie.
- **Tool/Workflow** - deklaratywna, wersjonowana kompozycja Actions z nazwanymi inputami i outputami.

Tool moze zostac utworzony recznie, przez panel lub przez agenta AI. Utworzenie przez AI nie daje prawa do wykonywania dowolnego C#/JavaScript/shell. Definicja przechodzi walidacje, kontrole uprawnien i test przed aktywacja zgodnie z polityka srodowiska.

DABot docelowo wspiera MCP w obu kierunkach:

- jako **MCP Server** wystawia scenariusze i aktywne Tools z rejestru jako narzedzia dla zewnetrznych agentow,
- jako **MCP Client** moze konsumowac narzedzia udostepnione przez zewnetrzne serwery MCP.

Minimalny lifecycle dynamicznego narzedzia:

    Draft -> Validate -> Test -> Enable -> Version/Disable

Zmiana aktywnej listy narzedzi powinna byc propagowana do klientow MCP zgodnie z mozliwosciami protokolu.

Szczegoly: [MCP and dynamic tools architecture](mcp-and-dynamic-tools.md).

## 5. Panel webowy

DABot posiada opcjonalny panel webowy do konfiguracji i monitorowania.

Panel jest klientem warstwy Application. Nie moze byc wymagany do uruchomienia CLI lub workera.

### 5.1 Dashboard

Dashboard pokazuje:

- liczbe runow,
- statusy runow,
- ostatnie bledy,
- aktywnych workerow,
- oczekujace i zawieszone runy.

### 5.2 Scenariusze

Panel umozliwia:

- liste scenariuszy,
- tworzenie i edycje scenariusza,
- wizualna edycje krokow,
- zmiane kolejnosci krokow,
- wlaczanie i wylaczanie krokow,
- import/export JSON,
- widok JSON dla zaawansowanej edycji,
- walidacje przed zapisem,
- testowe uruchomienie scenariusza.

JSON pozostaje wspieranym formatem wymiany i uruchamiania scenariuszy.

### 5.3 Runs

Widok runu pokazuje:

- status,
- aktualny krok,
- historie krokow,
- zmienne,
- zdarzenia,
- artefakty,
- czas rozpoczecia i ostatniej aktualizacji.

Docelowe akcje operatorskie:

- Resume
- Cancel
- Retry
- Retry from step
- Clone run

### 5.4 Profile przegladarki

Panel pozwala:

- tworzyc i nazywac profile,
- sprawdzac ich stan,
- uruchamiac sesje interaktywna,
- czyscic profil,
- diagnozowac problem z sesja.

### 5.5 Obserwatory

Panel pozwala konfigurowac:

- URL,
- profil przegladarki,
- typ warunku,
- selector,
- oczekiwana wartosc,
- interwal sprawdzania,
- typ emitowanego zdarzenia,
- correlation id.

### 5.6 Zdarzenia

Panel udostepnia historie zdarzen oraz, dla operatora z odpowiednimi uprawnieniami, mozliwosc recznego wyslania zdarzenia w celach administracyjnych i testowych.

### 5.7 Workers / Nodes

Docelowo jeden panel moze zarzadzac wieloma instancjami wykonawczymi DABot uruchomionymi na roznych VM lub maszynach fizycznych.

Panel pokazuje:

- stabilny identyfikator node/workera,
- status i czas ostatniego heartbeat,
- wersje DABot, system operacyjny i dostepne przegladarki,
- capabilities/tags,
- liczbe slotow wykonawczych i aktywne zadania,
- obciazenie,
- ostatni blad,
- aktualne i historyczne runy przypisane do node.

Panel udostepnia tez operacje operatorskie takie jak Drain, Enable i Disable. Wszystkie takie operacje powinny byc audytowalne.

Szczegolowy model: [Distributed DABot deployment](distributed-deployment.md).

## 6. Interfejsy uruchomieniowe

DABot powinien wspierac niezaleznie:

- CLI,
- worker,
- HTTP API,
- panel webowy,
- MCP server,
- MCP client.

CLI pozostaje pelnoprawnym sposobem uruchamiania scenariuszy.

Przykladowo:

```bash
dabot run scenario.json
dabot resume <runId>
dabot cancel <runId>
```

Dokladna skladnia moze ewoluowac.

## 7. Storage

### 7.1 Konfiguracja

Konfiguracja moze pochodzic z:

- `config.json`
- `config.local.json`
- zmiennych srodowiskowych
- panelu webowego dla ustawien przechowywanych w bazie

### 7.2 Dane runtime

Dla pojedynczej, samodzielnej instalacji z panelem preferowanym lokalnym storage jest SQLite.

Dla wdrozenia rozproszonego z wieloma agentami na roznych VM wymagany jest wspoldzielony storage wspierajacy bezpieczna wspolbieznosc i transakcyjne claim/lease. Preferowanym pierwszym providerem jest PostgreSQL. SQLite na wspoldzielonym zasobie sieciowym nie jest wspieranym mechanizmem koordynacji multi-VM.

Minimalne logiczne zbiory danych:

- Scenarios
- ScenarioVersions
- Runs
- RunSteps
- RunVariables
- Events
- BrowserProfiles
- PageObservers
- Workers

Implementacja storage musi byc schowana za interfejsami, aby mozna bylo podmienic SQLite na inne rozwiazanie.

## 8. Integracje i transport

Warstwa domenowa nie moze zalezec od konkretnego transportu.

Wymagane abstrakcje:

- `IRunStore`
- `IScenarioStore`
- `IEventPublisher`
- `IEventConsumer`
- `IBrowserSessionFactory`

Potencjalne implementacje infrastrukturalne:

- SQLite
- JSON/local filesystem
- HTTP/webhook
- GitHub events
- system kolejkowy
- zewnetrzne serwery MCP

MCP jest adapterem integracyjnym. Narzedzia wystawione przez MCP musza delegowac do warstwy Application zamiast omijac walidacje, uprawnienia, run tracking i audit.

## 9. Architektura techniczna

### Stack

- .NET
- C#
- Playwright for .NET
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Configuration
- HttpClient
- SQLite / EF Core dla trwalego runtime
- Blazor dla panelu webowego

### Warstwy

```text
Core
  domain models
  validation
  run/event semantics

Application
  scenario execution
  run coordination
  suspend/resume
  interfaces

Infrastructure
  Playwright
  persistence
  external transports
  logging
  filesystem

Runner
  CLI
  worker host

Web
  dashboard
  configuration
  scenario editor
  run monitoring

Mcp
  MCP server
  MCP client
  tool registry adapter
  protocol mapping
```

## 10. Wymagania niefunkcjonalne

- Linux jest glownym runtime.
- Standardowa automatyzacja musi dzialac headless.
- Tryb headed jest opcjonalny i sluzy m.in. konfiguracji profili i diagnostyce.
- System musi byc odporny na restart procesu pomiedzy `Suspend` i `Resume`.
- Ponowne dostarczenie zdarzenia nie moze powodowac podwojnego wykonania tego samego kroku.
- Aktywne runy musza miec mechanizm blokady lub lease przy pracy wielu workerow.
- Scenariusze musza byc walidowane przed uruchomieniem.
- Sekrety nie moga byc logowane ani commitowane.
- Sciezki plikow musza byc cross-platform.
- Playwright types nie powinny wyciekac do Core.
- Panel webowy nie moze omijac warstwy Application i bezposrednio sterowac Playwrightem.
- Architektura musi pozwalac uruchomic dwa niezalezne runy bez konfliktu zmiennych, katalogow i profili.
- W trybie rozproszonym kilka agentow na roznych maszynach musi moc korzystac z jednego control plane i jednego dashboardu bez ryzyka podwojnego wykonania runu.
- Utrata agenta nie moze powodowac automatycznego powtorzenia niezweryfikowanej operacji ubocznej; odzyskiwanie musi respektowac StepAttempt i retry-safety.

## 11. Model scenariusza

Przyklad zwyklego scenariusza:

```json
{
  "name": "Submit form",
  "steps": [
    { "type": "OpenUrl", "url": "https://example.com/form" },
    { "type": "FillText", "selector": "#title", "value": "{{title}}" },
    { "type": "Click", "selector": "#submit" },
    { "type": "WaitFor", "selector": ".result" },
    { "type": "ReadText", "selector": ".result", "output": "result" }
  ]
}
```

Przyklad trwalego scenariusza:

```json
{
  "name": "Submit and resume later",
  "steps": [
    { "type": "OpenUrl", "url": "{{url}}" },
    { "type": "FillText", "selector": "#input", "value": "{{payload}}" },
    { "type": "Click", "selector": "#submit" },
    {
      "type": "Suspend",
      "parameters": {
        "event": "external.response",
        "correlationId": "{{runId}}"
      }
    },
    { "type": "ReadText", "selector": ".result", "output": "result" }
  ]
}
```

## 12. Bezpieczenstwo

- Hasla i tokeny nie sa przechowywane w scenariuszach.
- Dane wrazliwe musza byc maskowane w logach.
- Profile przegladarki nie sa commitowane.
- Panel webowy wymaga autoryzacji przed udostepnieniem poza zaufana siec.
- Operacje administracyjne, takie jak reczny resume/cancel/event, powinny byc audytowalne.
- Integracje zewnetrzne powinny otrzymywac minimalny wymagany zakres uprawnien.

## 13. Roadmap

### Etap 1 - Fundament i podstawowa automatyzacja

- struktura solution
- walidacja scenariuszy
- Playwright
- podstawowe kroki
- JSON loader
- CLI

### Etap 2 - Stabilny runner

- API
- logging
- retry
- HTML/screenshot artifacts
- variables
- `If`
- `Loop`
- Linux smoke tests
- persistent browser profiles

### Etap 3 - Durable runtime

- `ScenarioRunRequest`
- `AutomationRun`
- `RunStatus`
- `IRunStore`
- `Suspend`
- `Resume`
- external `RunId`
- idempotency
- crash recovery

### Etap 4 - Event model

- `AutomationEvent`
- publisher/consumer abstractions
- correlation
- event history
- resume by event
- retries and dead-letter handling

### Etap 5 - Page observers

- generic page conditions
- polling
- persistent profile support
- event emission
- observer recovery

### Etap 6 - Web panel

- dashboard
- scenarios
- visual scenario editor
- runs and run details
- browser profiles
- configuration

### Etap 7 - Distributed execution and operations

- centralny DABot Server / Control Plane
- agenci DABot na wielu VM/hostach
- node registry i stabilne NodeId
- heartbeats i lifecycle node
- run leases / CAS claiming
- concurrency limits i execution slots
- capability-aware scheduling
- node-local browser profile ownership
- wspolny dashboard Nodes / Workers
- PostgreSQL jako pierwszy wspierany distributed store
- uwierzytelnione polaczenie Agent -> Server
- transfer/referencje artefaktow
- schedules
- secrets management
- audit log
- advanced retry/recovery

### Etap 8 - MCP i dynamiczne narzedzia

- neutralny Action registry
- wersjonowany Tool/Workflow registry
- walidacja input/output schema
- polityki uprawnien i approval
- testowanie przed aktywacja
- MCP Server nad Application
- MCP Client dla zewnetrznych serwerow
- dynamiczne odswiezanie listy narzedzi
- panel do listowania, edycji, testowania, wlaczania i wylaczania Tools
- audit trail dla narzedzi utworzonych lub zmienionych przez agentow

## 14. Definicja sukcesu

DABot spelnia docelowa wizje, gdy:

- obecne proste scenariusze nadal dzialaja bez zmian koncepcyjnych,
- scenariusz moze zostac zawieszony i wznowiony bez utrzymywania procesu,
- stan runu przetrwa restart,
- dowolne zewnetrzne zdarzenie moze wznowic odpowiedni run,
- obserwator strony moze wykryc zmiane i opublikowac zdarzenie,
- panel webowy moze konfigurowac scenariusze i pokazywac przebieg bez bycia wymaganym runtime,
- przegladarka moze korzystac z sesji efemerycznych i trwalych,
- transport zdarzen i storage mozna wymienic bez przebudowy Core,
- DABot moze wystawiac swoje mozliwosci przez MCP bez duplikowania silnika wykonawczego,
- agent moze utworzyc deklaratywne narzedzie z dozwolonych Actions, przetestowac je i aktywowac zgodnie z polityka uprawnien.
