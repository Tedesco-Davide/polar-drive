# PolarDrive 🚗❄️

Repository per il progetto **PolarDrive**.

---

### 🟠 CLOUDFARED

- LE CONFIGURAZIONI PRINCIPALI SONO SOTTO => C:\Users\Tedesco Davide\.cloudflared
- SE cloudflared tunnel NON SI AVVIA IN MODO AUTOMATICO => CHIAMATE VERSO POLARDRIVE-API RESTITUISCONO ERRORE 502
- PROBLEMI DI TUNNEL ( CLAUDFARED NON PRENDE LA CONFIGURAZIONE config.yml PRESENTE SOTTO .cloudflared )=>
    KILLARE SERVIZIO CLOUDFLARED ( WIN+R => SERVICES.MSC )
    DISINSTALLARE SERVIZIO CLOUDFLARED
    INSTALLARE SERVIZIO CLOUDFLARED
    WIN+R => REGEDIT => HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Cloudflared => ImagePath =>
    CAMBIARE DA 
    "C:\Program Files (x86)\cloudflared\cloudflared.exe" 
    A 
    "C:\Program Files (x86)\cloudflared\cloudflared.exe" tunnel run d439ffff-a779-461e-8600-f547ca1e19f2"
    STARTARE IL SERVIZIO CON Start-Service cloudflared

### 📞 VONAGE

- CELLULARE PRINCIPALE Vonage__PhoneNumber => Dentro .env.dev ed .env.prod
- CHIAMATE DA TESTARE FULL ONLINE => Dentro SMS.http 
- CHIAMATE DA TESTARE FULL ONLINE => 
    @numero1 e @numero2 sono uguali ma paghi x2 il costo SMS
    se @numero1 e @numero2 sono diversi => Mittente non autorizzato
    siccome @numero1 e @numero2 sono uguali, ricevono SMS di conferma, e quindi anche => Comando non riconosciuto

### ⚙️ GENERICHE COMUNI

PER "RIAVVIARE" APPLICATIVO =>
- docker compose -f docker-compose.dev.yml restart api (DEVELOPMENT)
- docker compose -f docker-compose.prod.yml restart api (PRODUCTION)

CONFIG PARAMETRI OPERATIVI PRINCIPALI APPLICATIVO (MODIFICABILE SENZA RIAVVIO) =>
- ROOT PRINCIPALE POLARDRIVE => app-config.json
- Per capire cosa fanno i paramentri dentro app-config.json, sono presenti i commenti qui =>
- polar-drive\backend\PolarDrive.Data\Constants\AppConfig.cs ( implementazione a codice dei parametri )

CONFIG PARAMETRI PER I DATI VEICOLI (MODIFICABILE CON RIAVVIO) =>
- ROOT PRINCIPALE POLARDRIVE => vehicle-options.json
- Per capire cosa fanno i paramentri dentro vehicle-options.json, sono presenti i commenti qui =>
- polar-drive\backend\PolarDrive.Data\Constants\VehicleConstants.cs ( implementazione a codice dei parametri )

CONFIG PARAMETRI COMPILE-TIME (MODIFICABILE CON RIAVVIO) =>
- backend/PolarDrive.WebApi/Constants/CommonConstants.cs

- DECOMMENTARE PER STAMPARE VELOCEMENTE => 
var insights = "TEST_INSIGHTS_NO_AI";

- COMMENTARE PER NON INVIARE DATI A GOOGLE => 
await googleAds.SendAiInsightsToGoogleAds(insights, vehicleId, vehicle.Vin);

- CONTIENE FORMATTING PER EVENTUALI SBAGLI STAMPA AI => 
private static string FormatInsightsForHtml(string insights)

### 🐳 DOCKER DEV - PRINCIPALI

_(Tasto destro sulla cartella ROOT principale del progetto → Open in integrated Terminal)_

=> FULL DOWN ED UP

- RIMUOVERE ALL CONTAINER DEV => docker compose -f docker-compose.dev.yml down
- REBUILD ALL IMMAGINI DEV => docker build -f backend/PolarDrive.TeslaMockApiService/Dockerfile -t polardrive-mock:latest .
docker build -f backend/PolarDrive.WebApi/Dockerfile -t polardrive-api:latest .
docker build -f frontend/Dockerfile -t polardrive-frontend:latest .
- START ALL CONTAINER DEV => docker compose -f docker-compose.dev.yml --env-file .env.dev up -d

### 🐳 DOCKER DEV - SECONDARI

_(Tasto destro sulla cartella ROOT principale del progetto → Open in integrated Terminal)_

=> OLLAMA

RIAVVIO VELOCE =>

- STOPPARE CONTAINER OLLAMA => docker compose -f docker-compose.ollama.dev.yml stop
- AVVIO OLLAMA => docker compose -f docker-compose.ollama.dev.yml up -d ollama
- VERIFICA STATO => docker compose -f docker-compose.ollama.dev.yml ps
- VERIFICA HEALTH => docker inspect polardrive-ollama-dev --format='{{.State.Health.Status}}'
- LISTA MODELLI => curl http://localhost:11434/api/tags

COMANDI LENTI IMPATTANTI =>

- RIMUOVERE CONTAINER OLLAMA (DA NON FARE MAI) => docker compose -f docker-compose.ollama.dev.yml down
- PULL MODELLO ANCHE SE GIÀ SCARICATO => docker compose -f docker-compose.ollama.dev.yml run --rm ollama-init
- SCARICARE MODELLO => ESEMPIO: docker exec -it polardrive-ollama-dev ollama pull qwen2.5:14b

!!!UNA TANTUM!!! => LAUNCH INIT DB PER RESETTARE IL DB DataPolar_PolarDrive_DB_DEV =>

- REBUILD IMMAGINE INITDB => docker build -f backend/PolarDriveInitDB.Cli/Dockerfile -t polardrive-initdb:latest .
- AZIONE INIT DB => docker compose -f docker-compose.dev.yml --env-file .env.dev run --rm initdb
- RIMUOVERE IMMAGINE INITDB => docker rmi -f polardrive-initdb:latest

=> REBUILD TESLA-MOCK-API-SERVICE POST MODIFICHE =>

- RIMUOVERE CONTAINER DEV => docker rm -f polardrive-mock-api-dev
- REBUILD IMMAGINE TESLA MOCK => docker build -f backend/PolarDrive.TeslaMockApiService/Dockerfile -t polardrive-mock:latest .
- START CONTAINER DEV => docker compose -f docker-compose.dev.yml --env-file .env.dev up -d mock-api
- LOGS => docker compose -f docker-compose.dev.yml logs -f mock

=> REBUILD POLARDRIVE-WEB-API POST MODIFICHE =>

- RIMUOVERE CONTAINER DEV => docker rm -f polardrive-api-dev
- REBUILD IMMAGINE WEB-API => docker build -f backend/PolarDrive.WebApi/Dockerfile -t polardrive-api:latest .
- START CONTAINER DEV => docker compose -f docker-compose.dev.yml --env-file .env.dev up -d api
- LOGS => docker compose -f docker-compose.dev.yml logs -f api

=> REBUILD FRONTEND POST MODIFICHE =>

- RIMUOVERE CONTAINER DEV => docker rm -f polardrive-frontend-dev
- REBUILD IMMAGINE DEV FRONTEND => docker build -f frontend/Dockerfile -t polardrive-frontend:latest .
- START CONTAINER DEV => docker compose -f docker-compose.dev.yml --env-file .env.dev up -d frontend
- LOGS => docker compose -f docker-compose.dev.yml logs -f frontend

### 🐳 DOCKER PROD

_(Tasto destro sulla cartella ROOT principale del progetto → Open in integrated Terminal)_

PROD => DA SCRIVERE DOPO CHE DEV FUNZIONA PERFETTAMENTE. FARE CHECK COMPARE CON DEV DI TUTTI I FILES DOCKER, PER ADATTARLI A PROD

### 🔷 FRONTEND POLARDRIVE ADMIN

_(Tasto destro sulla cartella `frontend` → Open in integrated Terminal)_

#### **🚀 SVILUPPO (Development)**

- `npm i` → Installa/reinstalla tutti i pacchetti
- `npm run dev` → Avvio in modalità sviluppo (hot reload, debug attivo)
- `npm list` → Visualizza tutti i pacchetti installati

### 🔶 BACKEND PolarAi

_(Tasto destro sulla cartella `backend` → Open in integrated Terminal)_

#### **🚀 SVILUPPO (Development)**

- `dotnet build` → Rebuild completo della soluzione

    - `$env:ASPNETCORE_ENVIRONMENT="Development"` → IMPORTANTISSIMO → Set di ambiente DEV

    - `dotnet run --project PolarDriveInitDB.Cli` → Crea un nuovo DB (mock, cancellabile, per tesing) da ZERO in Micrisoft SQL Server

    - `dotnet run --project PolarDrive.WebApi` → Avvia la WebAPI principale in modalità sviluppo

    > Espone l'endpoint `http://localhost:3000/admin` per Dashboard Backend

- Extra comandi da lancaire nel caso di problemi di connessione al backend

    > dotnet dev-certs https --clean

    > dotnet dev-certs https --trust
    
    > dotnet dev-certs https --check

---

#### **📦 PRODUZIONE (Production)**

- `$env:ASPNETCORE_ENVIRONMENT="Production"` → IMPORTANTISSIMO → Set di ambiente PROD
- `dotnet run --project PolarDriveInitDB.Cli` → Crea un nuovo DB da zero ( di PRODUZIONE ) da ZERO in Micrisoft SQL Server
- → IMPORTANTISSIMO → MAI eseguire questo comando se NON per RESETTARE IL DB DI PRODUZIONE

---

### 🧪 TEST AUTOMATICO API

_(Tasto destro sulla cartella `frontend` → Open in integrated Terminal)_

- `node converter.js` → Lanciare questo comando ogni volta che si aggiorna il file converter.js → Genera collection Postman ottimizzata con parametri e body corretti
- `newman run polardrive-collection-fixed.json --insecure` → Testa tutti gli endpoint automaticamente

---

### 🚗 TESLA MOCK API SERVICE

_(Tasto destro sulla cartella `backend/PolarDrive.TeslaMockApiService` → Open in integrated Terminal)_

- `dotnet run` → Avvia il servizio di push dati verso la WebAPI
  > Simula i dati Tesla e li invia automaticamente alla WebAPI principale per test e sviluppo.

---

#### ✅ Ollama

_(Tasto destro sulla cartella `backend` → Open in integrated Terminal)_

- `ollama --version` → Mostra la versione del runtime **Ollama**

  > Ollama è il **motore AI locale**: scarica, avvia e gestisce modelli LLM direttamente sul tuo PC.

- `ollama serve` → Avvia **Ollama in modalità server REST**

  > Espone l'endpoint `http://localhost:11434/api/generate` per richieste AI programmatiche.  
  > ✔️ È **stateless** e perfettamente integrabile nel backend (`HttpClient`, `POST`, JSON).

##### 🔍 Come verificare se è attivo

- Apri il browser e visita:  
  `http://localhost:11434`
