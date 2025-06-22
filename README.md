# PolarDrive 🚗❄️

Repository unificato per il progetto **PolarDrive**.

---

## ⚙️ Comandi

### 🔷 FRONTEND GENERICO

_(Tasto destro sulla cartella `frontend` → Open in integrated Terminal)_

- `npm run dev` → Avvio classico in dev
- `npm lista` → Visualizza tutti i pacchetti installati
- `npm i` → Reinstalla tutti i pacchetti

---

### 🔶 BACKEND GENERICO

_(Tasto destro sulla cartella `backend` → Open in integrated Terminal)_

- `dotnet build` → Rebuild completo della soluzione
- `dotnet run --project PolarDriveInitDB.Cli` → Crea un nuovo DB da zero nella cartella `PolarDriveInitDB.Cli`
- `dotnet run --project PolarDriveInitDBMockData.Cli` → Aggiunge dati di mock al DB creato
- `dotnet run --project PolarDrive.WebApi` → Avvia la WebAPI principale

---

### 🚗 TESLA MOCK API SERVICE

_(Tasto destro sulla cartella `backend/PolarDrive.TeslaMockApiService` → Open in integrated Terminal)_

- `dotnet run` → Avvia il servizio di push dati verso la WebAPI
  > Simula i dati Tesla e li invia automaticamente alla WebAPI principale per test e sviluppo.

---

### 🧠 BACKEND PolarAi

_(Tasto destro sulla cartella `backend/PolarDrive.WebApi` → Open in integrated Terminal)_

#### ✅ Ollama

- `ollama --version` → Mostra la versione del runtime **Ollama**
  > Ollama è il **motore AI locale**: scarica, avvia e gestisce modelli LLM direttamente sul tuo PC.

#### 🧪 UTILIZZO CORRETTO IN BACKEND

- `ollama serve` → Avvia **Ollama in modalità server REST**
  > Espone l'endpoint `http://localhost:11434/api/generate` per richieste AI programmatiche.  
  > ✔️ È **stateless** e perfettamente integrabile nel backend (`HttpClient`, `POST`, JSON).

##### 🔍 Come verificare se è attivo

- Apri il browser e visita:  
  `http://localhost:11434`
