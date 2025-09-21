# PolarDrive 🚗❄️

Repository backend per il progetto **PolarDrive**.

---

## ⚙️ Comandi

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
- `dotnet run --project PolarDriveInitDB.Cli` → Crea un nuovo DB da zero nella cartella `PolarDriveInitDB.Cli`
- `dotnet run --project PolarDriveInitDBMockData.Cli` → Aggiunge dati di mock al DB creato (opzionale)
- `dotnet run --project PolarDrive.WebApi` → Avvia la WebAPI principale in modalità sviluppo

    > Espone l'endpoint `http://localhost:3000/admin` per Dashboard Backend

- Extra comandi da lancaire nel caso di problemi di connessione al backend

    > dotnet dev-certs https --clean

    > dotnet dev-certs https --trust
    
    > dotnet dev-certs https --check


#### **📦 PRODUZIONE (Production)**

- `dotnet build --configuration Release` → Build ottimizzata per produzione
- `dotnet run --project PolarDriveInitDB.Cli --configuration Release` → Crea DB (produzione)
- `dotnet run --project PolarDrive.WebApi --configuration Release` → Avvia WebAPI in produzione

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
