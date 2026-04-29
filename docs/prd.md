# PRD Ă˘â‚¬â€ś NarzĂ„â„˘dzie Desktop Bot Automation (.NET / C#, Linux)

## 1. Nazwa robocza projektu
**Desktop Automation Bot**  
(roboczo: DABot)

## 2. Cel produktu
Stworzenie aplikacji w **C# / .NET**, ktÄ‚Ĺ‚ra automatyzuje zadania wykonywane w przeglĂ„â€¦darce oraz komunikuje siĂ„â„˘ z lokalnym API.

Aplikacja ma dzialac docelowo na **Linuxie** jako narzedzie CLI/worker, z mozliwoscia uruchamiania Chromium w trybie headless. Windows ma byc wspierany jako platforma developerska i testowa, o ile nie wymaga to mechanizmow dostepnych wylacznie na Windows i nie rozbija zgodnosci z Linuxem.

NarzĂ„â„˘dzie ma dziaÄąâ€šaĂ„â€ˇ jako uniwersalny bot wykonujĂ„â€¦cy scenariusze biznesowe, np.:
- pobranie danych z API
- otwarcie strony WWW
- zalogowanie uÄąÄ˝ytkownika
- klikniĂ„â„˘cie przyciskÄ‚Ĺ‚w
- wklejenie tekstu do formularzy
- pobranie wynikÄ‚Ĺ‚w ze strony
- zapisanie rezultatu do API
- raportowanie bÄąâ€šĂ„â„˘dÄ‚Ĺ‚w

## 3. Problem do rozwiĂ„â€¦zania
Wiele procesÄ‚Ĺ‚w wymaga rĂ„â„˘cznego wykonywania powtarzalnych czynnoÄąâ€şci w przeglĂ„â€¦darce:
- kopiuj Ă˘â€ â€™ wklej
- kliknij
- sprawdÄąĹź status
- przepisz dane
- pobierz wynik
- wyÄąâ€şlij dalej

To zajmuje czas, generuje bÄąâ€šĂ„â„˘dy i blokuje czÄąâ€šowieka.

## 4. Wizja produktu
UÄąÄ˝ytkownik uruchamia bota, a ten samodzielnie wykonuje zadania w przeglĂ„â€¦darce na podstawie danych z API lub lokalnej konfiguracji.

Bot ma byĂ„â€ˇ:
- szybki
- stabilny
- rozszerzalny
- prosty do utrzymania
- gotowy pod przyszÄąâ€še scenariusze
- gotowy do uruchamiania na Linuxie, takÄąÄ˝e w Äąâ€şrodowiskach serwerowych/CI

## 5. Grupa docelowa
### GÄąâ€šÄ‚Ĺ‚wna:
- developerzy .NET
- testerzy
- administratorzy
- administratorzy Linux / DevOps
- osoby automatyzujĂ„â€¦ce pracĂ„â„˘ biurowĂ„â€¦

### Dodatkowa:
- firmy z systemami bez API
- uÄąÄ˝ytkownicy wykonujĂ„â€¦cy powtarzalne operacje

## 6. Zakres MVP (wersja 1.0)
### 6.1 Integracja z lokalnym API
Bot potrafi:
- GET dane wejÄąâ€şciowe
- POST wynik
- PUT status
- obsÄąâ€šuÄąÄ˝yĂ„â€ˇ token/autoryzacjĂ„â„˘

### 6.2 Automatyzacja przeglÄ‚â€žĂ˘â‚¬Â¦darki
Bot potrafi:
- uruchomiÄ‚â€žĂ˘â‚¬Ë‡ Chromium / Chrome
- wejĂ„Ä…Ă˘â‚¬ĹźÄ‚â€žĂ˘â‚¬Ë‡ na adres URL
- czekaÄ‚â€žĂ˘â‚¬Ë‡ na zaĂ„Ä…Ă˘â‚¬Ĺˇadowanie strony
- kliknÄ‚â€žĂ˘â‚¬Â¦Ä‚â€žĂ˘â‚¬Ë‡ element
- wpisaÄ‚â€žĂ˘â‚¬Ë‡ tekst
- wkleiÄ‚â€žĂ˘â‚¬Ë‡ tekst
- odczytaÄ‚â€žĂ˘â‚¬Ë‡ tekst ze strony
- pobraÄ‚â€žĂ˘â‚¬Ë‡ HTML
- zrobiÄ‚â€žĂ˘â‚¬Ë‡ screenshot
- otwieraÄ‚â€žĂ˘â‚¬Ë‡, zamykaÄ‚â€žĂ˘â‚¬Ë‡ i przeĂ„Ä…Ă˘â‚¬ĹˇÄ‚â€žĂ˘â‚¬Â¦czaÄ‚â€žĂ˘â‚¬Ë‡ siÄ‚â€žĂ˘â€žË miÄ‚â€žĂ˘â€žËdzy zakĂ„Ä…Ă˘â‚¬Ĺˇadkami, gdy scenariusz tego wymaga
- uruchamiaÄ‚â€žĂ˘â‚¬Ë‡ przeglÄ‚â€žĂ˘â‚¬Â¦darkÄ‚â€žĂ˘â€žË z trwaĂ„Ä…Ă˘â‚¬Ĺˇym profilem uĂ„Ä…Ă„Ëťytkownika, aby zachowaÄ‚â€žĂ˘â‚¬Ë‡ sesjÄ‚â€žĂ˘â€žË logowania

### 6.3 Sesja uĂ„Ä…Ă„Ëťytkownika i logowanie rÄ‚â€žĂ˘â€žËczne
Bot musi wspieraÄ‚â€žĂ˘â‚¬Ë‡ scenariusz, w ktĂ„â€šÄąâ€šrym:
- uĂ„Ä…Ă„Ëťytkownik uruchamia przeglÄ‚â€žĂ˘â‚¬Â¦darkÄ‚â€žĂ˘â€žË w trybie headed
- uĂ„Ä…Ă„Ëťytkownik loguje siÄ‚â€žĂ˘â€žË rÄ‚â€žĂ˘â€žËcznie, np. przez konto Google
- bot zapisuje profil przeglÄ‚â€žĂ˘â‚¬Â¦darki i uĂ„Ä…Ă„Ëťywa go w kolejnych uruchomieniach
- sesja moĂ„Ä…Ă„Ëťe byÄ‚â€žĂ˘â‚¬Ë‡ przypisana do nazwanego profilu, np. `default`, `google`, `prod`
- tryb headless nadal pozostaje domyĂ„Ä…Ă˘â‚¬Ĺźlny dla zwykĂ„Ä…Ă˘â‚¬Ĺˇych uruchomieĂ„Ä…Ă˘â‚¬Ĺľ

### 6.4 Scenariusze
Bot wykonuje kroki zapisane jako scenariusz.

### 6.5 Logowanie
System zapisuje:
- start procesu
- wykonane kroki
- bĂ„Ä…Ă˘â‚¬ĹˇÄ‚â€žĂ˘â€žËdy
- czas wykonania
- odpowiedzi API

### 6.6 ObsĂ„Ä…Ă˘â‚¬Ĺˇuga bĂ„Ä…Ă˘â‚¬ĹˇÄ‚â€žĂ˘â€žËdĂ„â€šÄąâ€šw
W przypadku bĂ„Ä…Ă˘â‚¬ĹˇÄ‚â€žĂ˘â€žËdu:
- screenshot
- zapis HTML
- retry
- komunikat koĂ„Ä…Ă˘â‚¬Ĺľcowy

### 6.7 Linux runtime
### 6.7 Linux runtime`r`nBot w MVP musi:`r`n- dzialac na Linuxie jako aplikacja konsolowa`r`n- dzialac rowniez na Windows jako lokalny target developerski i testowy`r`n- uruchamiac Chromium przez Playwright w trybie headless`r`n- wspierac tryb headed tylko wtedy, gdy dostepny jest serwer graficzny, np. X11/Xvfb na Linuxie albo aktywna sesja graficzna na Windows`r`n- uzywac przenosnych sciezek plikow i separatorow`r`n- korzystac z konfiguracji przez pliki JSON i zmienne srodowiskowe`r`n- unikac zaleznosci od Windows-only API, np. WPF, rejestru Windows, DPAPI jako jedynego mechanizmu sekretow`r`n- dokumentowac instalacje zaleznosci Playwright na Linuxie`r`n- Harmonogram
- Kolejka zadaÄąâ€ž
- Wiele botÄ‚Ĺ‚w rÄ‚Ĺ‚wnolegle
- OCR
- AI decision engine
- ObsÄąâ€šuga aplikacji desktopowych jako osobny moduÄąâ€š zaleÄąÄ˝ny od systemu operacyjnego

## 8. User Stories
- Jako uÄąÄ˝ytkownik chcĂ„â„˘ uruchomiĂ„â€ˇ bota, aby sam wykonaÄąâ€š proces w przeglĂ„â€¦darce.
- Jako uÄąÄ˝ytkownik chcĂ„â„˘ pobraĂ„â€ˇ dane z API, aby bot dziaÄąâ€šaÄąâ€š dynamicznie.
- Jako uÄąÄ˝ytkownik chcĂ„â„˘ dostaĂ„â€ˇ log bÄąâ€šĂ„â„˘du.
- Jako uÄąÄ˝ytkownik chcĂ„â„˘ Äąâ€šatwo dodawaĂ„â€ˇ nowe scenariusze.

## 9. Wymagania funkcjonalne
### API
- obsÄąâ€šuga REST
- JSON request/response
- timeout
- retry
- token bearer

### Browser Engine
- Playwright
- Chromium
- headless on/off
- wiele kart
- sesje uÄąÄ˝ytkownika
- Linux headless jako podstawowy tryb uruchomienia

### Scenariusze
Typy krokÄ‚Ĺ‚w:
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
- Äąâ€şcieÄąÄ˝ki przenoÄąâ€şne miĂ„â„˘dzy Windows i Linux

## 10. Wymagania niefunkcjonalne
- start < 5 sekund
- modularna architektura
- moÄąÄ˝liwoÄąâ€şĂ„â€ˇ wielu scenariuszy
- szyfrowanie sekretÄ‚Ĺ‚w
- brak haseÄąâ€š w kodzie
- zgodnoÄąâ€şĂ„â€ˇ z Linuxem jako Äąâ€şrodowiskiem docelowym
- brak obowiĂ„â€¦zkowych zaleÄąÄ˝noÄąâ€şci Windows-only
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
- Linux runtime: Ubuntu/Debian compatible jako gÄąâ€šÄ‚Ĺ‚wny target wdroÄąÄ˝eniowy

### Warstwy
- Core
- Infrastructure
- Application
- UI (opcjonalnie, cross-platform; preferowana Avalonia zamiast WPF)

## 12. Struktura projektu
```text
DesktopAutomationBot.sln
src/
 Ă˘â€ťĹ›Ă˘â€ťâ‚¬Ă˘â€ťâ‚¬ DesktopAutomationBot.Core
 Ă˘â€ťĹ›Ă˘â€ťâ‚¬Ă˘â€ťâ‚¬ DesktopAutomationBot.Application
 Ă˘â€ťĹ›Ă˘â€ťâ‚¬Ă˘â€ťâ‚¬ DesktopAutomationBot.Infrastructure
 Ă˘â€ťĹ›Ă˘â€ťâ‚¬Ă˘â€ťâ‚¬ DesktopAutomationBot.Runner
 Ă˘â€ťâ€ťĂ˘â€ťâ‚¬Ă˘â€ťâ‚¬ DesktopAutomationBot.UI
```

## 13. Model scenariusza JSON
```json
{
  "name": "Dodaj zgÄąâ€šoszenie",
  "steps": [
    { "type": "OpenUrl", "url": "https://app.local" },
    { "type": "FillText", "selector": "#title", "value": "{{title}}" },
    { "type": "Click", "selector": "#save" },
    { "type": "ReadText", "selector": ".result", "output": "result" }
  ]
}
```

## 14. Flow dziaÄąâ€šania
Start Ă˘â€ â€™ ZaÄąâ€šaduj scenariusz Ă˘â€ â€™ Uruchom browser Ă˘â€ â€™ Wykonaj kroki Ă˘â€ â€™ Raport

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
- UI panel cross-platform albo dalszy rozwÄ‚Ĺ‚j CLI/worker
