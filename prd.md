# PRD – Narzędzie Desktop Bot Automation (.NET / C#, Linux)

## 1. Nazwa robocza projektu
**Desktop Automation Bot**  
(roboczo: DABot)

## 2. Cel produktu
Stworzenie aplikacji w **C# / .NET**, która automatyzuje zadania wykonywane w przeglądarce oraz komunikuje się z lokalnym API.

Aplikacja ma działać docelowo na **Linuxie** jako narzędzie CLI/worker, z możliwością uruchamiania Chromium w trybie headless. Środowisko Windows może być używane developersko, ale implementacja MVP nie może zależeć od mechanizmów dostępnych wyłącznie na Windows.

Narzędzie ma działać jako uniwersalny bot wykonujący scenariusze biznesowe, np.:
- pobranie danych z API
- otwarcie strony WWW
- zalogowanie użytkownika
- kliknięcie przycisków
- wklejenie tekstu do formularzy
- pobranie wyników ze strony
- zapisanie rezultatu do API
- raportowanie błędów

## 3. Problem do rozwiązania
Wiele procesów wymaga ręcznego wykonywania powtarzalnych czynności w przeglądarce:
- kopiuj → wklej
- kliknij
- sprawdź status
- przepisz dane
- pobierz wynik
- wyślij dalej

To zajmuje czas, generuje błędy i blokuje człowieka.

## 4. Wizja produktu
Użytkownik uruchamia bota, a ten samodzielnie wykonuje zadania w przeglądarce na podstawie danych z API lub lokalnej konfiguracji.

Bot ma być:
- szybki
- stabilny
- rozszerzalny
- prosty do utrzymania
- gotowy pod przyszłe scenariusze
- gotowy do uruchamiania na Linuxie, także w środowiskach serwerowych/CI

## 5. Grupa docelowa
### Główna:
- developerzy .NET
- testerzy
- administratorzy
- administratorzy Linux / DevOps
- osoby automatyzujące pracę biurową

### Dodatkowa:
- firmy z systemami bez API
- użytkownicy wykonujący powtarzalne operacje

## 6. Zakres MVP (wersja 1.0)
### 6.1 Integracja z lokalnym API
Bot potrafi:
- GET dane wejściowe
- POST wynik
- PUT status
- obsłużyć token/autoryzację

### 6.2 Automatyzacja przeglÄ…darki
Bot potrafi:
- uruchomiÄ‡ Chromium / Chrome
- wejĹ›Ä‡ na adres URL
- czekaÄ‡ na zaĹ‚adowanie strony
- kliknÄ…Ä‡ element
- wpisaÄ‡ tekst
- wkleiÄ‡ tekst
- odczytaÄ‡ tekst ze strony
- pobraÄ‡ HTML
- zrobiÄ‡ screenshot
- otwieraÄ‡, zamykaÄ‡ i przeĹ‚Ä…czaÄ‡ siÄ™ miÄ™dzy zakĹ‚adkami, gdy scenariusz tego wymaga
- uruchamiaÄ‡ przeglÄ…darkÄ™ z trwaĹ‚ym profilem uĹĽytkownika, aby zachowaÄ‡ sesjÄ™ logowania

### 6.3 Sesja uĹĽytkownika i logowanie rÄ™czne
Bot musi wspieraÄ‡ scenariusz, w ktĂłrym:
- uĹĽytkownik uruchamia przeglÄ…darkÄ™ w trybie headed
- uĹĽytkownik loguje siÄ™ rÄ™cznie, np. przez konto Google
- bot zapisuje profil przeglÄ…darki i uĹĽywa go w kolejnych uruchomieniach
- sesja moĹĽe byÄ‡ przypisana do nazwanego profilu, np. `default`, `google`, `prod`
- tryb headless nadal pozostaje domyĹ›lny dla zwykĹ‚ych uruchomieĹ„

### 6.4 Scenariusze
Bot wykonuje kroki zapisane jako scenariusz.

### 6.5 Logowanie
System zapisuje:
- start procesu
- wykonane kroki
- bĹ‚Ä™dy
- czas wykonania
- odpowiedzi API

### 6.6 ObsĹ‚uga bĹ‚Ä™dĂłw
W przypadku bĹ‚Ä™du:
- screenshot
- zapis HTML
- retry
- komunikat koĹ„cowy

### 6.7 Linux runtime
Bot w MVP musi:
- dziaĹ‚aÄ‡ na Linuxie jako aplikacja konsolowa
- uruchamiaÄ‡ Chromium przez Playwright w trybie headless
- wspieraÄ‡ tryb headed tylko wtedy, gdy dostÄ™pny jest serwer graficzny, np. X11/Xvfb
- uĹĽywaÄ‡ przenoĹ›nych Ĺ›cieĹĽek plikĂłw i separatorĂłw
- korzystaÄ‡ z konfiguracji przez pliki JSON i zmienne Ĺ›rodowiskowe
- unikaÄ‡ zaleĹĽnoĹ›ci od Windows-only API, np. WPF, rejestru Windows, DPAPI jako jedynego mechanizmu sekretĂłw
- dokumentowaÄ‡ instalacjÄ™ zaleĹĽnoĹ›ci Playwright na Linuxie

## 7. Funkcje po MVP (v2+)
- UI do budowy scenariuszy
- Harmonogram
- Kolejka zadań
- Wiele botów równolegle
- OCR
- AI decision engine
- Obsługa aplikacji desktopowych jako osobny moduł zależny od systemu operacyjnego

## 8. User Stories
- Jako użytkownik chcę uruchomić bota, aby sam wykonał proces w przeglądarce.
- Jako użytkownik chcę pobrać dane z API, aby bot działał dynamicznie.
- Jako użytkownik chcę dostać log błędu.
- Jako użytkownik chcę łatwo dodawać nowe scenariusze.

## 9. Wymagania funkcjonalne
### API
- obsługa REST
- JSON request/response
- timeout
- retry
- token bearer

### Browser Engine
- Playwright
- Chromium
- headless on/off
- wiele kart
- sesje użytkownika
- Linux headless jako podstawowy tryb uruchomienia

### Scenariusze
Typy kroków:
- OpenUrl
- Click
- FillText
- PasteText
- WaitFor
- ReadText
- Screenshot
- CallApi
- If
- Loop
- Delay

### Storage
- config.json
- logs/
- screenshots/
- scenarios/
- ścieżki przenośne między Windows i Linux

## 10. Wymagania niefunkcjonalne
- start < 5 sekund
- modularna architektura
- możliwość wielu scenariuszy
- szyfrowanie sekretów
- brak haseł w kodzie
- zgodność z Linuxem jako środowiskiem docelowym
- brak obowiązkowych zależności Windows-only
- poprawna praca w trybie headless bez aktywnej sesji graficznej

## 11. Architektura techniczna
### Stack
- .NET 8
- C#
- Playwright for .NET
- HttpClient
- Serilog
- Polly
- Microsoft DI
- Linux runtime: Ubuntu/Debian compatible jako główny target wdrożeniowy

### Warstwy
- Core
- Infrastructure
- Application
- UI (opcjonalnie, cross-platform; preferowana Avalonia zamiast WPF)

## 12. Struktura projektu
```text
DesktopAutomationBot.sln
src/
 ├── DesktopAutomationBot.Core
 ├── DesktopAutomationBot.Application
 ├── DesktopAutomationBot.Infrastructure
 ├── DesktopAutomationBot.Runner
 └── DesktopAutomationBot.UI
```

## 13. Model scenariusza JSON
```json
{
  "name": "Dodaj zgłoszenie",
  "steps": [
    { "type": "OpenUrl", "url": "https://app.local" },
    { "type": "FillText", "selector": "#title", "value": "{{title}}" },
    { "type": "Click", "selector": "#save" },
    { "type": "ReadText", "selector": ".result", "output": "result" }
  ]
}
```

## 14. Flow działania
Start → Załaduj scenariusz → Uruchom browser → Wykonaj kroki → Raport

## 15. Roadmap
### Sprint 1
- skeleton solution
- Playwright start
- Click / Fill / Read
- Linux Playwright setup

### Sprint 2
- API integration
- logs
- screenshots
- Linux-compatible filesystem paths

### Sprint 3
- JSON scenarios
- retry
- variables

### Sprint 4
- UI panel cross-platform albo dalszy rozwój CLI/worker
