# CLAUDE.md

System do zarządzania pracą pizzerii/restauracji z przyjmowaniem zamówień online. Projekt budowany z myślą o komercjalizacji (SaaS dla wielu sieci i lokali).

> Nazwa produktu jeszcze nieustalona. W kodzie używamy prefiksu `Pizzeria.` (namespace'y, nazwy projektów). Zmiana nazwy później = jeden rename.

## Produkt w skrócie

Trzy aplikacje dla trzech grup użytkowników + agent sprzętowy:

| Część | Kto używa | Gdzie | Co robi |
|---|---|---|---|
| **Panel lokalu** (`web-staff`) | kierownik, obsługa, kuchnia | komputer w lokalu (PWA) | przyjmowanie zamówień telefonicznych, kolejka zamówień, ekran kuchni, przydzielanie kierowców, rozliczenia, raporty |
| **Aplikacja kierowcy** (część `web-staff`) | dostawcy | telefon (PWA) | lista kursów, statusy, nawigacja, GPS na żywo, rozliczenie zmiany |
| **Panel firmy** (część `web-staff`) | właściciel/admin sieci | komputer | menu i ceny, lokale, strefy dostaw, pracownicy, raporty zbiorcze |
| **Sklep klienta** (`web-shop`) | klienci | telefon/komputer | menu, koszyk, zamówienie (gość lub konto), płatność, śledzenie statusu i kierowcy |
| **Agent lokalny** (`Pizzeria.LocalAgent`) | — | komputer w lokalu (usługa Windows) | drukarka fiskalna, drukarka bonów kuchennych |

**Najważniejsza zasada UX: szybko i prosto.** Obsługa pracuje w biegu, często w rękawiczkach lub z mąką na rękach. Każdy ekran projektujemy pod minimum kliknięć, duże cele dotykowe i czytelność z odległości.

## Model biznesowy: wielu najemców

Hierarchia: **Platforma → Firma (sieć) → Lokal**

```
Domino's (Company)      → Domino's Białystok, Domino's Warszawa (Location)
Pizza Hut (Company)     → Pizza Hut Warszawa, Pizza Hut Poznań (Location)
```

- **Firma (`Company`) = najemca (tenant).** Każda tabela z danymi firmy ma `CompanyId`. Izolacja przez globalne filtry zapytań w EF Core (`HasQueryFilter`) oparte o `ITenantContext`.
- **Lokal (`Location`)** należy do firmy. Dane operacyjne (zamówienia, zmiany kierowców, strefy dostaw, drukarki) mają też `LocationId`.
- **Menu i ceny ustala wyłącznie firma.** Jedno menu i cennik dla wszystkich lokali sieci. Lokal NIE edytuje menu ani cen.
  - Wyjątek operacyjny (założenie, do potwierdzenia): lokal może chwilowo oznaczyć pozycję jako niedostępną („skończyło się ciasto”) — to flaga dostępności, nie edycja menu.
- Każdy lokal ma własne: strefy dostaw, godziny otwarcia, adres, pracowników, drukarki, ustawienia czasu realizacji.
- Sklep klienta działa w kontekście firmy (osobna subdomena/domena firmy, np. `dominos.nasza-platforma.pl`), klient wybiera lokal lub lokal wynika z adresu dostawy (strefa).

### Role

| Rola | Zakres |
|---|---|
| `PlatformAdmin` | cała platforma (my) |
| `CompanyAdmin` | cała firma: menu, ceny, lokale, pracownicy, raporty zbiorcze |
| `LocationManager` | jeden/kilka lokali: zamówienia, pracownicy lokalu, rozliczenia, raporty lokalu |
| `Staff` | obsługa lokalu: przyjmowanie i prowadzenie zamówień |
| `Kitchen` | ekran kuchni |
| `Driver` | aplikacja kierowcy |
| `Customer` | konto klienta (opcjonalne) |

Uprawnienia sprawdzamy po stronie API zawsze z uwzględnieniem tenanta i lokalu — nigdy tylko po stronie frontendu.

## Stack

### Backend
- **.NET 10 (LTS), ASP.NET Core** — Web API
- **SignalR** — zamówienia i statusy na żywo, pozycja kierowcy, komunikacja z agentem lokalnym
- **PostgreSQL + PostGIS** — strefy dostaw jako poligony, punkty adresów i kierowców
- **EF Core + Npgsql** (+ NetTopologySuite dla geometrii)
- **ASP.NET Core Identity** — użytkownicy, role
- **FluentValidation** — walidacja wejścia
- **Serilog** — logowanie strukturalne
- **xUnit + Testcontainers (PostgreSQL)** — testy

### Frontend
- **React + TypeScript + Vite**, monorepo na **pnpm workspaces**
- **TanStack Query** — dane z API; **React Router** — routing
- **@microsoft/signalr** — realtime
- **react-i18next** — wszystkie teksty UI przez i18n (na start tylko `pl`, struktura gotowa na `en`)
- **Leaflet + OpenStreetMap** — mapy (strefy, kierowcy)
- **vite-plugin-pwa** — instalowalne PWA dla `web-staff`
- **Vitest + React Testing Library**; Playwright do e2e w późniejszym etapie
- Styl: Tailwind CSS. Komponenty proste, własne lub shadcn/ui.

### Agent lokalny
- **.NET Worker Service** uruchamiany jako usługa Windows
- Łączy się **wychodząco** z API przez SignalR (żadnych otwartych portów w lokalu), uwierzytelnia się tokenem urządzenia
- Odbiera zadania druku: paragon fiskalny, bon kuchenny (ESC/POS)
- Sterowniki za interfejsem `IFiscalPrinter` / `IReceiptPrinter`; na start **symulator** (zapis do pliku/logu). Konkretne protokoły (Posnet, Novitus, Elzab) dodajemy później.

### Infrastruktura
- Lokalnie: `docker-compose` (PostgreSQL z PostGIS, opcjonalnie pgAdmin)
- Hosting produkcyjny: **jeszcze nie wybrany.** Wszystko ma działać w kontenerach Docker, konfiguracja przez zmienne środowiskowe, bez zależności od usług konkretnej chmury.

## Struktura repozytorium (docelowa)

```
/
├── CLAUDE.md
├── docker-compose.yml
├── backend/
│   ├── Pizzeria.sln
│   ├── src/
│   │   ├── Pizzeria.Domain/          # encje, value objecty, maszyna stanów zamówienia, reguły cenowe
│   │   ├── Pizzeria.Application/     # przypadki użycia, DTO, interfejsy (płatności, geokodowanie, druk)
│   │   ├── Pizzeria.Infrastructure/  # EF Core, migracje, integracje zewnętrzne
│   │   ├── Pizzeria.Api/             # endpointy, huby SignalR, auth, middleware tenanta
│   │   └── Pizzeria.LocalAgent/      # usługa Windows dla drukarek
│   └── tests/
│       ├── Pizzeria.Domain.Tests/
│       ├── Pizzeria.Application.Tests/
│       └── Pizzeria.Api.IntegrationTests/
└── frontend/
    ├── pnpm-workspace.yaml
    ├── apps/
    │   ├── web-staff/   # PWA: panel lokalu, kuchnia, kierowca, panel firmy (osobne sekcje routingu)
    │   └── web-shop/    # sklep klienta
    └── packages/
        ├── api-client/  # typy i klient API (generowane z OpenAPI)
        ├── i18n/        # tłumaczenia
        └── ui/          # wspólne komponenty
```

Typy w `api-client` generujemy z OpenAPI backendu — nie piszemy ręcznie DTO po stronie frontendu.

## Domena

### Zamówienie — maszyna stanów

```
New → Accepted → InPreparation → Ready ─┬─ (dostawa)  → AssignedToDriver → InDelivery → Delivered
                                        └─ (odbiór)   → PickedUp
Każdy stan przed Delivered/PickedUp → Cancelled (z powodem)
```

- Przejścia stanów tylko przez metody domenowe (`order.Accept()`, `order.AssignDriver(...)` itd.), nigdy przez ustawienie pola z zewnątrz. Niedozwolone przejście = wyjątek domenowy.
- Każda zmiana statusu zapisywana w historii (`OrderStatusHistory`: kto, kiedy, z jakiego na jaki) i rozsyłana przez SignalR.

**Jak to jest zrobione (Domain/Orders, Application/Orders):**
- `Order.Place(...)` przyjmuje linie już wycenione przez `ItemPricing` (snapshot nazw, rozmiaru, dodatków i cen w `OrderItem`). Liczy sumy, cenę dostawy i sprawdza minimum strefy. Status startowy to `New`, płatność `Unpaid`.
- `Order.NextStatus` / `Advance()` to „jedno kliknięcie = następny status” dla kuchni i kolejki. `Ready` → kierowca wymaga `AssignDriver(...)`. Gotówka i karta przy odbiorze: płatność zmienia się na `Paid` przy `Delivered`/`PickedUp`.
- Płatność `Online` jest odrzucana (`order.payment_method_not_available`) do czasu operatora płatności (etap 2).
- Numer zamówienia jest krótki, per lokal i dzień lokalny (strefa czasowa lokalu). Generuje go atomowy upsert w `order_counters` (`OrderNumberGenerator`).
- `TrackingToken` to losowy sekret w linku śledzenia. `GET /api/shop/orders/{token}` nadal podlega filtrowi tenanta (firma z subdomeny).
- `PlaceOrderService` obsługuje sklep (`Online`, anonimowo) i jest gotowy na zamówienia telefoniczne (`Phone`, `tenant.UserId` w historii). Lokal dla dostawy wynika ze strefy (`DeliveryResolver`), dla odbioru wybiera go klient.
- `IOrderNotifier` (na razie `NullOrderNotifier`) to punkt, w którym podłączymy SignalR.
- **Godzin otwarcia jeszcze nie ma**: zamówienia są przyjmowane zawsze.
- **Dev:** sklep działa na `http://demo.localhost:5174` (subdomena = slug firmy). Proxy Vite zachowuje `Host` (`changeOrigin: false`), a API obsługuje `X-Forwarded-*` (ufa tylko proxy z loopbacka; produkcja musi dodać sieć swojego proxy).
- Źródło zamówienia: `Online` (sklep) lub `Phone`/`InStore` (panel lokalu).
- Status płatności niezależny od statusu zamówienia: `Unpaid`, `PendingOnline`, `Paid`, `Refunded`.
- Metoda płatności: `Cash`, `CardOnDelivery`, `Online`.

### Menu (na poziomie firmy)
- `Category` → `Product` → `ProductVariant` (rozmiar: 32 cm, 42 cm...)
- `ModifierGroup` (np. „Dodatki”, „Sos”) z `MinSelect`/`MaxSelect`, przypięte do produktów
- `Modifier` z ceną zależną od rozmiaru
- **Pół na pół:** pizza złożona z dwóch połówek; reguła ceny konfigurowalna na poziomie firmy (domyślnie: cena droższej połówki)
- Zestawy i promocje: **etap 2** — model projektujemy tak, żeby dało się je dodać bez przebudowy

**Jak to jest zrobione (Domain/Menu):**
- **Rozmiary należą do kategorii** (`Category.Sizes`, np. Pizza: 32/42 cm, Napoje: 0,5/1 l). „Wariant” produktu to jego cena w danym rozmiarze (`Product.Prices`); brak ceny = produkt nie występuje w tym rozmiarze.
- **Grupa dodatków należy do jednej kategorii** (`ModifierGroup.CategoryId`), a ceny dodatków są per rozmiar tej kategorii. Produkt może mieć tylko grupy swojej kategorii. Dodatek wybiera się maksymalnie raz (bez ilości).
- **`ItemPricing.Price(...)`** to jedyne miejsce, które zamienia wybór klienta (produkt / dwie połówki, rozmiar, dodatki) na kwotę. Sprawdza dostępność, rozmiar oraz min/max w grupach. Używane przez zamówienia (krok 5).
- **Pół na pół:** obie połówki z tej samej kategorii, obie z `AllowsHalves`, ten sam rozmiar. Reguła ceny jest ustawieniem firmy (`Company.HalfAndHalfPricing`: droższa połówka / średnia zaokrąglona do grosza). Dodatki dotyczą całej pizzy i można wybierać tylko z grup wspólnych dla obu połówek (założenie, patrz „Otwarte decyzje”).
- **Usuwanie:** produkty i dodatki się dezaktywuje (`IsActive`), nie usuwa. Rozmiaru, który ma ceny, nie da się usunąć (`menu.size_in_use`). Usuwania kategorii i grup na razie nie ma.
- **Persystencja:** agregaty mają kolekcje posiadane (`OwnsMany`) w osobnych tabelach. Wiersze podrzędne mają własny klucz Guid: przy kluczu złożonym (właściciel + rozmiar) EF robi UPDATE zamiast INSERT dla nowych wierszy. Kolekcje z bazy nie mają kolejności, więc mapowanie do DTO zawsze sortuje po `SortOrder`.

### Ceny i pieniądze
- Zawsze `decimal` w C#, `numeric(10,2)` w bazie. Nigdy `float`/`double`. Waluta: PLN.
- **Cenę zamówienia liczy wyłącznie backend.** Frontend może pokazywać podgląd, ale API przelicza wszystko od zera i ignoruje ceny przesłane przez klienta.
- Pozycje zamówienia przechowują **snapshot** w chwili zamówienia (nazwa, wariant, modyfikatory, ceny) — późniejsza zmiana menu nie zmienia historycznych zamówień.

### Strefy dostaw (na poziomie lokalu)
- Poligon na mapie (PostGIS) + cena dostawy, minimalna wartość zamówienia, szacowany czas
- Adres klienta → geokodowanie → punkt → sprawdzenie, w której strefie i którego lokalu leży
- Geokodowanie za interfejsem `IGeocodingService` (dostawca do wyboru — patrz „Otwarte decyzje”)

**Jak to jest zrobione (Domain/Delivery):**
- `DeliveryZone.Area` to `geometry(Polygon, 4326)` z indeksem GiST. Granica jest walidowana: min. 3 punkty, bez samoprzecięć, niezerowe pole. Pierwszy punkt się nie powtarza, pierścień zamyka domena.
- Gdy strefy się nakładają (także strefy różnych lokali), wygrywa **najmniejsza** (`DeliveryZone.PickFor`). Liczą się tylko aktywne strefy aktywnych lokali.
- `POST /api/delivery/check` (adres → `Delivers`, lokal, strefa, cena dostawy, minimum, czas) działa dla obsługi i anonimowo dla sklepu (tenant z subdomeny). Ma limit zapytań, bo prawdziwy geokoder kosztuje.
- `FakeGeocodingService` mapuje adres deterministycznie na punkt w promieniu `Geocoding:Fake:RadiusKm` od `Geocoding:Fake:Center*` (domyślnie centrum Białegostoku). Testy integracyjne podmieniają go na `StubGeocoder`.
- `Location.Position` jest geokodowana przy tworzeniu lokalu i przy zmianie adresu (best effort, może być `null`) i służy jako środek mapy.
- **Edytor stref:** Leaflet bez wrappera i bez pluginu do rysowania; kliknięcie na mapie dodaje punkt. Kafelki z tile.openstreetmap.org tylko do developmentu. W testach UI `ZoneMap` jest podmieniany (`vi.mock`).

### Kierowcy
- **Lista kursów:** przypisane zamówienia, adres, telefon do klienta (przycisk „zadzwoń”), forma płatności i kwota do pobrania, przycisk otwierający nawigację w Google Maps
- **Statusy:** „Odebrałem z lokalu” → „Dostarczone” (jednym kliknięciem, duże przyciski)
- **GPS na żywo:** `navigator.geolocation.watchPosition` gdy aplikacja jest otwarta; wysyłka co ~10–15 s przez SignalR. Lokal widzi wszystkich aktywnych kierowców, klient widzi kierowcę **tylko dla swojego zamówienia w stanie `InDelivery`**.
  - Ograniczenie PWA: śledzenie w tle (ekran zgaszony, aplikacja zminimalizowana) jest zawodne, szczególnie na iOS. UI ma prosić kierowcę o trzymanie aplikacji na wierzchu i pokazywać ostrzeżenie, gdy pozycja przestaje dochodzić.
  - RODO: przechowujemy ostatnią pozycję + ślad tylko w czasie kursu, ślad usuwany po ustalonym czasie (np. 30 dni). Nie śledzimy kierowcy poza zmianą.
- **Rozliczenie zmiany (`DriverShift`):** start/koniec, kwota startowa (pogotówka), liczba kursów, suma gotówki do oddania, płatności kartą, online, napiwki. Kierownik potwierdza rozliczenie; rozbieżność zapisywana z komentarzem.

### Ekran kuchni
- Kolejka zamówień w stanach `Accepted`/`InPreparation`, od najstarszego
- Duże kafelki, czas od przyjęcia z kolorem (zielony → żółty → czerwony), dźwięk przy nowym zamówieniu
- Jedno kliknięcie = następny status

### Raporty
- Utarg dzienny/okresowy (lokal i zbiorczo dla firmy), podział na formy płatności
- Najlepiej sprzedające się pozycje
- Czasy: przyjęcie → gotowe → dostarczone
- Wyniki kierowców (kursy, średni czas dostawy)
- Raporty jako zapytania SQL/widoki w PostgreSQL zamiast liczenia w pamięci

### Klienci
- Zamówienie **jako gość** (imię, telefon, adres) lub z **opcjonalnym kontem** (historia zamówień, zapisane adresy)
- Klient po złożeniu zamówienia dostaje link do śledzenia statusu (działa także dla gościa — token w linku)

### Płatności
- Przy odbiorze (gotówka/karta u kierowcy lub w lokalu) — system tylko zapisuje formę i kwotę
- Online — za interfejsem `IPaymentProvider`; na start **fake provider** do developmentu. Docelowo polski operator z BLIK (Przelewy24 lub PayU — do decyzji).
  - Każda firma ma własne konto u operatora (pieniądze idą do firmy, nie do nas) → dane dostępowe per firma, szyfrowane.
  - Webhooki od operatora: weryfikacja podpisu, idempotencja (ten sam webhook przetworzony dwa razy nic nie psuje).

### Fiskalizacja
- Paragon fiskalny drukuje **agent lokalny** na drukarce w lokalu, na zlecenie z API.
- Zadania druku mają status i są ponawiane, jeśli agent był offline. Panel pokazuje, czy agent i drukarka są online.
- Zasady fiskalizacji (np. sprzedaż opłacona online z dostawą) do potwierdzenia z księgową/serwisem kas — nie zgadujemy przepisów w kodzie, logika „kiedy drukować paragon” ma być konfigurowalna.

## Realtime (SignalR)

Huby i grupy:
- `OrdersHub` — grupy `location:{locationId}` (panel, kuchnia), `order:{orderId}` (śledzenie przez klienta)
- `DriversHub` — `driver:{userId}`, pozycje kierowców do `location:{locationId}`
- `AgentHub` — `agent:{locationId}` dla agenta lokalnego

Zasada: SignalR tylko **powiadamia** o zmianie, źródłem prawdy jest REST API. Po reconnect klient pobiera aktualny stan przez API (TanStack Query invalidation), nie polega na tym, że nie zgubił komunikatów.

**Jak to jest zrobione:**
- Na razie jeden hub, `OrdersHub` (`/hubs/orders`), z grupami `location:{id}` (`JoinLocation`, sprawdza dostęp do lokalu), `order:{id}` (`TrackOrder(token)`, anonimowo, tenant z subdomeny) i `driver:{userId}` (automatycznie dla kierowcy). Komunikat `OrderChanged { orderId, locationId, number, status }`.
- Wywołania huba nie przechodzą przez middleware HTTP, więc `HubTenantFilter` ustawia `TenantContext` przy połączeniu i przy każdym wywołaniu (`TenantResolver`, wspólny z middleware). Token bearer dla WebSocketów idzie w `access_token` w query (tylko dla ścieżki huba).
- `SignalROrderNotifier` (`IOrderNotifier`) wysyła powiadomienie po złożeniu zamówienia i po każdej zmianie statusu (`OrderWorkflowService`).
- **Klienci:** w `web-staff` jest `OrdersFeed` (wstrzykiwany przez `createAppServices`, w testach podmieniany). Panel, kuchnia i kierowca robią invalidację zapytań. Bez połączenia lista odpytuje API co 30 s, a ekran pokazuje „Brak połączenia na żywo”. W `web-shop` jest `TrackingFeed` (anonimowo), z odpytywaniem co 15 s jako zapasem. W dev Vite proxuje `/hubs` z `ws: true`.
- Osobnego `DriversHub` nie ma: pozycje kierowców idą REST-em, a powiadomienie `DriverMoved` przez `OrdersHub` (patrz etap 2).
- `AgentHub` (`/hubs/agent`) jest tylko dla agenta lokalnego: wpuszcza wyłącznie schemat `AgentToken` (polityka `Agent`), grupa `agent:{locationId}`. Serwer wysyła tylko `JobsAvailable`; agent sam woła `FetchJobs`, `Confirm(jobId, printed, error)` i `Heartbeat(receiptOnline, fiscalOnline)`. SignalR (oba huby) serializuje enumy jako nazwy.

## Konwencje

### Ogólne
- **Kod, nazwy, komentarze, commity — po angielsku.** UI — po polsku przez i18n.
- Daty: w bazie UTC (`timestamptz`, `DateTimeOffset` w C#). Każdy lokal ma strefę czasową (domyślnie `Europe/Warsaw`); konwersja do czasu lokalnego przy wyświetlaniu i w raportach („dzień” w raporcie = doba lokalna).
- Backend zwraca błędy jako ProblemDetails z **kodem błędu** (np. `order.invalid_status_transition`), frontend tłumaczy kod przez i18n. Żadnych polskich komunikatów zahardkodowanych w API.

### Backend
- Endpointy pogrupowane per moduł (Orders, Menu, Locations, Drivers, Reports...)
- Logika biznesowa w Domain/Application, nie w endpointach ani w kontrolerach
- Każda encja tenanta ma `CompanyId` i podlega filtrowi globalnemu. Wyłączenie filtra (`IgnoreQueryFilters`) tylko w uzasadnionych miejscach (PlatformAdmin) i z komentarzem dlaczego.
- Migracje EF Core: jedna migracja na jedną logiczną zmianę, opisowa nazwa (`AddDeliveryZones`, nie `Update3`)
- Nullable reference types włączone, ostrzeżenia traktowane jako błędy

### Backend — tenancy i auth (jak to działa)
- **Auth:** bearer tokeny ASP.NET Core Identity (nieprzezroczyste, nie JWT): access 30 min, refresh 14 dni. Endpointy `POST /api/auth/login`, `POST /api/auth/refresh`, `GET /api/auth/me`. Refresh odbudowuje claimy z bazy, a zmiana security stampu unieważnia refresh token.
- **Claimy w tokenie:** role (`ClaimTypes.Role`), `company_id`, `location_id` (wiele), `display_name`, generowane przez `AppUserClaimsPrincipalFactory`. Zmiana ról lub lokali użytkownika = `UpdateSecurityStampAsync`.
- **Tenant per request:** `TenantResolutionMiddleware` wypełnia scoped `TenantContext`. Zalogowany użytkownik → claimy z tokenu; anonim → subdomena sklepu (`{slug}.{Tenancy:ShopRootDomain}`, w dev `demo.localhost`). Bez tenanta filtry zwracają pustkę.
- **Filtr globalny:** nazwany filtr `"Tenant"` (`AppDbContext.TenantFilter`) na każdej encji `ITenantEntity` i na `Company`. Dodatkowo `SaveChanges` blokuje zapis encji tenanta do innej firmy (`InvalidOperationException`).
- **Wyjątek: `users` nie są pod filtrem globalnym**, bo Identity musi znaleźć użytkownika przy logowaniu, zanim tenant jest znany. Każde zapytanie o użytkowników poza Identity filtruje `CompanyId` jawnie (`UserAccountService`).
- **Dostęp do lokalu:** `ITenantContext.CanAccessLocation(id)`, gdzie CompanyAdmin ma wszystkie lokale firmy, a role lokalowe tylko przypisane. Brak dostępu = 404 (nie zdradzamy istnienia zasobu).
- **Autoryzacja:** polityka fallback wymaga zalogowania (nowy endpoint jest domyślnie chroniony, publiczne mają jawne `AllowAnonymous`). Polityki ról na endpointach (`AuthorizationPolicies`) to tylko gruba bramka; właściwe reguły są w serwisach Application.
- **Błędy:** wyjątki `NotFoundException`/`ForbiddenException`/`ConflictException` (Application), `DomainException` (Domain) i `ValidationException` (FluentValidation, walidacja w serwisach) są mapowane przez `AppExceptionHandler` na ProblemDetails z `code`. Błędy pól: `errors: { "address.postalCode": ["validation.postal_code"] }`, a wbudowane kody FluentValidation są zamieniane na `validation.not_empty` itd. Każda odpowiedź błędu ma `code`, także 401/403/404/500 z frameworka.
- **Baza:** nazwy tabel i kolumn w snake_case (`SnakeCaseNaming`, bez dodatkowego pakietu), `decimal` → `numeric(10,2)` konwencją, Id jako UUIDv7 (`Guid.CreateVersion7`).
- **Start aplikacji:** `DatabaseInitializer` stosuje migracje (gdy `Database:MigrateOnStartup`, w Development włączone), zakłada role, opcjonalnie PlatformAdmina (`Seed:PlatformAdminEmail`/`Seed:PlatformAdminPassword`) i dane demo (`Seed:DemoData` + `Seed:DemoPassword`): firma `demo`, lokal „Demo Pizza Centrum”, użytkownicy `owner|manager|staff|kitchen|driver@demo.local`.
- Na razie e-mail użytkownika jest unikalny globalnie. Konta klientów (etap 2) będą wymagały unikalności per firma — do zaprojektowania przy kontach klientów.

### Frontend
- Komponenty funkcyjne, TypeScript `strict`
- Żadnych tekstów w JSX — tylko `t('...')`
- **Panel lokalu:** skróty klawiszowe dla częstych akcji (nowe zamówienie, wyszukiwanie klienta po telefonie), cele dotykowe min. 48 px, wysoki kontrast
- **Kierowca:** projekt mobile-first, obsługa jedną ręką, kluczowe przyciski na dole ekranu
- **Sklep:** szybkie ładowanie na słabym telefonie i LTE, zamówienie w jak najmniejszej liczbie kroków

### Frontend — jak to działa
- **Typy API:** `packages/api-client/src/schema.ts` jest generowany z OpenAPI (`pnpm generate:api`, wymaga API na :5080) i commitowany. Aliasy typów (`LocationDto` itd.) eksportuje `@pizzeria/api-client`. Generator idzie przez `pnpm dlx` z TypeScript 5, bo `openapi-typescript` nie działa z natywnym TS 7 używanym w workspace.
- **Kontrakt JSON:** API ma `RespectNullableAnnotations` + `RespectRequiredConstructorParameters`, więc pola w typach TS są wymagane i nie-null zgodnie z C#.
- **Sesja:** `AuthSession` (api-client) trzyma refresh token w localStorage (przetrwa przeładowanie PWA), a access token tylko w pamięci. Token odświeża się 30 s przed wygaśnięciem, równoległe wywołania korzystają z jednego odświeżenia, a odrzucony refresh oznacza wylogowanie. Błąd sieci nie wylogowuje.
- **Wywołania API:** `createApiClient` (openapi-fetch + middleware z tokenem; 401 → wylogowanie). Z TanStack Query używamy `unwrap(api.GET(...))`, który rzuca `ApiError` (`code`, `fieldErrors`). Po wylogowaniu cache zapytań jest czyszczony.
- **Tłumaczenie kodów z API:** `useCodeTranslations()` z `@pizzeria/i18n` (`error(code)` → `errors.<code>`, `validation(code)`, `role(role)`). W formularzach `useFormErrors(mutation.error)` daje baner i błędy per pole (klucze jak w API, np. `address.postalCode`).
- **web-staff:** `createAppServices()` + `AppServicesProvider` (sesja, klient API, QueryClient; w testach wstrzykiwany fałszywy `fetch`). Trasy są w `src/routes/index.tsx`, sekcje i role w `src/routes/navigation.ts` (pierwsza dozwolona sekcja to strona startowa). `RequireAuth` (logowanie + `/me`) i `RequireRole` tylko ukrywają UI — API sprawdza to samo niezależnie.
- **Tailwind** skanuje tylko aplikację, dlatego `index.css` ma `@source` na `packages/ui/src`.
- **Testy UI:** `src/test/renderApp.tsx` renderuje całą aplikację z routerem w pamięci i fałszywym backendem (`"METHOD /path"` → odpowiedź; brak stubu = 404). Bez MSW i bez user-event: wystarcza `fireEvent`.

### Testy
- Obowiązkowo testy jednostkowe dla: maszyny stanów zamówienia, liczenia cen (w tym pół na pół i modyfikatory), przypisania adresu do strefy, rozliczenia zmiany kierowcy
- Testy integracyjne API na prawdziwym PostgreSQL (Testcontainers), w tym test izolacji tenantów: użytkownik firmy A nie widzi danych firmy B
- Nowa funkcja = testy w tym samym zadaniu

## Zasady pracy dla Claude

- Przed dodaniem nowej zależności (NuGet/npm) zapytaj i uzasadnij.
- Nie zmieniaj struktury bazy bez migracji. Nie edytuj migracji, które już zostały zastosowane.
- Przy każdym zapytaniu do bazy sprawdź: czy filtr tenanta działa? czy użytkownik ma dostęp do tego lokalu?
- Nie ufaj danym z klienta: ceny, statusy, przypisania — wszystko weryfikowane po stronie API.
- Sekretów (klucze płatności, connection stringi) nigdy nie commituj. Lokalnie `dotnet user-secrets` / `.env` w `.gitignore`.
- Gdy decyzja z sekcji „Otwarte decyzje” blokuje pracę — zapytaj zamiast zgadywać. Do tego czasu używaj interfejsu + implementacji fake.
- Po większej zmianie zaktualizuj ten plik, jeśli zmieniła się architektura lub konwencje.

## Komendy

Wymagania: .NET SDK 10 (wersja w `backend/global.json`), Node 24, Docker. pnpm przez corepack (wersja w `frontend/package.json` → `packageManager`); bez uprawnień admina: `corepack enable --install-directory "$APPDATA/npm" pnpm`.

```bash
# baza lokalnie (konfiguracja: skopiuj .env.example → .env)
docker compose up -d
docker compose --profile tools up -d   # + pgAdmin na :5050

# backend (API na http://localhost:5080, OpenAPI: /openapi/v1.json w Development)
cd backend
# jednorazowo: sekrety lokalne (nie w repo)
dotnet user-secrets --project src/Pizzeria.Api set "ConnectionStrings:Pizzeria" "Host=localhost;Port=5432;Database=pizzeria;Username=pizzeria;Password=pizzeria"
dotnet user-secrets --project src/Pizzeria.Api set "Seed:DemoPassword" "<hasło-min-8-znaków-z-cyfrą>"
dotnet user-secrets --project src/Pizzeria.Api set "Seed:PlatformAdminEmail" "admin@pizzeria.local"
dotnet user-secrets --project src/Pizzeria.Api set "Seed:PlatformAdminPassword" "<hasło>"
dotnet build
dotnet test            # testy integracyjne wymagają działającego Dockera (Testcontainers)
dotnet run --project src/Pizzeria.Api
dotnet tool restore    # dotnet-ef jako narzędzie lokalne (backend/dotnet-tools.json)
dotnet ef migrations add <Name> --project src/Pizzeria.Infrastructure --startup-project src/Pizzeria.Api --output-dir Persistence/Migrations

# frontend (web-staff :5173, web-shop :5174; /api proxowane na backend)
cd frontend
pnpm install
pnpm --filter web-staff dev
pnpm --filter web-shop dev
pnpm typecheck
pnpm test
pnpm build
pnpm generate:api   # typy z OpenAPI → packages/api-client/src/schema.ts (API musi działać na :5080)

# agent lokalny (drukarki; w Development łączy się z http://localhost:5080, wydruki symulatora w print-output/ obok exe)
cd backend
# token z panelu: Drukowanie → Dodaj komputer (pokazywany raz)
dotnet user-secrets --project src/Pizzeria.LocalAgent set "Agent:Token" "<token>"
dotnet run --project src/Pizzeria.LocalAgent
# na komputerze w lokalu: dotnet publish, w appsettings.json obok exe Agent:ApiUrl + Agent:Token, potem np.
# sc.exe create PizzeriaAgent binPath= "C:PizzeriaPizzeria.LocalAgent.exe" start= auto
```

Konwencje szkieletu:
- Wersje pakietów NuGet centralnie w `backend/Directory.Packages.props`, wspólne ustawienia (net10.0, nullable, warnings as errors, analizatory) w `backend/Directory.Build.props`. Pakiety Microsoft/EF/Npgsql trzymamy na linii **10.x** (najnowsze 11.x wymagają net11). `Microsoft.AspNetCore.SignalR.Client` jest na 10.0.15, bo 10.0.12 ma znaną lukę (NU1903 przerywa build).
- Migracje w `src/Pizzeria.Infrastructure/Persistence/Migrations` są oznaczone w `.editorconfig` jako kod generowany (bez analizatorów).
- Endpointy API pod prefiksem `/api/...` (proxy Vite przekazuje `/api` bez przepisywania). `/health` poza prefiksem.
- Aplikacje frontendu importują i18n wyłącznie z `@pizzeria/i18n` (re-eksport `useTranslation`), klucze tłumaczeń są typowane.
- CI: `.github/workflows/ci.yml` — build + testy backendu i frontendu.

## Plan prac

### Etap 1 — pełna ścieżka zamówienia (MVP)
Cel: jedno zamówienie przechodzi całą drogę od klienta do dostarczenia.
1. ✅ Szkielet repo, docker-compose, CI (build + testy)
2. ✅ Firmy, lokale, użytkownicy, role, auth, middleware tenanta — API oraz UI: logowanie, panel firmy (lokale, pracownicy)
3. ✅ Menu firmy: kategorie, produkty, rozmiary, modyfikatory, pół na pół (prosty edytor w panelu firmy)
4. ✅ Strefy dostaw lokalu + geokodowanie (fake/prosty dostawca)
5. ✅ Sklep klienta: menu → koszyk → adres → zamówienie jako gość, płatność przy odbiorze, strona śledzenia (na razie odświeża status co 15 s; SignalR razem z kolejką zamówień w kroku 6)
6. ✅ Panel lokalu: kolejka zamówień na żywo, zamówienie telefoniczne, zmiany statusów
7. ✅ Ekran kuchni
8. ✅ Aplikacja kierowcy: lista kursów, statusy, nawigacja
9. ✅ Przydzielanie kierowcy w panelu

**Etap 1 zrobiony.** Uprawnienia do zmian statusu (`OrderPermissions`): obsługa, kierownik i admin firmy mogą wszystko. Kuchnia: „w przygotowaniu” i „gotowe”. Kierowca: „w drodze” i „dostarczone”, tylko dla swoich zamówień (inne widzi jako 404). Anulowanie wymaga powodu. Kuchnia dzwoni, gdy zamówienie zostanie przyjęte (wtedy trafia do kuchni), panel przy nowym zamówieniu, kierowca przy przypisaniu kursu.

### Etap 2
- ✅ GPS na żywo (lokal + klient), rozliczenie zmiany kierowcy. Pozycja idzie REST-em (`POST /api/driver/position`) tylko przy otwartej zmianie, a powiadomienie `DriverMoved` przez SignalR. Ślad kasuje `DriverPositionRetention` (Api/Background) po `DriverTracking:RetentionDays` (domyślnie 30). Mapa kierowców w panelu jest zwijana i odpytuje API co 15 s.
- ✅ Płatności online (na razie fake provider). Zamówienie online startuje jako `PendingOnline`, a `Accept` jest zablokowane (`order.awaiting_payment`) do potwierdzenia płatności. `Payment` to jedna próba płatności (klient może ponowić z linku śledzenia). Webhook `POST /api/payments/webhooks/{provider}` sprawdza podpis i szuka płatności poza filtrem tenanta (webhook nie ma tenanta), po czym działa już w firmie tej płatności. Jest idempotentny: zakończona próba się nie zmienia, a opłacone zamówienie zostaje opłacone. Płatności online są wyłączone, dopóki nie ustawi się `Payments:Provider` (w Development i testach `Fake`). Nie ma ich też w zamówieniach telefonicznych. Fake: strona `/pay/fake/{id}` w sklepie wysyła webhook podpisany HMAC (`/api/payments/fake/{id}/complete` istnieje tylko przy providerze Fake). Prawdziwy operator wymaga danych dostępowych per firma (szyfrowanych, np. ASP.NET Data Protection) — do zrobienia razem z wyborem operatora.
- ✅ Raporty: widok `report_orders` (migracja `AddReportViews`: dzień lokalny = `placed_at AT TIME ZONE` strefy lokalu, momenty etapów z historii statusów) i zapytania SQL w `Infrastructure/Reports/ReportQueries` — **surowe SQL nie ma filtra tenanta, każde zapytanie filtruje `company_id` jawnie**. `GET /api/reports?locationId=&from=&to=` (maks. rok): utarg (tylko `Delivered`/`PickedUp`) per dzień i forma płatności, bestsellery, średnie czasy, kierowcy. Admin firmy: dowolny lokal lub cała firma; kierownik: tylko swój lokal. Ekran „Raporty” w panelu.
- ✅ Agent lokalny + drukarka bonów (symulator). Szczegóły niżej. Drukarka fiskalna: interfejs i symulator są, prawdziwy model czeka na decyzję (otwarte decyzje).
- Następne: konta klientów

**Drukowanie — jak to jest zrobione:**
- `PrintAgent` (lokal, nazwa, hash SHA-256 tokenu urządzenia, ostatni kontakt, status drukarek) i `PrintJob` (rodzaj `KitchenTicket`/`FiscalReceipt`, status `Pending → Sent → Printed/Failed`, payload JSON w `jsonb`, liczba prób). Token pokazuje się raz przy rejestracji w panelu („Drukowanie” → „Dodaj komputer”); zgubiony = odłącz i dodaj ponownie.
- `PrintJobScheduler` (po każdej zmianie statusu w `OrderWorkflowService`) tworzy zadania wg ustawień lokalu: bon kuchenny przy `Accepted` (`PrintKitchenTickets`, domyślnie tak), paragon wg `FiscalReceiptTrigger` (`Never` domyślnie / `OnAccepted` / `OnCompleted`). Bez aktywnego agenta zadań się nie tworzy (i dodruk zwraca `printing.no_agent`), żeby nie drukować starych bonów po podłączeniu komputera.
- Uwierzytelnianie agenta: `AgentTokenAuthenticationHandler` (Api/Auth) szuka agenta po hashu **poza filtrem tenanta** (`AgentAuthenticationService`) i wystawia claimy: rola `Agent` (nie jest rolą Identity, nie da się jej nadać), `company_id`, `location_id`. Token działa tylko na `/hubs/agent`, a tokeny pracowników nie otwierają tego huba. Odłączony agent jest odcinany przy następnym wywołaniu (hub zrywa połączenie), a ponowne połączenie dostaje 401.
- `FetchJobs` oddaje `Pending` oraz `Sent` niepotwierdzone przez 2 min (agent mógł paść), oznacza je `Sent`. Dwa komputery jednego lokalu nie dostaną tego samego zadania: `xmin` jako token współbieżności, przegrany dostaje pustą listę. `Failed` można ponowić z panelu.
- Agent (`Pizzeria.LocalAgent`, Worker Service + `AddWindowsService`): `AgentWorker` trzyma połączenie (automatyczny reconnect bez limitu, pierwsze połączenie ponawiane z backoffem), pobiera zadania po `JobsAvailable`, po reconnect i co `Agent:PollInterval` (1 min). Heartbeat co 30 s, panel uznaje agenta za online przez 90 s. `PrintJobProcessor` nigdy nie rzuca: błąd drukarki lub złe dane = zadanie `Failed` z komunikatem. Drukarki za `IReceiptPrinter` / `IFiscalPrinter` (`Printers:Receipt` / `Printers:Fiscal` = `Simulator` albo `None`); symulator zapisuje wydruk tekstowy (`TicketFormatter`, 42 znaki) do `print-output/`. Kontrakt payloadów jest skopiowany w agencie (`Pizzeria.LocalAgent/Printing/PrintPayloads.cs`), bo agent nie referencjonuje projektów API — zmieniać obie strony razem. Konfiguracja leży obok exe (content root = katalog aplikacji).
- Panel: ekran „Drukowanie” (admin, kierownik, obsługa): komputery online/offline ze statusem drukarek, ostatnie 50 wydruków z ponowieniem; admin i kierownik dodają/odłączają komputery i zmieniają ustawienia druku. Na karcie zamówienia (od „przyjęte”) przycisk „Drukuj bon ponownie".
- Testy frontendu: `pnpm test` uruchamia pakiety po kolei (`--workspace-concurrency=1`), bo równoległe jsdom potrafiło skończyć się brakiem pamięci
- Płatności online (pierwszy operator), konta klientów
- Agent lokalny + drukarka bonów, potem drukarka fiskalna (pierwszy model)
- Raporty

### Etap 3
- Zestawy i promocje, kody rabatowe
- Panel PlatformAdmin, onboarding nowych firm, subskrypcje
- Język angielski, własne domeny firm, branding sklepu per firma

## Otwarte decyzje

- [ ] Nazwa produktu
- [ ] Operator płatności online (Przelewy24 / PayU / inny)
- [ ] Dostawca geokodowania adresów (Google Maps / Mapbox / Nominatim-OSM — koszt vs jakość polskich adresów)
- [ ] Hosting produkcyjny
- [ ] Dostawca kafelków mapy na produkcję (tile.openstreetmap.org nie jest do ruchu produkcyjnego; np. MapTiler, Stadia, własny serwer)
- [ ] Kierownik lokalu może zarządzać strefami swojego lokalu przez API, ale w panelu nie ma jeszcze dla niego wejścia (link jest na liście lokali, którą widzi tylko administrator firmy)
- [ ] Progi kolorów na ekranie kuchni są stałe w kodzie (10 / 20 min od przyjęcia, `KITCHEN_*_MINUTES`). Czy mają być ustawieniem lokalu?
- [ ] Zamówienie telefoniczne z dostawą trafia do lokalu wynikającego ze strefy adresu (może to być inny lokal niż ten, który odebrał telefon). Czy tak ma zostać?
- [ ] Godziny otwarcia lokali: nie zaimplementowane, zamówienia są przyjmowane zawsze
- [ ] Kierowca dołącza do grupy SignalR całego lokalu (widzi numery i statusy wszystkich zamówień lokalu, ale szczegóły tylko swoich przez API)
- [ ] Obsługiwane modele drukarek fiskalnych i zasady wystawiania paragonów (moment wydruku jest ustawieniem lokalu, domyślnie „nie drukuj”; brak NIP-u na paragonie, stawek VAT per produkt i form płatności w formacie drukarki — do ustalenia z serwisem kas)
- [ ] Kilka komputerów z agentem w jednym lokalu: dowolny agent bierze dowolne zadanie. Jeśli drukarka bonów i fiskalna będą na różnych komputerach, trzeba kierować zadania wg drukarek agenta
- [ ] Drukarka bonów: na razie tylko symulator; konkretny model/połączenie (USB, sieć, ESC/POS przez port 9100?) do ustalenia
- [ ] Czy lokal może chwilowo wyłączać dostępność pozycji z menu (założone: tak)
- [ ] Reguła ceny pół na pół (założone: cena droższej połówki, konfigurowalne — zrobione: droższa / średnia)
- [ ] Dodatki przy pizzy pół na pół: na razie tylko na całą pizzę i tylko z grup wspólnych dla obu połówek. Czy potrzebne są dodatki „na połówkę” i jak je wyceniać?
- [ ] Model rozliczeń z firmami (abonament / prowizja od zamówienia)
