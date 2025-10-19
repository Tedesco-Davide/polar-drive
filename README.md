# PolarDrive 🚗❄️

Repository per il progetto **PolarDrive**.

---

### 🐳 DOCKER

_(Tasto destro sulla cartella ROOT principale del progetto → Open in integrated Terminal)_

Crea la rete con nome esplicito (comando da lanciare una sola volta)

- docker network create polardrive-network-dev
- docker network create polardrive-network-prod

DEV => REBUILD FRONTEND POST MODIFICHE =>

- STOP CONTAINER DEV => docker compose -f docker-compose.dev.yml down
- REBUILD IMMAGINE DEV FRONTEND => docker build -f frontend/Dockerfile -t polardrive-frontend:latest .
- RESTART CONTAINER DEV => docker compose -f docker-compose.dev.yml --env-file .env.dev up -d
- LOGS => docker compose -f docker-compose.dev.yml logs -f frontend

DEV => REBUILD TESLA-MOCK-API-SERVICE POST MODIFICHE =>

- STOP CONTAINER DEV ⇒ docker compose -f docker-compose.dev.yml down
- REBUILD IMMAGINE TESLA MOCK ⇒ docker build -f backend/PolarDrive.TeslaMockApiService/Dockerfile -t polardrive-mock:latest .
- RESTART CONTAINER DEV ⇒ docker compose -f docker-compose.dev.yml --env-file .env.dev up -d
- LOGS ⇒ docker compose -f docker-compose.dev.yml logs -f mock

DEV => REBUILD POLARDRIVE-WEB-API POST MODIFICHE =>

- STOP CONTAINER DEV ⇒ docker compose -f docker-compose.dev.yml down
- REBUILD IMMAGINE WEB-API ⇒ docker build -f backend/PolarDrive.WebApi/Dockerfile -t polardrive-api:latest .
- RESTART CONTAINER DEV ⇒ docker compose -f docker-compose.dev.yml --env-file .env.dev up -d
- LOGS ⇒ docker compose -f docker-compose.dev.yml logs -f api

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

    - `dotnet run --project PolarDriveInitDBMockData.Cli` → Aggiunge dati di mock al DB creato (opzionale)

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
