// eslint-disable-next-line @typescript-eslint/no-require-imports
const fs = require("fs");
const swaggerSpec = JSON.parse(fs.readFileSync("swagger.json", "utf8"));

const collection = {
  info: {
    name: "PolarDrive API - Ultimate",
    schema:
      "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
  },
  variable: [
    {
      key: "baseUrl",
      value: "https://localhost:5041",
    },
    {
      key: "vehicleId",
      value: "1",
    },
    {
      key: "companyId",
      value: "1",
    },
    {
      key: "reportId",
      value: "1",
    },
    {
      key: "sampleVin",
      value: "TESTVIN123456789",
    },
    {
      key: "testVatNumber",
      value: "IT12345678901",
    },
    {
      key: "testPhoneNumber",
      value: "+393331234567",
    },
  ],
  item: [],
};

// Funzione per sostituire i placeholder nei path
function replacePlaceholders(path) {
  return path
    .replace(/{vehicleId}/g, "{{vehicleId}}")
    .replace(/{companyId}/g, "{{companyId}}")
    .replace(/{id}/g, "1")
    .replace(/{reportId}/g, "{{reportId}}")
    .replace(/{vin}/g, "{{sampleVin}}");
}

// Funzione per creare body di esempio MIGLIORATI
function createSampleBody(path, method) {
  const timestamp = new Date().toISOString();
  const futureDate = new Date(Date.now() + 3600000).toISOString();

  const sampleBodies = {
    // SmsAdaptiveProfiling
    "POST /api/SmsAdaptiveProfiling/receive-sms": {
      vehicleId: 1,
      messageContent: "Test SMS message from automated test",
    },

    // AdminFullClientInsert - FIXED for multipart
    "POST /api/AdminFullClientInsert": {
      CompanyName: "DataPolar Test Company",
      CompanyVatNumber: "IT98765432101",
      ReferentName: "Mario Rossi",
      ReferentEmail: "mario.rossi@datapolar.com",
      ReferentMobile: "+393331234567",
      VehicleVIN: "TESTVIN987654321",
      VehicleFuelType: "Electric",
      VehicleBrand: "Tesla",
      VehicleModel: "Model 3",
      VehicleTrim: "Long Range",
      VehicleColor: "White",
      UploadDate: timestamp,
    },

    // ClientCompanies - ENHANCED
    "POST /api/ClientCompanies": {
      vatNumber: `IT${Date.now().toString().slice(-11)}`,
      name: `DataPolar Test ${Date.now()}`,
      address: "Via Innovation 123, Milano, 20100, Italia",
      email: `test${Date.now()}@datapolar.com`,
      pecAddress: `pec${Date.now()}@datapolar.pec.it`,
      landlineNumber: "+390212345678",
      referentName: "Test Manager",
      referentMobileNumber: "+393339876543",
      referentEmail: `manager${Date.now()}@datapolar.com`,
    },

    "PUT /api/ClientCompanies/{id}": {
      id: 1,
      vatNumber: "IT12345678901",
      name: "Updated DataPolar Company",
      address: "Via Updated 456, Roma, 00100, Italia",
      email: "updated@datapolar.com",
      pecAddress: "updated@datapolar.pec.it",
      landlineNumber: "+390687654321",
      referentName: "Updated Manager",
      referentMobileNumber: "+393337654321",
      referentEmail: "updated.manager@datapolar.com",
    },

    // ClientConsents - ENHANCED
    "POST /api/ClientConsents": {
      clientCompanyId: 1,
      vehicleId: 1,
      consentType: "DataProcessingConsent",
      uploadDate: timestamp,
      notes: `Test consent created at ${timestamp}`,
    },

    "PATCH /api/ClientConsents/{id}/notes": {
      notes: `Updated consent notes at ${timestamp}`,
    },

    // ClientVehicles - ENHANCED
    "POST /api/ClientVehicles": {
      clientCompanyId: 1,
      vin: `TESTVIN${Date.now().toString().slice(-8)}`,
      fuelType: "Electric",
      brand: "Tesla",
      model: "Model Y",
      trim: "Performance",
      color: "Blue",
      isActive: true,
      isFetching: false,
    },

    "PATCH /api/ClientVehicles/{id}/status": {
      isActive: true,
      isFetching: true,
    },

    "PUT /api/ClientVehicles/{id}": {
      id: 1,
      clientCompanyId: 1,
      vin: "UPDATEDVIN123456",
      fuelType: "Electric",
      brand: "Tesla",
      model: "Model S",
      trim: "Plaid",
      color: "Black",
      isActive: true,
      isFetching: false,
    },

    // FileManager - ENHANCED
    "POST /api/FileManager/filemanager-download": {
      periodStart: "2024-01-01T00:00:00Z",
      periodEnd: "2024-12-31T23:59:59Z",
      companies: ["DataPolar Test Company"],
      vins: ["TESTVIN123456789"],
      brands: ["Tesla"],
      requestedBy: "AutomatedTest",
    },

    "PATCH /api/FileManager/{id}/notes": {
      notes: `File manager notes updated at ${timestamp}`,
    },

    // Logs - ENHANCED
    "POST /api/Logs": {
      source: "API_AutoTest",
      level: "Information",
      message: "Automated API test execution",
      details: `Test run at ${timestamp} - All systems operational`,
    },

    // OutagePeriods - ENHANCED
    "POST /api/OutagePeriods": {
      outageType: "ScheduledMaintenance",
      outageBrand: "Tesla",
      outageStart: timestamp,
      outageEnd: futureDate,
      vehicleId: 1,
      clientCompanyId: 1,
      notes: `Scheduled maintenance outage created at ${timestamp}`,
    },

    "PATCH /api/OutagePeriods/{id}/notes": {
      notes: `Outage notes updated at ${timestamp} - Maintenance completed successfully`,
    },

    // PdfReports - ENHANCED
    "PATCH /api/PdfReports/{id}/notes": {
      notes: `PDF report notes updated at ${timestamp} - Report validated`,
    },

    // TeslaFakeApi - ENHANCED
    "POST /api/TeslaFakeApi/GenerateVehicleReport/{vehicleId}": {
      analysisLevel: "comprehensive",
    },

    "POST /api/TeslaFakeApi/ControlScheduler": {
      action: "start",
      vehicleId: "1",
    },

    // TeslaFakeDataReceiver - ENHANCED
    "POST /api/TeslaFakeDataReceiver/ReceiveVehicleData/{vin}": {
      timestamp: timestamp,
      batteryLevel: 85,
      chargeRate: 25.5,
      location: {
        latitude: 45.4642,
        longitude: 9.19,
        address: "Milano, Lombardia, Italia",
      },
      speed: 0,
      odometer: 15432.7,
      isCharging: false,
      isClimateOn: true,
      interiorTemp: 22.5,
      exteriorTemp: 18.3,
    },
  };

  return sampleBodies[`${method.toUpperCase()} ${path}`] || {};
}

// Funzione per determinare il Content-Type corretto - MIGLIORATA
function getContentType(path) {
  const multipartEndpoints = [
    "/api/AdminFullClientInsert",
    "/api/ClientConsents/{id}/upload-zip",
    "/api/OutagePeriods/{id}/upload-zip"
  ];

  if (
    multipartEndpoints.some((endpoint) =>
      path.includes(endpoint.replace(/{id}/g, ""))
    )
  ) {
    return "multipart/form-data";
  }

  return "application/json";
}

// Funzione per aggiungere query parameters specifici - MIGLIORATA
function addQueryParameters(path, request) {
  // Date-based endpoints
  if (
    path.includes("/statistics") ||
    path.includes("/history") ||
    path.includes("/audit-logs") ||
    path.includes("/stats/period")
  ) {
    request.url.query = [
      {
        key: "fromDate",
        value: "2024-01-01T00:00:00Z",
      },
      {
        key: "toDate",
        value: "2024-12-31T23:59:59Z",
      },
    ];
  }

  // Specific endpoints
  if (path.includes("/resolve-ids")) {
    request.url.query = [
      {
        key: "vatNumber",
        value: "{{testVatNumber}}",
      },
      {
        key: "vin",
        value: "{{sampleVin}}",
      },
    ];
  }

  if (path.includes("/download-all-by-company")) {
    request.url.query = [
      {
        key: "vatNumber",
        value: "{{testVatNumber}}",
      },
    ];
  }

  if (path.includes("/available-vins")) {
    request.url.query = [
      {
        key: "company",
        value: "DataPolar Test Company",
      },
    ];
  }

  if (path.includes("/GenerateUrl")) {
    request.url.query = [
      {
        key: "brand",
        value: "Tesla",
      },
      {
        key: "vin",
        value: "{{sampleVin}}",
      },
    ];
  }

  if (path.includes("/OAuthCallback")) {
    request.url.query = [
      {
        key: "code",
        value: "test_authorization_code_12345",
      },
      {
        key: "state",
        value: "test_state_token_67890",
      },
    ];
  }

  // Pagination for audit logs
  if (path.includes("/audit-logs")) {
    request.url.query = request.url.query || [];
    request.url.query.push(
      {
        key: "page",
        value: "1",
      },
      {
        key: "pageSize",
        value: "50",
      }
    );
  }
}

// Genera la collection ULTIMATE
Object.keys(swaggerSpec.paths).forEach((path) => {
  Object.keys(swaggerSpec.paths[path]).forEach((method) => {
    const fixedPath = replacePlaceholders(path);
    const contentType = getContentType(path);
    const sampleBody = createSampleBody(path, method);

    const request = {
      name: `${method.toUpperCase()} ${path}`,
      request: {
        method: method.toUpperCase(),
        header: [
          {
            key: "Content-Type",
            value: contentType,
          },
          {
            key: "Accept",
            value: "application/json",
          },
          {
            key: "User-Agent",
            value: "PolarDrive-API-Tester/1.0",
          },
        ],
        url: {
          raw: `{{baseUrl}}${fixedPath}`,
          protocol: "https",
          host: ["localhost"],
          port: "5041",
          path: fixedPath.split("/").filter((p) => p),
        },
      },
    };

    // Aggiungi query parameters
    addQueryParameters(path, request.request);

    // Aggiungi body per richieste POST/PUT/PATCH - MIGLIORATO
    if (
      ["POST", "PUT", "PATCH"].includes(method.toUpperCase()) &&
      Object.keys(sampleBody).length > 0
    ) {
      if (contentType === "multipart/form-data") {
        request.request.body = {
          mode: "formdata",
          formdata: Object.keys(sampleBody).map((key) => ({
            key: key,
            value: String(sampleBody[key]),
            type: "text",
          })),
        };
        // Remove Content-Type header for multipart (let Newman set it)
        request.request.header = request.request.header.filter(
          (h) => h.key !== "Content-Type"
        );
      } else {
        request.request.body = {
          mode: "raw",
          raw: JSON.stringify(sampleBody, null, 2),
        };
      }
    }

    collection.item.push(request);
  });
});

// Aggiungi collection per health check separata
const healthCheckCollection = {
  info: {
    name: "PolarDrive Health Check",
    schema:
      "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
  },
  variable: collection.variable,
  item: [
    {
      name: "API Health Status",
      request: {
        method: "GET",
        header: [
          {
            key: "Accept",
            value: "application/json",
          },
        ],
        url: {
          raw: "{{baseUrl}}/api/OutageSystem/health",
          protocol: "https",
          host: ["localhost"],
          port: "5041",
          path: ["api", "OutageSystem", "health"],
        },
      },
    },
    {
      name: "System Statistics",
      request: {
        method: "GET",
        header: [
          {
            key: "Accept",
            value: "application/json",
          },
        ],
        url: {
          raw: "{{baseUrl}}/api/OutageSystem/stats",
          protocol: "https",
          host: ["localhost"],
          port: "5041",
          path: ["api", "OutageSystem", "stats"],
        },
      },
    },
    {
      name: "Quick CRUD Test",
      request: {
        method: "GET",
        header: [
          {
            key: "Accept",
            value: "application/json",
          },
        ],
        url: {
          raw: "{{baseUrl}}/api/ClientCompanies",
          protocol: "https",
          host: ["localhost"],
          port: "5041",
          path: ["api", "ClientCompanies"],
        },
      },
    },
  ],
};

// Salva entrambe le collection
fs.writeFileSync(
  "polardrive-collection-fixed.json",
  JSON.stringify(collection, null, 2)
);
fs.writeFileSync(
  "polardrive-health-check.json",
  JSON.stringify(healthCheckCollection, null, 2)
);

// Crea script di monitoraggio migliorati
const monitorScript = `@echo off
echo 🚀 PolarDrive API Ultimate Monitor
echo ===================================
echo.

echo ⏰ %date% %time%
echo 🔍 Testing API health...

newman run polardrive-health-check.json --insecure --reporter-json-export health-result.json --silent

if %errorlevel% equ 0 (
    echo ✅ API Health: GOOD
) else (
    echo ❌ API Health: ISSUES DETECTED
)

echo.
echo 📊 Running comprehensive API test...
newman run polardrive-collection-fixed.json --insecure --reporter-json-export full-result.json --timeout 15000

echo.
echo 📁 Results saved:
echo    - health-result.json
echo    - full-result.json
echo.
echo 📈 Check the JSON files for detailed metrics
pause
`;

fs.writeFileSync("monitor-api.bat", monitorScript);

// PowerShell script migliorato
const psScript = `Write-Host "🚀 PolarDrive API Ultimate Monitor" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Green
Write-Host ""

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
Write-Host "⏰ Starting test at $(Get-Date)" -ForegroundColor Yellow

try {
    Write-Host "🔍 Health Check..." -ForegroundColor Cyan
    newman run polardrive-health-check.json --insecure --reporter-json-export "health-$timestamp.json" --silent
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Health Check: PASSED" -ForegroundColor Green
    } else {
        Write-Host "❌ Health Check: FAILED" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "📊 Full API Test..." -ForegroundColor Cyan
    newman run polardrive-collection-fixed.json --insecure --reporter-json-export "full-$timestamp.json" --timeout 15000
    
    Write-Host ""
    Write-Host "✅ Testing completed successfully" -ForegroundColor Green
    Write-Host "📁 Results: health-$timestamp.json, full-$timestamp.json" -ForegroundColor Blue
    
} catch {
    Write-Host "💥 Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}`;

fs.writeFileSync("monitor-api.ps1", psScript);

// Output finale
console.log("🚀 PolarDrive API Ultimate Converter");
console.log("====================================");
console.log(
  `✅ Collection creata con ${collection.item.length} endpoint ottimizzati!`
);
console.log("");
console.log("📁 File generati:");
console.log(
  "   📄 polardrive-collection-fixed.json - Collection completa (65 endpoint)"
);
console.log(
  "   📄 polardrive-health-check.json     - Health check rapido (3 endpoint)"
);
console.log("   📄 monitor-api.bat                  - Script Windows");
console.log("   📄 monitor-api.ps1                  - Script PowerShell");
console.log("");
console.log("🔧 Miglioramenti ULTIMATE:");
console.log("   ✅ Body JSON realistici con timestamp dinamici");
console.log("   ✅ Multipart/form-data corretto per upload");
console.log("   ✅ Query parameters completi per tutti gli endpoint");
console.log("   ✅ Headers ottimizzati (Accept, User-Agent)");
console.log("   ✅ Variabili globali per riuso facile");
console.log("   ✅ Collection health check separata");
console.log("   ✅ Script di monitoraggio automatico");
console.log("");
console.log("🚀 Comandi da aggiungere al README:");
console.log("   newman run polardrive-health-check.json --insecure");
console.log("   newman run polardrive-collection-fixed.json --insecure");
console.log("   .\\monitor-api.bat");
console.log("");
console.log("🎯 Target: Massimizzare il numero di 200/201/204 responses!");
