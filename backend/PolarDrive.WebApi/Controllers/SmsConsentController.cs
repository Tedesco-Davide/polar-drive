using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.Entities;

namespace PolarDrive.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ConsentController : ControllerBase
    {
        private readonly PolarDriveDbContext _db;
        private readonly PolarDriveLogger _logger;

        public ConsentController(PolarDriveDbContext db)
        {
            _db = db;
            _logger = new PolarDriveLogger(db);
        }

        [HttpGet("{token}")]
        public async Task<IActionResult> ShowConsentPage(string token)
        {
            // SICUREZZA: Forza HTTPS
            if (!Request.IsHttps && !Request.Host.Host.Contains("localhost"))
            {
                var httpsUrl = $"https://{Request.Host}{Request.Path}{Request.QueryString}";
                return Redirect(httpsUrl);
            }

            var consent = await _db.SmsAdaptiveGdpr
                .Include(c => c.ClientVehicle)
                .ThenInclude(v => v!.ClientCompany)
                .FirstOrDefaultAsync(c => c.ConsentToken == token && !c.IsActive);

            if (consent == null)
            {
                return BadRequest("Link non valido o già utilizzato.");
            }

            // SICUREZZA: Verifica scadenza
            if (consent.ExpiresAt.HasValue && consent.ExpiresAt < DateTime.Now)
            {
                return BadRequest("Link scaduto. Richiedi un nuovo consenso.");
            }

            // Restituisci pagina HTML inline
            var html = GenerateConsentPageHtml(consent);
            return Content(html, "text/html");
        }

        [HttpPost("{token}/accept")]
        public async Task<IActionResult> AcceptConsent(string token)
        {
            // SICUREZZA: Forza HTTPS
            if (!Request.IsHttps && !Request.Host.Host.Contains("localhost"))
            {
                return BadRequest("Connessione non sicura. Usa HTTPS.");
            }

            var consent = await _db.SmsAdaptiveGdpr
                .FirstOrDefaultAsync(c => c.ConsentToken == token && !c.IsActive);

            if (consent == null)
            {
                return NotFound("Link non valido.");
            }

            // SICUREZZA: Verifica scadenza
            if (consent.ExpiresAt.HasValue && consent.ExpiresAt < DateTime.Now)
            {
                return BadRequest("Link scaduto. Richiedi un nuovo consenso.");
            }

            // Attiva consenso con audit trail completo
            consent.ConsentGivenAt = DateTime.Now;
            consent.IsActive = true;
            consent.IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
            consent.UserAgent = Request.Headers["User-Agent"];

            await _db.SaveChangesAsync();

            await _logger.Info("Consent.Accept", "GDPR consent granted",
                $"Phone: {consent.PhoneNumber}, VehicleId: {consent.VehicleId}, IP: {consent.IpAddress}");

            var successHtml = GenerateSuccessPageHtml();
            return Content(successHtml, "text/html");
        }

        [HttpPost("cleanup-expired")]
        public async Task<IActionResult> CleanupExpiredConsents()
        {
            var expired = await _db.SmsAdaptiveGdpr
                .Where(c => c.ExpiresAt < DateTime.Now && !c.IsActive)
                .ToListAsync();

            _db.SmsAdaptiveGdpr.RemoveRange(expired);
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Removed {expired.Count} expired consent requests" });
        }

        private string GenerateConsentPageHtml(SmsAdaptiveGdpr consent)
        {
            var vehicleInfo = consent.ClientVehicle != null 
                ? $"Tesla {consent.ClientVehicle.Model ?? "Model"} (VIN: {consent.ClientVehicle.Vin})"
                : "Tesla";

            var companyInfo = consent.ClientVehicle?.ClientCompany?.Name ?? "Cliente";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Consenso GDPR - Tesla Adaptive Profiling</title>
    <style>
        body {{ font-family: Arial, sans-serif; max-width: 600px; margin: 50px auto; padding: 20px; }}
        .header {{ text-align: center; color: #333; }}
        .vehicle-info {{ background: #f5f5f5; padding: 15px; border-radius: 8px; margin: 20px 0; }}
        .consent-text {{ line-height: 1.6; margin: 20px 0; }}
        .btn {{ background: #007bff; color: white; padding: 15px 30px; border: none; border-radius: 5px; cursor: pointer; font-size: 16px; }}
        .btn:hover {{ background: #0056b3; }}
        .danger {{ color: #dc3545; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>🚗 Consenso Utilizzo Tesla</h1>
        <h2>Ricerca e Sviluppo PolarDrive</h2>
    </div>

    <div class='vehicle-info'>
        <strong>Veicolo:</strong> {vehicleInfo}<br>
        <strong>Cliente:</strong> {companyInfo}<br>
        <strong>Scadenza link:</strong> {consent.ExpiresAt:dd/MM/yyyy HH:mm} UTC
    </div>

    <div class='consent-text'>
        <h3>INFORMATIVA GDPR</h3>
        <p><strong>Durante l'utilizzo del veicolo verranno raccolti:</strong></p>
        <ul>
            <li>Posizione GPS precisa ogni 30 secondi</li>
            <li>Velocità, accelerazione, frenata</li>
            <li>Stato batteria e processi di ricarica</li>
            <li>Dati telemetrici per ricerca scientifica automotive</li>
            <li>Timestamp e metadati operativi</li>
        </ul>

        <p><strong>Finalità del trattamento:</strong></p>
        <ul>
            <li>Ricerca e sviluppo algoritmi di intelligenza artificiale</li>
            <li>Validazione sistemi di automotive intelligence</li>
            <li>Miglioramento tecnologie di mobilità sostenibile</li>
        </ul>

        <p><strong>Titolare del trattamento:</strong> DataPolar S.R.L.S.</p>
        <p><strong>I tuoi diritti:</strong> Puoi revocare il consenso in qualsiasi momento contattando DataPolar a support@datapolar.dev. Puoi anche richiedere accesso ai dati, rettifica o cancellazione.</p>

        <p class='danger'>⚠️ Cliccando ACCETTO dai il consenso esplicito al trattamento dei tuoi dati di localizzazione GPS per le finalità sopra indicate.</p>
    </div>

    <div style='text-align: center; margin: 30px 0;'>
        <button class='btn' onclick='acceptConsent()'>ACCETTO IL TRATTAMENTO DATI</button>
    </div>

    <script>
        function acceptConsent() {{
            fetch(window.location.pathname + '/accept', {{
                method: 'POST',
                headers: {{ 'Content-Type': 'application/json' }}
            }})
            .then(response => response.text())
            .then(html => {{
                document.body.innerHTML = html;
            }})
            .catch(error => {{
                alert('Errore durante l\'accettazione: ' + error);
            }});
        }}
    </script>
</body>
</html>";
        }

        private string GenerateSuccessPageHtml()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Consenso Registrato</title>
    <style>
        body { font-family: Arial, sans-serif; max-width: 600px; margin: 50px auto; padding: 20px; text-align: center; }
        .success { color: #28a745; font-size: 24px; margin: 20px 0; }
        .info { background: #d4edda; padding: 15px; border-radius: 8px; margin: 20px 0; }
    </style>
</head>
<body>
    <div class='success'>✅ Consenso registrato con successo!</div>
    
    <div class='info'>
        <strong>Ora puoi ripetere il comando SMS per attivare l'Adaptive Profiling.</strong><br><br>
        Timestamp: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + @" UTC<br>
        IP: " + Request.HttpContext.Connection.RemoteIpAddress?.ToString() + @"
    </div>

    <p>Puoi chiudere questa pagina e tornare al tuo SMS.</p>
</body>
</html>";
        }
    }
}