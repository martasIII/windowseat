# WindowSeat

Mobilní appka, která přes kameru z okénka letadla ukáže, nad čím právě letíte: města, vrcholy hor, řeky. Funguje **offline** na Androidu i iPhonu.

Nápad je starý. První verzi jsem kdysi napsal v Javě pro Android a fungovala špatně: směr brala z kompasu, který v kovovém trupu letadla ukazuje nesmysly. Tahle verze bere v letadle kurz z GPS trati a kompas používá jen na zemi.

## Jak to funguje

1. Poloha a výška z GPS (na zemi lze zadat ručně a ladit tak „let“ z okna doma).
2. Kam míří kamera: náklon z rotačního senzoru telefonu, směr z kompasu (na zemi) nebo z kurzu letu (v letadle).
3. Projekce: z polohy, výšky a zorného úhlu kamery se spočítá, kde na obrazovce leží které místo, a přes náhled se vykreslí popisek.
4. Data: offline databáze měst a vrcholů (GeoNames), stažená předem.

## Struktura

| Projekt | Co obsahuje |
|---|---|
| `src/WindowSeat.Core` | Čistá knihovna bez MAUI: orientace kamery, geodézie, projekce. Tady se rozhoduje, jestli popisky sedí. |
| `src/WindowSeat.App` | .NET MAUI appka (Android, iOS): kamera, senzory, vykreslení. |
| `tests/WindowSeat.Core.Tests` | Unit testy knihovny (xUnit). |

## Spuštění

```bash
dotnet test
dotnet build src/WindowSeat.App -f net10.0-android -t:Run
```

Android se staví na Windows i Linuxu. iOS jen na macOS.

## Stav

- [x] Krok 1: náhled kamery a živé hodnoty senzorů (směr, výška, náklon, kompas, GPS)
- [ ] Krok 2: projekce testovací sady míst, ruční poloha a výška
- [ ] Krok 3: offline data z GeoNames
- [ ] Krok 4: režim letadla (kurz z GPS trati)
- [ ] Krok 5: iPhone
