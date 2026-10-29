# Lookabout

[![CI](https://github.com/martasIII/lookabout/actions/workflows/ci.yml/badge.svg)](https://github.com/martasIII/lookabout/actions/workflows/ci.yml)

Namiřte telefon na město nebo krajinu a uvidíte, co je co: památky, mosty, věže, vrcholy hor, Slunce a Měsíc.
Pro turisty na vyhlídce, na lodi i na procházce městem. Začínáme Prahou.

**Vyzkoušet:** [martasiii.github.io/lookabout](https://martasiii.github.io/lookabout/) na telefonu (Android i iPhone),
bez instalace. Potřebuje kameru, polohu a senzory pohybu; nic se nikam neposílá.

Nápad je starý. První verzi jsem kdysi napsal v Javě pro Android pro pohled z okénka letadla a fungovala špatně:
směr brala z kompasu, který v kovovém trupu ukazuje nesmysly. Tahle verze se o kompas jen opírá a dá se doladit
kalibrací.

> Pracovní název. Kód se zatím jmenuje `WindowSeat.*`, přejmenuje se, až bude jméno konečné.

## Jak to funguje

1. **Poloha a výška** z GPS, převedená z elipsoidu na nadmořskou.
2. **Kam míří kamera:** natáčení z gyroskopu, sever z kompasu jako pomalá kotva (rušení kovem se pozná a přečká),
   magnetická deklinace.
3. **Kalibrace**, když kompas lže: tažením prstu, zaměřením Slunce nebo Měsíce, nebo chůzí rovně (směr z GPS trati).
4. **Projekce:** z polohy, výšky a zorného úhlu kamery se spočítá, kde na obrazovce leží které místo, se zakřivením
   Země a obzorem. Ikony podle druhu, shluky se rozbalí do vějíře, popisky se nepřekrývají a přednost má známější místo.
5. **Kroužek uprostřed** zaměří místo a po chvilce ukáže kartičku s popisem, Wikipedií a navigací.
6. **Vltava a mosty** jako obrysy přes obraz: na první pohled je vidět, jestli popisky sedí.

Všechna logika je ve sdílené knihovně a pokrytá testy; appka i web jen dodávají senzory a kreslí.

## Struktura

| Projekt | Co obsahuje |
|---|---|
| `src/WindowSeat.Core` | Čistá knihovna: orientace kamery, spojení gyroskopu s kompasem, kalibrace, geodézie, projekce, rozložení popisků, efemeridy Slunce a Měsíce, skládání scény. |
| `src/WindowSeat.Web` | Webová verze (Blazor WebAssembly, PWA): kamera, senzory a kreslení v JavaScriptu, scéna z knihovny. Data míst v `wwwroot/data`. |
| `src/WindowSeat.App` | .NET MAUI appka pro Android: přesné parametry objektivu, nahrávání jízd, snímky obrazovky. |
| `tests/WindowSeat.Core.Tests` | Unit testy knihovny (xUnit). |
| `tools/WindowSeat.DataBuilder` | Stažení dat: místa z Wikidat, výška terénu, plochy řeky a mostů z OpenStreetMap. |

## Spuštění

```bash
dotnet test
dotnet run --project src/WindowSeat.Web --urls http://localhost:5300
dotnet build src/WindowSeat.App -f net10.0-android -t:Run
```

Senzory a kamera chtějí https nebo `localhost`. Telefon na kabelu: `adb reverse tcp:5300 tcp:5300` a v Chromu
`http://localhost:5300`. Web se při každém pushi do `main` publikuje na GitHub Pages.

## Stav

- [x] Geodézie a projekce (WGS84, zakřivení Země, obzor), popisky bez překrývání podle popularity
- [x] Směr z gyroskopu, kompas jen jako pomalá kotva; deklinace; nadmořská výška z GPS
- [x] Kalibrace tažením, na Slunce a Měsíc, chůzí
- [x] Ikony podle druhu, vějíř pro shluky, kroužek se zaměřením a kartičkou místa, zoom dvěma prsty
- [x] Vltava a 42 mostů jako obrysy
- [x] Offline katalog Prahy a okolí z Wikidat, výška terénu z EU-DEM
- [x] Webová verze pro Android i iPhone bez instalace
- [ ] Na webu dohnat appku: zoom, kalibrace na Měsíc a chůzí, nastavení, přesná deklinace a výška terénu
- [ ] Vyhlídky jako pevné body (výška ochozu)
- [ ] Zakrytí terénem a budovami
- [ ] Další oblasti a stahování dat podle polohy, více jazyků

Další nápady v [docs/napady.md](docs/napady.md).

## Data

- Místa: [Wikidata](https://www.wikidata.org) (CC0), popularita podle počtu článků na Wikipedii.
- Výška terénu: EU-DEM v1.1 (Copernicus) přes [OpenTopoData](https://www.opentopodata.org).
- Vltava a mosty: © přispěvatelé [OpenStreetMap](https://www.openstreetmap.org/copyright), licence ODbL.

```bash
dotnet run --project tools/WindowSeat.DataBuilder -- Praha 50.0880 14.4208 15 src/WindowSeat.Web/wwwroot/data/praha.json
dotnet run --project tools/WindowSeat.DataBuilder -- water src/WindowSeat.Web/wwwroot/data/vltava.json
dotnet run --project tools/WindowSeat.DataBuilder -- bridges src/WindowSeat.Web/wwwroot/data/mosty.json
```
