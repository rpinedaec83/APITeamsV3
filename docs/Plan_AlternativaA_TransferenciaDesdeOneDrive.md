# Plan de Ejecución: Alternativa A - Transferencia de Grabaciones Directa desde OneDrive de la Cuenta de Servicio

**Fecha de Creación**: 2026-09-22  
**Objetivo**: Procesar y transferir las grabaciones `.mp4` acumuladas en el OneDrive de la cuenta de servicio institucional (`Propietario2`), enviándolas a los equipos de Teams correspondientes, incluso si dichas secciones **no forman parte de la lista de pilotos**, validando siempre la regla obligatoria de modalidad:
```sql
SELECT * FROM Seccion S WHERE S.IdTipoModalidad = 29690
```

---

## 1. Justificación y Diagnóstico

### El Problema Actual:
- **Flujo tradicional (Sección -> OneDrive)**:
  El job recurrente (`RunPilotRecordingTransfers`) recorre únicamente las secciones del piloto (`CompanyPilotSections`) y busca en el OneDrive si hay videos para esas secciones específicas.
- **Archivos huérfanos**:
  Las reuniones de secciones que **no están en el piloto** también se grabaron con la cuenta de servicio institucional (`appteams...`). Como la empresa está en modo piloto (`IsPilotMode = true`), el job masivo (`RunAllRecordingTransfers`) está cancelado por código y nunca las busca.
- **Ineficiencia del barrido por sección**:
  Recorrer miles de secciones en la base de datos para buscar si tienen videos en OneDrive genera cientos de peticiones vacías a Microsoft Graph.

### La Solución (Alternativa A - Enfoque Inverso: OneDrive -> Equipos):
En lugar de recorrer secciones en la base de datos:
1. Se consultan directamente los archivos `.mp4` que están **físicamente en la carpeta `/Recordings` del OneDrive de la cuenta de servicio**.
2. De cada nombre de archivo se extrae el identificador de la sección (`IdSeccion` / `Codigo`).
3. Se valida en `SmartDB` que la sección cumpla la regla `IdTipoModalidad == 29690` y tenga equipo activo en `cTeams`.
4. Se transfiere cada video al SharePoint del canal del Team (`/General/Recordings/`).
5. Tras confirmarse la copia, se elimina el archivo original del OneDrive para liberar almacenamiento.

---

## 2. Componentes Técnicos a Implementar

### A. Capa de Aplicación e Interfaces
1. **`ITeamsRecordingTransferService`** (`APITeamsV3.Application/Common/Interfaces/ITeamsRecordingTransferService.cs`):
   - Agregar método:
     ```csharp
     Task<RecordingTransferBatchResult> TransferAllFromServiceAccountOneDriveAsync(
         string? serviceAccountUpn = null, 
         CancellationToken cancellationToken = default);
     ```

2. **`IHangfireJobService`** (`APITeamsV3.Application/Common/Interfaces/IHangfireJobService.cs`):
   - Agregar métodos para encolar y ejecutar:
     ```csharp
     Task<string> EnqueueRecordingTransfersFromOneDrive(string companyKey, string? executedBy = null);
     Task RunRecordingTransfersFromOneDrive(string companyKey, string? executedBy, PerformContext? performContext);
     ```

---

### B. Capa de Infraestructura

1. **`TeamsRecordingTransferService.cs`** (`APITeamsV3.Infrastructure/Services/TeamsRecordingTransferService.cs`):
   - **Paso 1: Resolver la cuenta de servicio**:
     Utilizar `ResolveOrganizerFromAplicativosTeamsAsync(cancellationToken)` para obtener el UPN de la cuenta institucional (ej. `appteamszegel@...`).
   - **Paso 2: Listar archivos en OneDrive**:
     Paginar la carpeta `Recordings` del Drive de la cuenta de servicio (`graphClient.Users[upn].Drive.Items["root"].ItemWithPath("Recordings").Children`).
   - **Paso 3: Extracción de Sección del Nombre del Archivo**:
     Aplicar regex para detectar la sección:
     ```csharp
     // Regex estándar de Teams / APITeamsV3:
     var match = Regex.Match(fileName, @"SEC(?<sec>\d+)COD(?<cod>.*?)GRP(?<grp>.*?)\]", RegexOptions.IgnoreCase);
     int idSeccion = 0;
     if (match.Success)
     {
         int.TryParse(match.Groups["sec"].Value, out idSeccion);
     }
     else
     {
         // Fallback para nombres con token SEC o ID numérico entre corchetes
         var fallbackMatch = Regex.Match(fileName, @"SEC[:\s]?(?<sec>\d+)", RegexOptions.IgnoreCase);
         if (fallbackMatch.Success) int.TryParse(fallbackMatch.Groups["sec"].Value, out idSeccion);
     }
     ```
   - **Paso 4: Validación en Base de Datos**:
     ```csharp
     var targetModalidadId = _options.RequiredModalidadId ?? 29690;
     var seccion = await _smartDbContext.SeccionTable
         .AsNoTracking()
         .FirstOrDefaultAsync(s => s.IdSeccion == idSeccion && s.IdTipoModalidad == targetModalidadId, cancellationToken);

     if (seccion == null)
     {
         // Omitir: no cumple con la modalidad requerida (29690) o no existe la sección
         continue;
     }

     var team = await _smartDbContext.Set<TeamEntity>()
         .AsNoTracking()
         .FirstOrDefaultAsync(t => t.IdSeccionSmart == idSeccion && t.EstadoTeam == "A" && t.IsActive == "A", cancellationToken);

     if (team == null || string.IsNullOrWhiteSpace(team.IdTeamsGroup))
     {
         // Omitir: la sección no tiene Team activo provisionado
         continue;
     }
     ```
   - **Paso 5: Transferir y Eliminar**:
     - Resolver destino del Team (`ResolveDestinationAsync`).
     - Copiar a SharePoint (`CopyDriveItemAsync`).
     - Si la copia finaliza exitosamente y `_options.DeleteSourceAfterCopy == true`, eliminar el archivo original del OneDrive.

2. **`HangfireJobService.cs`** (`APITeamsV3.Infrastructure/Services/HangfireJobService.cs`):
   - Implementar `RunRecordingTransfersFromOneDrive`:
     - Inicializa el contexto del tenant (`ResolveTenantAsync`).
     - Ejecuta la transferencia de los archivos del OneDrive hacia sus equipos.
     - Maneja logs detallados de archivos transferidos, omitidos y errores.

---

### C. Capa de API y Controlador
1. **`JobsController.cs`** (`APITeamsV3.API/Controllers/JobsController.cs`):
   - Exponer el endpoint para ejecución manual:
     ```csharp
     [HttpPost("recordings-transfer-onedrive/run")]
     [Authorize(Roles = "ADMIN,IT")]
     public async Task<ActionResult<object>> RunRecordingTransferFromOneDriveNow()
     {
         var tenant = _tenantProvider.GetCurrentTenant();
         var executedBy = GetManualExecutorName();
         var jobId = await _jobService.EnqueueRecordingTransfersFromOneDrive(tenant.CompanyKey, executedBy);
         return Ok(new
         {
             JobId = jobId,
             Message = $"Transferencia desde OneDrive encolada para tenant {tenant.CompanyKey} por {executedBy}."
         });
     }
     ```

---

## 3. Pasos para la Ejecución Mañana

1. **Implementar los métodos en el código**:
   - Agregar método en `ITeamsRecordingTransferService` y su implementación en `TeamsRecordingTransferService.cs`.
   - Agregar método en `IHangfireJobService` y su implementación en `HangfireJobService.cs`.
   - Agregar endpoint `POST /api/jobs/recordings-transfer-onedrive/run` en `JobsController.cs`.

2. **Compilar y validar localmente**:
   ```powershell
   dotnet build APITeamsV3.API/APITeamsV3.API.csproj
   ```

3. **Publicar y Desplegar a IIS**:
   Ejecutar el script automatizado:
   ```powershell
   powershell -ExecutionPolicy Bypass -File deploy\deploy_iis.ps1
   ```

4. **Ejecutar la Transferencia**:
   - Realizar la llamada HTTP al endpoint:
     ```http
     POST https://apiteams.zegel.edu.pe/api/jobs/recordings-transfer-onedrive/run
     (o para idat según corresponda)
     ```
   - O monitorear su progreso en el Dashboard de Hangfire (`/hangfire`).
