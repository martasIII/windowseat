# WindowSeat

[![CI](https://github.com/martasIII/windowseat/actions/workflows/ci.yml/badge.svg)](https://github.com/martasIII/windowseat/actions/workflows/ci.yml)

Mobilní appka, která přes kameru ukáže, co máte před sebou: památky, mosty, vrcholy hor, města. Pro turisty na kopci, na rozhledně i ve městě, a jako bonus i z okénka letadla. Funguje **offline**.

Nápad je starý. První verzi jsem kdysi napsal v Javě pro Android a fungovala špatně: směr brala z kompasu, který v kovovém trupu letadla ukazuje nesmysly. Tahle verze bere v letadle kurz z GPS trati a kompas používá jen na zemi.

## Jak to funguje

1. Poloha a výška z GPS (na zemi lze zadat ručně).
2. Kam míří kamera: náklon a otáčení z gyroskopu, sever z kompasu (pěšky) nebo z kurzu GPS (vozidlo), doladění kalibrací.
3. Projekce: z polohy, výšky a zorného úhlu kamery se spočítá, kde na obrazovce leží které místo, a přes náhled se vykreslí popisek.
4. Data: offline katalog míst z Wikidat s popularitou podle počtu článků na Wikipedii.

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

- [x] Náhled kamery a živé hodnoty senzorů
- [x] Geodézie a projekce (WGS84, zakřivení Země, obzor), popisky bez překrývání podle popularity
- [x] Směr z gyroskopu, kompas jen jako pomalá kotva; deklinace; výška GPS převedená na nadmořskou
- [x] Kalibrace klepnutím na známý cíl, Slunce nebo Měsíc
- [x] Offline katalog Prahy z Wikidat (`tools/WindowSeat.DataBuilder`), výška terénu z EU-DEM
- [x] Zorný úhel z parametrů objektivu, náhled přes celou obrazovku
- [x] Kurz z GPS u bočního okna (auto, letadlo)
- [ ] Zakrytí terénem a budovami
- [ ] Další oblasti a stahování dat podle polohy
- [ ] Volný směr ve vozidle (gyroskop + kalibrace)
- [ ] iPhone

## Data

Místa: [Wikidata](https://www.wikidata.org) (CC0). Výška terénu: EU-DEM v1.1 (Copernicus) přes [OpenTopoData](https://www.opentopodata.org).

```bash
dotnet run --project tools/WindowSeat.DataBuilder -- Praha 50.0880 14.4208 15 src/WindowSeat.App/Resources/Raw/places/praha.json
```
