## Plan: Analiza Dashboard i upravljanje pitanjima

TL;DR: Ponovni pregled je i dalje ograničen time što live login trenutno ne daje stabilan pristup Dashboardu; poslednji fetch ka `/login` vraća 503. Zato je dublja analiza zasnovana na izvornom kodu Admin UI-a, API autoring sloja i domenskog modela. Ovo je proširena verzija plana: prvo opis trenutne implementacije, zatim dublja analiza mana i rizika, pa konkretni predlozi za unapređenje po prioritetu.

**Koraci**
1. Verifikacija pristupa Dashboardu i UX-a uživo čim login ponovo proradi.
2. Pregled stvarnog toka kreiranja/izmene pitanja kroz Admin UI, helper sloj i DB persistenciju.
3. Identifikacija funkcionalnih, bezbednosnih i modelskih slabosti.
4. Prioritizacija: obavezne korekcije, zatim UX i produktivnost, pa napredne funkcionalnosti.

## **Detaljan opis funkcionalnosti i način implementacije**

### 1) Šta Dashboard trenutno pokriva
- **Lista pitanja**: [src/MathLearning.Admin/Pages/Questions/Index.razor](src/MathLearning.Admin/Pages/Questions/Index.razor) daje pretragu, filter po kategoriji, paginaciju, edit i delete akcije.
- **Kreiranje pitanja**: [src/MathLearning.Admin/Pages/Questions/New.razor](src/MathLearning.Admin/Pages/Questions/New.razor) otvara editor, učitava kategorije/podteme i čuva novo pitanje direktno kroz `AdminDbContext`.
- **Izmena pitanja**: [src/MathLearning.Admin/Pages/Questions/Edit.razor](src/MathLearning.Admin/Pages/Questions/Edit.razor) učitava postojeći entitet, mapira ga u editor model i opet snima direktno kroz `AdminDbContext`.
- **Editor pitanja**: [src/MathLearning.Admin/Components/QuestionEditor.razor](src/MathLearning.Admin/Components/QuestionEditor.razor) je glavni UI sa tabovima za tekst pitanja, opcije/odgovore, korake rešenja i objašnjenje/hintove.
- **Preview render**: [src/MathLearning.Admin/wwwroot/mathPreview.js](src/MathLearning.Admin/wwwroot/mathPreview.js) radi prikaz plain text / LaTeX / Markdown+Math sadržaja, uz KaTeX fallback i auto-render.

### 2) Kako je editor organizovan
- **Tab 1: Tekst pitanja**
  - izbor tipa pitanja (`multiple_choice` ili `open_answer`)
  - izbor kategorije i podteme
  - unos teksta pitanja
  - izbor formata sadržaja i render moda
  - unos semantic alt teksta
  - slider za težinu 1–5
  - preview pitanja i upozorenja sanitizatora
- **Tab 2: Opcije / odgovori**
  - za MCQ dozvoljava dodavanje/brisanje opcija
  - označavanje jedne opcije kao tačne
  - preview po opciji
  - za open answer postoji jedno polje za tačan odgovor
- **Tab 3: Koraci rešenja**
  - dodavanje, brisanje i pomeranje koraka gore/dole
  - svaka stavka ima tekst, format, render mod, hint, semantic alt text i flag za isticanje
  - preview i za tekst koraka i za hint
- **Tab 4: Objašnjenje i hintovi**
  - objašnjenje
  - tri odvojena hint polja: formula, clue, full
  - preview za svako polje
- **Footer**
  - save dugme sa loading stanjem
  - back/nazad
  - lista validacionih grešaka
  - `NavigationLock` sprečava slučajan odlazak bez potvrde

### 3) Implementacioni detalji koji su bitni
- `QuestionEditorModel` u [src/MathLearning.Admin/Models/QuestionEditorModel.cs](src/MathLearning.Admin/Models/QuestionEditorModel.cs) drži kompletan oblik UI stanja, uključujući opcije i korake.
- `QuestionEditorHelper` u [src/MathLearning.Admin/Models/QuestionEditorHelper.cs](src/MathLearning.Admin/Models/QuestionEditorHelper.cs) radi sanitizaciju, mapiranje u domenske objekte, snapshot za detekciju promena i izradu opcija/koraka.
- `QuestionEditorValidation` u [src/MathLearning.Admin/Models/QuestionEditorValidation.cs](src/MathLearning.Admin/Models/QuestionEditorValidation.cs) radi client-side pravila: dužine, obavezna polja, duplikati, broj opcija, tačan odgovor.
- `MathContentSanitizer` se koristi za čišćenje i za generisanje semantic alt text fallback-a.
- `Question` entitet u [src/MathLearning.Domain/Entities/Question.cs](src/MathLearning.Domain/Entities/Question.cs) i dalje čuva `CorrectAnswer` kao string, a `QuestionOption` u [src/MathLearning.Domain/Entities/QuestionOption.cs](src/MathLearning.Domain/Entities/QuestionOption.cs) nosi `IsCorrect` i `Order`.
- API authoring sloj u [src/MathLearning.Api/Endpoints/QuestionAuthoringEndpoints.cs](src/MathLearning.Api/Endpoints/QuestionAuthoringEndpoints.cs) izlaže validate, preview, save-draft, publish i revalidate tokove.
- Server validacija u [src/MathLearning.Application/Validators/QuestionAuthoringValidators.cs](src/MathLearning.Application/Validators/QuestionAuthoringValidators.cs) pokriva opšta polja i nested DTO provere, ali nema kompletnu logiku za sve type-specific MCQ pravila u samom validatoru.

## **Dublja analiza mana i rizika**

### A. Data model i konzistentnost
- **`CorrectAnswer` kao string je krhak model.** Ako se tekst opcije promeni, veza se gubi. Ako su dve opcije iste, string više ne nosi identitet odgovora.
- **MCQ logika zavisi od booleana `IsCorrect` u editoru**, a `QuestionEditorHelper.ResolveCorrectAnswer` uzima prvi označeni odgovor. Ako UI ili podaci postanu nekonzistentni, sistem može tiho snimiti pogrešan odgovor.
- **Replace pattern za opcije i korake** briše i rekreira kolekcije. To je jednostavno za implementaciju, ali loše za stabilnost identiteta, audit i eventualni diff/versioning.
- **`StepIndex` i `Order` moraju ostati konzistentni**; sada se to ručno normalizuje, ali bez jake domenske zaštite.

### B. Validacija i poslovna pravila
- **Client-side validacija je jača od server-side validacije u nekim tačkama.** `QuestionEditorValidation` proverava duplikate i MCQ pravila, dok `QuestionAuthoringRequestValidator` trenutno uglavnom proverava pojedinačna polja i nested DTO limite.
- **To znači da backend mora eksplicitno čuvati istu logiku.** Ako se neki tok zaobiđe UI-jem, pravila mogu da oslabe.
- **Open answer nije semantički validiran.** Postoji samo tekstualni unos; nema normalizacije izraza, ekvivalencije, tolerancije za numeriku, niti canonical forme.
- **Nema server-side zaštite od “skrivenih” nevalidnih kombinacija** kao što su više označenih tačnih opcija, prazne opcije koje su prošle UI manipulacijom ili duplicirani tekst sa različitim razmacima/case varijacijama.

### C. UX i tok uređivanja
- **Nema autosave-a.** Jedini mehanizam zaštite je `NavigationLock` sa potvrdom. To je korisno, ali ne spašava rad ako browser padne ili korisnik izgubi konekciju.
- **Preview je globalni toggle, ne radni režim.** To znači da korisnik mora da uključi prikaz, ali nema pravi split-screen workflow za upoređivanje source/render.
- **Nema inline signala kvaliteta sadržaja.** Sanitizer daje warnings, ali korisnik nema jasnu, vizuelno bogatu informaciju šta treba popraviti.
- **MCQ editor je funkcionalan, ali grub.** Postoji samo linearno dodavanje, brisanje i jedan “mark correct” klik; nema drag-and-drop reorder-a, nema live duplicate feedback-a i nema boljeg vizuelnog naglaska tačnog odgovora.
- **Lista pitanja je osnovna.** Ima tekstualnu pretragu i filter po kategoriji, ali nema filtera po tipu pitanja, težini, statusu, datumu izmene, autoru ili problematičnim zapisima.

### D. Bezbednost i rendering
- **Math preview zavisi od sanitizacije i escape logike.** `mathPreview.js` escape-uje plain text i za KaTeX koristi `renderToString`, ali celokupna sigurnost i dalje zavisi od toga šta ulazi u model.
- **Sadržaj se prikazuje na više mesta sa različitim formatima.** To povećava rizik od neusklađenog escaping-a ako se neki tok kasnije promeni.
- **Semantic alt text je dobar signal za pristupačnost, ali nije dosledno obavezivan na nivou svih polja.**

### E. Arhitektura i održavanje
- **Admin UI i API oba imaju authoring tokove.** To je dobro za fleksibilnost, ali loše za dupliranje pravila ako helper, validator i service nisu potpuno usklađeni.
- **Upis kroz `DbContext` direktno iz Admin stranica znači da se poslovna pravila lako raziđu od API toka.** Ako se budu uvodile nove authoring funkcije, treba paziti da ne nastane dva izvora istine.
- **Nema vidljivog version history UI-ja** za pitanja, pa je rollback ili audit trenutno slab.
- **Nema bulk operacija.** Za administratore koji rade veći broj pitanja, to će brzo postati usko grlo.

## **Šta treba da se popravi, po prioritetu**

### 1) Kritične korekcije
- **Zameniti `CorrectAnswer` stabilnim identitetom odgovora.** Za MCQ, `CorrectOptionId` ili sličan FK je mnogo sigurniji od string vrednosti.
- **Pojačati server-side validaciju za MCQ.** Pravila o jednom tačnom odgovoru, minimumu opcija i zabrani duplikata moraju biti na serveru, ne samo u UI validatoru.
- **Uskladiti admin save flow i API save flow.** Isti business rules, isti sanitizator, isti snapshot i isti kriterijumi za publish/save-draft.
- **Ojačati sanitizaciju.** Posebno za HTML/Markdown sa matematikom i semantic alt text.

### 2) Visok uticaj na UX
- **Autosave draftova** sa lokalnim recovery-jem i periodičnim slanjem na backend.
- **Split preview**: source levo, render desno, posebno za LaTeX i duga objašnjenja.
- **Bolji MCQ radni tok**: drag-and-drop, live duplicate warning, jasna ikona/boja za tačan odgovor.
- **Jasniji status sadržaja**: draft/published, poslednja izmena, ko je menjao, da li je validno.
- **Brži edit**: keyboard shortcuts za dodavanje opcija/koraka i prelazak kroz polja.

### 3) Napredne funkcionalnosti
- **Semantička provera open-answer odgovora.** Numerička tolerancija, canonicalization izraza, ekvivalencija formula.
- **Verzionisanje i audit trail.** Svaka promena pitanja da bude vidljiva kao diff.
- **Partial credit i višestruki tačni odgovori** ako je to poslovno poželjno.
- **Bulk import/export** za veći broj pitanja.
- **QA mode / Student view** da autor vidi kako pitanje izgleda učeniku pre objave.

## **Predlog konkretnih tehničkih koraka**
1. U modelu pitanja preći sa string `CorrectAnswer` na stabilni FK ili identifikator opcije.
2. Dodati server-side MCQ validaciju u [src/MathLearning.Application/Validators/QuestionAuthoringValidators.cs](src/MathLearning.Application/Validators/QuestionAuthoringValidators.cs).
3. Ujednačiti save logiku između Admin UI i API authoring servisa.
4. Dodati autosave i recovery za [src/MathLearning.Admin/Components/QuestionEditor.razor](src/MathLearning.Admin/Components/QuestionEditor.razor).
5. Preurediti preview u split layout i poboljšati warning display.
6. Dodati integracione i E2E testove za create/edit/preview/delete tok.

## **Testovi koje treba dodati ili ojačati**
- **Validator unit testovi**: dužine, duplikati, jedna tačna opcija, minimum opcija, obavezni tekstovi.
- **Service / API integracioni testovi**: create/edit/publish/save-draft za MCQ i open answer.
- **Security testovi**: HTML/Markdown/LaTeX payloadovi i rendering bez XSS.
- **UI smoke testovi**: dodavanje opcije, označavanje tačne, reorder koraka, preview toggle, navigaciona zaštita.
- **Regression test za snapshot i dirty state**: da se ne izgubi detekcija nečuvanih promena.

## **Napomena o live stanju**
- Ponovni live pregled Dashboarda nije bio moguć jer je `/login` i dalje vraćao 503 tokom fetch probe. Zbog toga je ova dopuna i dalje zasnovana na kodu, ali je preciznija nego prethodna jer uključuje stvarnu implementaciju editor komponente, validatora i save toka.

---

Ako želite, sledeći korak mogu da uradim odmah:
- A) Pretvorim ovaj plan u tehnički backlog po prioritetima i riziku.
- B) Napišem konkretnu migraciju i kod plan za prelazak sa `CorrectAnswer` na `CorrectOptionId`.
- C) Napišem proposal za novi UX layout editora pitanja, sa rasporedom polja i ponašanjem preview-a.
