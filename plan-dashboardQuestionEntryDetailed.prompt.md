## Queue: pitanja backend prvo, admin UI poslednji

Kontekst: stari plan je delom zastareo. U medjuvremenu su uradjeni `CorrectOptionId`, shared authoring pipeline, server-side MCQ validacija, draft/publish/revalidate tokovi, validation history, versioning i veci deo planiranog Admin UI rada oko preview-a, autosave-a i filtera. Zbog toga queue ispod ostavlja samo ono sto jos ima smisla, poredjano po prioritetu, sa backend fokusom.

## Izbaceno iz queue-a kao zavrseno ili vise nije prioritet
- `CorrectAnswer` -> `CorrectOptionId` migracija i domen/schema pokrivenost su vec prisutni.
- Shared save flow izmedju Admin UI i API authoring servisa je vec uveden.
- Server-side MCQ validacija za jednu tacnu opciju, minimalan broj opcija i invalid `CorrectOptionId` je vec pokrivena.
- Verzionisanje, validation history i revalidate tok vec postoje.
- Autosave, split preview, student view, quality snapshot, latest validation panel i veci deo authoring UX poboljsanja su vec uradjeni.
- Prosireni filteri liste pitanja u Admin UI-ju vise nisu backend prioritet i ne treba da blokiraju dalje backend radove.

## Backend queue po prioritetu

### P1 Kriticno
1. Ojacati sanitization i rendering security granice u authoring pipeline-u.
   - Pregledati sve ulaze koji prolaze kroz math/markdown/html segmente.
   - Dodati ciljane testove za XSS, malformed LaTeX i edge-case payloadove.
   - Zatvoriti razliku izmedju "valid math" i "safe for render".
2. Dovrsiti end-to-end backend pokrivenost authoring toka.
   - Integracioni testovi za create/update/save-draft/publish/revalidate kroz API.
   - Negativni testovi za concurrency i duplicate version allocation scenarije.
   - Provera da validation snapshot, published version i persisted question ostaju konzistentni.
3. Ukloniti preostale legacy oslonce na `CorrectAnswer` tamo gde MCQ i dalje cita tekstualni fallback bez stvarne potrebe.
   - Zadrzati fallback samo gde je potreban zbog kompatibilnosti sa starim zapisima.
   - Dokumentovati tacno koje putanje su legacy i kada mogu da se ugase.

### P2 Visok prioritet
4. Formalizovati backend contract za validation/preview rezultat.
   - Stabilizovati DTO shape i eksplicitno pokriti warning/error/stage semantiku.
   - Dodati regresione testove za equivalence checks i validation issue ordering.
5. Ojacati observability authoring pipeline-a.
   - Jasniji log/metric signal za validation failure, publish failure i concurrency retry.
   - Brza dijagnostika kada draft/version alokacija udje u konflikt.
6. Pregledati replace-strategy za opcije i korake sa backend strane.
   - Ako identitet kolekcija i audit diff postaju bitni, planirati prelaz sa "replace all" na stabilniji update model.
   - Ako nije bitno u ovoj fazi, eksplicitno oznaciti kao odlozeno da ne ostane mutan zahtev.

### P3 Srednji prioritet
7. Dodatno ucvrstiti semantic/equivalence proveru za open-answer tok.
   - Prosiriti test pokrivenost za canonicalization, toleranciju i validation fallback ponasanje.
   - Razdvojiti "validation error" od "not equivalent" ishoda u contract testovima.
8. Pripremiti backlog proposal za bulk import/export tek kada backend authoring contract bude stabilan.
   - Trenutno nije za implementaciju pre zatvaranja P1/P2 stavki.

## Admin UI poslednje
9. Preostale Admin UI sitnice ostaju na kraju reda.
   - Doterivanje helper textova, dodatnih filter detalja i slicnih UX finisa.
   - Nove vizuelne dorade editor layout-a samo ako podrzavaju vec zavrsene backend tokove.
   - Nista iz Admin UI-ja ne treba da ide ispred backend security, contract i integration test rada.

## Sledeci preporuceni prompt iz ovog queue-a
- "Ojacaj authoring sanitization boundary i dodaj backend testove za XSS/malformed LaTeX payloadove kroz validation/preview/save-draft tok."
