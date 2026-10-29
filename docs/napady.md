# Zásobník nápadů

Co nás napadlo a na co zatím nedošlo. Vlajkový projekt je WindowSeat; nápady, které využijí jeho engine
(promítání míst, čar a ploch přes kameru), jsou jeho budoucí vrstvy. Ostatní čekají, až bude WindowSeat venku.

## Vrstvy a funkce WindowSeat

- **Webová verze (Blazor WebAssembly, PWA)**: rozhodnuto 29. 10. 2026. Odkaz pro testery a QR kódy bez instalace,
  bez iOS buildu přes Mac. Nejdřív přesunout skládání scény z MAUI do `WindowSeat.Core`.
- **Sdílení obrázku s popisky** přes systémovou nabídku telefonu (Web Share API): soukromě, nic se nenahrává,
  jemná značka s adresou v rohu.
- **Selfie s popisky toho, co je za tebou**: přední kamera, otočený směr pohledu.
- **Sjezdovky**: z OpenStreetMap, barvy podle obtížnosti, název a kam vede („je tamto A12, nebo D3?“). Zimní vrstva.
- **Turistické trasy**: stejný princip jako sjezdovky, v létě.
- **Historické pohledy z vyhlídek**: staré rytiny a pohlednice (volná díla) usazené do panoramatu; kresby původní
  podoby zřícenin na skutečném místě ve skutečné velikosti.
- **Vyhlídky jako pevné body**: přesná poloha a výška ochozu, předkalibrace na výrazné cíle.
- **Data pro celý svět po dlaždicích**: statické soubory 50 × 50 km na Azure, appka si stáhne jen okolí a uloží offline.
- **Více jazyků**: názvy a popisy z Wikidat v jazyce telefonu.
- **Zakrytí terénem**: výškový model, schovaná místa jen obrysem.
- **Kompasový pásek** dole místo šipek na okraji (šipky byly matoucí).
- **Letadlo**: dlaždice stažené podél trasy letu, kurz z GPS, offline.
- **Úspora baterie**: samostatné téma.

## Byznys (zatím jen úvahy)

- Primárně **portfolio** vývojáře; přímé tržby nejisté.
- **Affiliate**: vstupenky a prohlídky z kartičky místa, provize od prodejců zážitků; vyžaduje tisíce uživatelů.
- **Design partner**: vyhlídková loď s celoročním provozem zdarma do jara (QR na stole, jejich logo) za data a zpětnou vazbu.
- **Terasy a hotely s výhledem**: „Co vidíte z naší terasy“, pilot v sezóně (duben–květen), cena domluvená předem.
- **Zakázky v cestovním ruchu**: hrady, muzea, regiony; appka jako vizitka.
- Šíření bez obchodování: Reddit, Show HN, Product Hunt; sdílené obrázky jako pozvánka.

## Mimo WindowSeat

- **Skupinová poloha na výlet nebo na sjezdovce** (čundr, „kde je máma“): dočasná skupina bez účtů. Pozor: web neumí
  polohu na pozadí, potřebuje nativní appku a server; silná konkurence zdarma (WhatsApp, Find My, Life360).

## Název

Pracovně WindowSeat. Kandidáti: Rozhled, Lookabout, WhatsThere (domény obsazené), Okolo, Skyline Lens.
