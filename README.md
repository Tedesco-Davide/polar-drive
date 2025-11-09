# PolarDrive 🚗❄️

Repository per il progetto **PolarDrive**.

---

### ⚙️ GENERICHE COMUNI

- backend/PolarDrive.WebApi/Constants/CommonConstants.cs => CONTIENE CONFIG PRINCIPALI APPLICATIVO => ESEMPIO: MONTHLY_HOURS_THRESHOLD
- DECOMMENTARE PER STAMPARE VELOCEMENTE => var insights = "TEST_INSIGHTS_NO_AI";
- COMMENTARE PER STAMPARE REASONING NEI PDF => aiInsights = System.Text.RegularExpressions.Regex.Replace

### 🐳 DOCKER DEV

_(Tasto destro sulla cartella ROOT principale del progetto → Open in integrated Terminal)_

DEV => INFO SU OLLAMA

- RIMUOVERE CONTAINER OLLAMA (DA NON FARE MAI) => docker compose -f docker-compose.dev.gpu.yml down
- PULL MODELLO ANCHE SE GIÀ SCARICATO => docker compose -f docker-compose.dev.gpu.yml run --rm ollama-init

- STOPPARE CONTAINER OLLAMA => docker compose -f docker-compose.dev.gpu.yml stop
- AVVIO OLLAMA => docker compose -f docker-compose.dev.gpu.yml up -d ollama
- VERIFICA STATO => docker compose -f docker-compose.dev.gpu.yml ps
- VERIFICA HEALTH => docker inspect polardrive-ollama-dev --format='{{.State.Health.Status}}'
- LISTA MODELLI => curl http://localhost:11434/api/tags
- SCARICARE MODELLO => ESEMPIO: docker exec -it polardrive-ollama-dev ollama pull deepseek-r1:8b

DEV => COMANDI GENERICI => FULL DOWN ED UP

- RIMUOVERE ALL CONTAINER DEV => docker compose -f docker-compose.dev.yml down
- REBUILD ALL IMMAGINI DEV => docker build -f backend/PolarDrive.TeslaMockApiService/Dockerfile -t polardrive-mock:latest .;
docker build -f backend/PolarDrive.WebApi/Dockerfile -t polardrive-api:latest .;
docker build -f frontend/Dockerfile -t polardrive-frontend:latest .
- START ALL CONTAINER DEV => docker compose -f docker-compose.dev.yml --env-file .env.dev up -d

DEV => !!!UNA TANTUM!!! => LAUNCH INIT DB PER RESETTARE IL DB DataPolar_PolarDrive_DB_DEV =>

- REBUILD IMMAGINE INITDB => docker build -f backend/PolarDriveInitDB.Cli/Dockerfile -t polardrive-initdb:latest .
- AZIONE INIT DB => docker compose -f docker-compose.dev.yml --env-file .env.dev run --rm initdb
- RIMUOVERE IMMAGINE INITDB => docker rmi -f polardrive-initdb:latest

DEV => REBUILD TESLA-MOCK-API-SERVICE POST MODIFICHE =>

- RIMUOVERE CONTAINER DEV => docker rm -f polardrive-mock-api-dev
- REBUILD IMMAGINE TESLA MOCK => docker build -f backend/PolarDrive.TeslaMockApiService/Dockerfile -t polardrive-mock:latest .
- START CONTAINER DEV => docker compose -f docker-compose.dev.yml --env-file .env.dev up -d mock-api
- LOGS => docker compose -f docker-compose.dev.yml logs -f mock

DEV => REBUILD POLARDRIVE-WEB-API POST MODIFICHE =>

- RIMUOVERE CONTAINER DEV => docker rm -f polardrive-api-dev
- REBUILD IMMAGINE WEB-API => docker build -f backend/PolarDrive.WebApi/Dockerfile -t polardrive-api:latest .
- START CONTAINER DEV => docker compose -f docker-compose.dev.yml --env-file .env.dev up -d api
- LOGS => docker compose -f docker-compose.dev.yml logs -f api

DEV => REBUILD FRONTEND POST MODIFICHE =>

- RIMUOVERE CONTAINER DEV => docker rm -f polardrive-frontend-dev
- REBUILD IMMAGINE DEV FRONTEND => docker build -f frontend/Dockerfile -t polardrive-frontend:latest .
- START CONTAINER DEV => docker compose -f docker-compose.dev.yml --env-file .env.dev up -d frontend
- LOGS => docker compose -f docker-compose.dev.yml logs -f frontend

### 🐳 DOCKER PROD

_(Tasto destro sulla cartella ROOT principale del progetto → Open in integrated Terminal)_

PROD => INFO SU OLLAMA

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
