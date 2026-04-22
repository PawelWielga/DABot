# PRD – Narzędzie Desktop Bot Automation (.NET / C#)

## 1. Nazwa robocza projektu
**Desktop Automation Bot**  
(roboczo: DABot)

## 2. Cel produktu
Stworzenie aplikacji w **C# / .NET**, która automatyzuje zadania wykonywane w przeglądarce oraz komunikuje się z lokalnym API.

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

## 5. Grupa docelowa
### Główna:
- developerzy .NET
- testerzy
- administratorzy
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

### 6.2 Automatyzacja przeglądarki
Bot potrafi:
- uruchomić Chromium / Chrome
- wejść na adres URL
- czekać na załadowanie strony
- kliknąć element
- wpisać tekst
- wkleić tekst
- odczytać tekst ze strony
- pobrać HTML
- zrobić screenshot

### 6.3 Scenariusze
Bot wykonuje kroki zapisane jako scenariusz.

### 6.4 Logowanie
System zapisuje:
- start procesu
- wykonane kroki
- błędy
- czas wykonania
- odpowiedzi API

### 6.5 Obsługa błędów
W przypadku błędu:
- screenshot
- zapis HTML
- retry
- komunikat końcowy

## 7. Funkcje po MVP (v2+)
- UI do budowy scenariuszy
- Harmonogram
- Kolejka zadań
- Wiele botów równolegle
- OCR
- AI decision engine
- Obsługa aplikacji desktopowych

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

## 10. Wymagania niefunkcjonalne
- start < 5 sekund
- modularna architektura
- możliwość wielu scenariuszy
- szyfrowanie sekretów
- brak haseł w kodzie

## 11. Architektura techniczna
### Stack
- .NET 8
- C#
- Playwright for .NET
- HttpClient
- Serilog
- Polly
- Microsoft DI

### Warstwy
- Core
- Infrastructure
- Application
- UI (opcjonalnie)

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

### Sprint 2
- API integration
- logs
- screenshots

### Sprint 3
- JSON scenarios
- retry
- variables

### Sprint 4
- UI panel

