# Beslutninger

## East/west security i WorkspaceApi (#72)

### Internt endpoint til medlemskab
Når nogen vil poste en besked, skal Message vide, om brugeren er medlem af kanalen. Det er Workspace, der ejer
medlemskab, så Message spørger Workspace via `GET internal/channels/{channelId}/members/{userId}`.

Endpointet er kun for andre services. Klienter kan ikke kalde det, fordi det kan svare for en hvilken som helst
bruger og ikke kun for den, der kalder.

Vi kunne i stedet have kopieret medlemskab over i Message via events. Det har vi fravalgt, fordi så skulle Message
holde en kopi ved lige. Ulempen ved vores løsning er, at man ikke kan poste, hvis WorkspaceApi er nede.

### Roller i tokens
Et token har enten `role: user` (en klient) eller `role: internal-service` (en anden service), som opgaven beder om.
De offentlige endpoints kræver `user`. Det betyder, at et token med en ukendt rolle bliver afvist automatisk.

### WorkspaceApi tjekker selv tokens
Selvom gatewayen allerede tjekker tokenet, gør WorkspaceApi det også (signatur, issuer, audience og exp). Ellers
kunne man kalde WorkspaceApi direkte inde i compose network og springe tjekket over. Brugeren læser vi fra
`sub` i tokenet og ikke fra `X-User-Id`-headeren, fordi headeren kan sættes af hvem som helst.

### Limits
Alle services, der tjekker tokens, har signing key. Hvis én af dem bliver kompromitteret, kan den lave gyldige
tokens. Faucet'en udsteder tokens med en hvilken som helst rolle, så den må kun køre i development.

## Samtidige ændringer af et workspace (#68)

Et workspace bliver gemt ved at loade hele aggregatet, ændre det og gemme det igen. Gemmer to requests det samme
workspace på samme tid, vinder den sidste, og den første ændring forsvinder, fx en kanal.

Vi har valgt optimistisk låsning. Hvert workspace har en `version`, og man kan kun gemme, hvis versionen i databasen
stadig er den, man loadede. Så bliver den talt én op. Den request, der kommer for sent, får 409
(`Workspace.ConcurrentChange`) og kan prøve igen.

Pessimistisk låsning (`select ... for update`) ville holde en lås i databasen, mens requesten kører, og alle andre
ville vente. Med vores timeout på 2 sekunder (#65) ville det give timeouts i stedet for et tydeligt svar. Konflikter er
sjældne, så det er billigere at lade den ene prøve igen.

Kvoten (#78) bliver ikke låst på den her måde. Den ændres med sin egen atomiske SQL-update og rører ikke `version`, så
en upload og en omdøbning af workspacet ikke giver konflikt med hinanden.

## Idempotent oprettelse af workspaces (#67)

Bliver `POST api/v1/workspaces` sendt igen, fx fordi klienten ikke fik svaret, bliver der oprettet to workspaces.
Navnet er ikke unikt, så det kan databasen ikke opdage alene.

Klienten kan derfor sende en `Idempotency-Key`. Nøglen gemmes i samme transaktion som workspacet, med en unik
nøgle på (bruger, nøgle). Kommer den samme nøgle igen, får klienten det første workspace tilbage, og der oprettes ikke
noget nyt. Kommer to requests med samme nøgle på samme tid, venter den ene på den andens commit og får så det samme
workspace.

Nøglen gælder pr. bruger, så to brugere ikke kan ramme hinandens nøgler. Headeren er valgfri, så eksisterende
klienter, Postman-testene og GraphQL virker som før. En retry med samme nøgle men et andet navn får også det første
workspace. Vi tjekker ikke, om indholdet er det samme.

## Outbox til ChannelActivityUpdatedEvent (#66)

Før blev `last_activity_at` gemt og eventet publiceret bagefter. Var RabbitMQ nede lige der, forsvandt eventet, og et
retry hjalp ikke, fordi opdateringen så allerede var sket, og vagten (`last_activity_at < @postedAt`) sprang den over.

Nu bliver eventet gemt i tabellen `outbox` i samme transaktion som opdateringen. Enten sker begge dele, eller ingen af
dem. En baggrundsjob (`OutboxPublisher`) kigger hvert sekund efter usendte events, publicerer dem og markerer dem som
sendt, først når RabbitMQ har bekræftet dem (publisher confirms). Sendte events bliver slettet efter en time.

Ulempen er, at et event kan blive sendt mere end én gang, fx hvis WorkspaceApi stopper efter RabbitMQ har bekræftet,
men før rækken er markeret som sendt. Derfor har eventet et `EventId`, som modtagerne kan bruge til at frasortere
dubletter. Rækkerne bliver låst med `for update skip locked`, så to instanser af WorkspaceApi ikke sender de samme.

## Saga: upload af en fil, der er for stor til kvoten

En saga er en proces, der går på tværs af flere services, hvor hver service laver sin egen del i sin egen database.
Der er ingen fælles transaktion, så hvis et senere trin fejler, skal de tidligere trin kompenseres.

### Flowet
1. File gemmer uploaden med status `Pending`.
2. File beder Workspace om at reservere plads (`ReserveQuota`). Er der ikke plads, svarer Workspace
   `QuotaExceededEvent`, og uploaden bliver `Rejected`.
3. File beder Message om at poste beskeden (`PostMessage`). Fejler det, frigiver Workspace pladsen igen, og uploaden
   bliver `Rejected`.
4. Når beskeden er postet, bliver uploaden `Confirmed`.

Kvoten bliver tjekket, før beskeden postes. Det er med vilje, så ser ingen andre brugere nogensinde en fil, der
bliver afvist, og vi slipper for at skulle "fjerne" en besked, folk allerede har læst.

### Kvoten ligger i Workspace
Kvoten gælder for hele workspacet. Derfor ejer Workspace den, og det er
Workspace, der kan sige nej.

Reservationen er én SQL-update, der kun lykkes hvis `used + size <= limit`. Vi loader altså ikke hele workspacet, ændrer
det og gemmer det igen. Gjorde vi det, kunne to uploads på samme tid begge se, at der var plads, og begge blive godkendt
(samme problem som #68).

### File styrer sagaen (orchestration)
Der er to måder at lave en saga på. Med orchestration er der én service, der styrer flowet og fortæller de andre, hvad
de skal gøre. Med choreography reagerer hver service bare på det forrige event, og der er ingen, der styrer.

Vi har valgt orchestration, og det er File, der styrer. File ejer uploaden, og den gemmer også beskeden (kanal, tekst,
afsender), indtil den skal postes. Så skal Workspace kun kende `SagaId`, `WorkspaceId` og størrelsen. Med choreography
skulle beskedens indhold sendes igennem Workspace, selvom Workspace ikke har noget med den at gøre.

Det betyder, at File bliver en rigtig service med egen database, HTTP-endpoint, route i gatewayen og JWT-tjek. Den
bygges på samme måde som WorkspaceApi. Message er bare en stub, altså en lille service, der kun svarer på beskeder.

### File gemmer kun metadata
File gemmer filnavn, størrelse og status (`Pending`, `Confirmed` eller `Rejected`), men ikke selve filen. Det gør
opgaven meget mindre, og selve sagaen er den samme.

Ulempen er, at det er klienten, der fortæller, hvor stor filen er. I virkeligheden ved man det først, når filen er
modtaget. Scenariet handler om, at kvoten bliver overskredet midt i en upload, og det kan vi ikke vise. Det er en
bevidst forenkling.

### API'et
En upload svarer `202 Accepted` med et upload-id. 202 betyder "vi har modtaget det, men vi er ikke færdige endnu".
Klienten kan så spørge med `GET`, hvordan det går. Upload-id'et er det samme som `SagaId`. Kun den bruger, der
uploadede filen (`sub` i tokenet), må se status.

### Navngivning af beskeder
Commands er en besked om, at nogen skal gøre noget. De hedder noget i bydeform uden endelse, fx `ReserveQuota`, og
ligger i `Bizcord.Contracts/Commands`.

Events er en besked om, at noget er sket. De har endelsen `Event`, fx `QuotaExceededEvent`, og ligger i
`Bizcord.Contracts/Events`.

### Message svarer med sit eget event
Når Message har postet beskeden, sender den to events. `PostMessageSucceededEvent` er svaret til File og har
`SagaId`. `MessagePostedEvent` er det samme event som i dag, og det fortæller alle andre, at der er kommet en besked.

Vi kunne have tilføjet et valgfrit `SagaId` til `MessagePostedEvent` i stedet. Det har vi fravalgt, fordi eventet så
ville betyde to ting: "der er postet en besked" og "din command lykkedes". Message ved heller ikke, hvad en upload er,
så svaret hedder noget med `PostMessage` og ikke noget med upload.

Ulempen er, at de to events ikke sendes i samme transaktion. Går stubben ned mellem dem, kan File have fået sit svar,
uden at `channel_activity` bliver opdateret. Det accepterer vi i en stub. En rigtig Message-service ville bruge en
outbox (som #66).

### Hvis en service aldrig svarer
Et baggrundsjob i File finder sagaer, der har stået stille for længe.

`PostMessage` er sagaens pivot, altså punktet hvor der ikke er nogen vej tilbage. Når først beskeden kan være postet,
giver det ikke mening at rulle tilbage. Derfor:

- **Før pivot** ruller jobbet sagaen tilbage. Pladsen frigives, og uploaden bliver `Rejected`.
- **Efter pivot** sender jobbet `PostMessage` igen. Message skal derfor kunne tåle at få samme besked to gange
  (idempotent på `SagaId`), så beskeden ikke bliver postet to gange.

Svar, der kommer, efter sagaen allerede er `Confirmed` eller `Rejected`, bliver ignoreret.
