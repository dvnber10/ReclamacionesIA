# Reclamaciones IA · Prueba técnica Desarrollador Junior IA y Automatización

Canal público de radicación + motor de clasificación con IA + panel interno para analistas, construido para el caso de reclamaciones de Global Seguros de Vida.

> **Estado de los entregables externos:** los enlaces marcados como `TODO_*` se completan al momento de la entrega. El repositorio será **público**; no hará falta invitar a los revisores, aunque se les confirmará el acceso.

| Entregable | Dónde está |
|---|---|
| Repositorio de GitHub con acceso de lectura | `https://github.com/dvnber10/ReclamacionesIA.git` (público) |
| Canal de radicación desplegado | `https://reclamacionesia-bkfvdkf8cfd4c8e2.westus-01.azurewebsites.net/` |
| Panel del analista + usuario de prueba | `https://reclamacionesia-bkfvdkf8cfd4c8e2.westus-01.azurewebsites.net/Cuenta/Login` · `analista@demo.local` / `Demo123*` |
| README | Este archivo |
| Las cuatro respuestas del punto 7 | [Las cuatro respuestas](#las-cuatro-respuestas-del-punto-7) |
| Dataset mínimo de 30 radicaciones | [Dataset de prueba](#dataset-de-prueba) |
| Video de 5 minutos | `https://drive.google.com/file/d/1mjJE1REwimXxnIQI0Y-MiA2xdrjeLO2g/view?usp=sharing` |
| Accesos a los tres correos | [Accesos](#accesos) |

---

## 1. Qué resuelve y qué no hace

El sistema recibe reclamaciones sin exigir usuario al beneficiario, infiere el tipo de reclamación del texto libre y de los nombres de archivo, revisa el expediente contra las reglas del negocio, avisa qué falta y arranca un reloj legal de un mes **cuando el expediente queda completo**, no cuando se radica.

**Frontera deliberada:** la IA clasifica, extrae y sugiere. No decide pagar, objetar ni pedir documentos; no calcula indemnizaciones. Toda decisión final la registra una persona en el panel y queda en el historial del caso.

### Lo que se decidió construir

- **Canal:** página web pública (`/Radicar`) + seguimiento por token (`/Seguimiento/Index?token=...`). Se descartó WhatsApp por requerir aprobación de plantillas y número de negocio; en su lugar se ofrece **Telegram opt-in** y correo SMTP real como canales de aviso desde el `outbox`.
- **IA:** Gemini como proveedor principal, Ollama como alternativa local y reglas locales como fallback auditado. Se usó la API OpenAI-compatible de Gemini.
- **Persistencia:** PostgreSQL + EF Core, con eventos de dominio para poder reconstruir cualquier caso.
- **Sin MediatR, AutoMapper ni segundo front:** se priorizó que las tres piezas estuvieran completas y fueran explicables.

---

## 2. Arquitectura

Se selecciono una arquitectura por capas ya que mantiene el orden y la estructura para cada archivo haciendolos mas entendibles y mantenibles con el tiempo, separadi en 4 capas principales las cuales se dividen de la siguiente manera:
```text
ReclamacionesIA.sln
└── src/
    ├── Common/     DTOs y enums compartidos (sin dependencias)
    ├── Business/   Entidades, interfaces y reglas puras (ClaimRules, LegalClock)
    ├── Data/       EF Core, Npgsql, migraciones, repositorio y adaptador HTTP de IA
    └── WebUI/      MVC/Razor, controladores, vistas, seeder y hosted services
```

| Proyecto | Responsabilidad |
|---|---|
| `Common` | `ArchivoDto`, `ClasificacionDto`, `PanelDto`, `RadicarCommand`, enums de estado, tipo, decisión, canal y fallo de IA. |
| `Business` | `Claim`, `ClaimDocument`, `Evento`, `Decision`, `InferenciaIA`, `OutboxMessage`; `ClaimRules`, `LegalClock`, `MotorService`, `RadicacionService`, `ComplementacionService`, `PanelService`. |
| `Data` | `ReclamacionesDbContext`, configuraciones Fluent API, 5 migraciones, `ClaimRepository` y `HttpClassificationService`. |
| `WebUI` | `Program.cs`, controladores, vistas, `DbInitializer`, `IaOptions`/`TelegramOptions`/`EmailOptions` y los tres workers. |

El pipeline se registra en `src/WebUI/Program.cs`: DbContext Npgsql, servicios de negocio, `IHttpClientFactory`, opciones, autenticación por cookie, tres `AddHostedService`, `DbInitializer.SeedAsync(app)` y routing convencional.

---

## 3. Cómo levantarlo desde cero

### Requisitos

- .NET SDK 8.0 (se desarrolló con 8.0.423).
- PostgreSQL 16 o compatible.
- Opcional: Docker para el contenedor local de PostgreSQL.
- Opcional: Ollama con `llama3.2` si se quiere usar el proveedor local.
- Key de Gemini con acceso al modelo `gemini-3.5-flash-lite`.

### 3.1 Base de datos local

Si aún no tienes el contenedor:

```bash
docker run --name pg-reclamaciones \
  -e POSTGRES_PASSWORD='<password>' \
  -e POSTGRES_DB=reclamaciones \
  -p 5432:5432 -d postgres:16
```

Si ya existe, solo verifícalo:

```bash
docker ps --filter name=pg-reclamaciones
```

### 3.2 Secretos

Todos los valores sensibles viven en user-secrets. El `UserSecretsId` está en `src/WebUI/WebUI.csproj`:

```bash
cd /ruta/a/ReclamacionesIA/src/WebUI

dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=reclamaciones;Username=postgres;Password=<password>"
dotnet user-secrets set "IA:Gemini:ApiKey" "<tu-key-de-gemini>"
dotnet user-secrets set "IA:Gemini:Modelo" "gemini-3.5-flash-lite"
dotnet user-secrets set "Telegram:BotToken" "<token-del-bot>"
dotnet user-secrets set "Telegram:BotUsername" "<usuario-del-bot>"
dotnet user-secrets set "Email:Usuario" "<usuario-smtp>"
dotnet user-secrets set "Email:Password" "<password-smtp>"
dotnet user-secrets set "Email:From" "<remitente>"
```

todos estos datos al configurarlos viven en la maquina de configuracion para no exponerlos al momento de desplegar.

Verificación sin exponer valores:

```bash
dotnet user-secrets list | sed -E 's/(= ).*/\1***/'
```

> **`IA:Gemini:BaseUrl` ya está en `appsettings.json`:** `https://generativelanguage.googleapis.com/v1beta/openai`.  
> **`IA:Proveedor` ya está en `appsettings.json`:** `Gemini`.  
> Si usaste claves sin el prefijo `IA:` (`Gemini:ApiKey`, `Gemini:Modelo`), bórralas: no se enlazan a `IaOptions`, porque la sección configurada es `IA`.

**Por qué `gemini-3.5-flash-lite`:** `gemini-3.8-flash`, `gemini-3.5-flash` y `gemini-flash-latest` respondieron `503 high demand` con la key de prueba; `gemini-2.5-flash` ya no está disponible para cuentas nuevas (`404`); los modelos `pro` devuelven `429` porque el plan gratuito tiene cupo 0. `gemini-3.5-flash-lite` responde `200` con el mismo payload, incluido `response_format: json_object`.

### 3.3 Build, migraciones y seed

```bash
cd /ruta/a/ReclamacionesIA
dotnet restore ReclamacionesIA.sln
dotnet build ReclamacionesIA.sln --nologo -v q
dotnet run --project src/WebUI --launch-profile http
```

La app queda en `http://localhost:5232`. Al arrancar, `DbInitializer.SeedAsync` aplica las migraciones automáticamente (`Database.MigrateAsync`) y, solo si la tabla `reclamaciones` está vacía, siembra los 30 casos.

Migración manual opcional:

```bash
dotnet ef database update --project src/Data --startup-project src/WebUI
```

Para volver a sembrar desde cero:

```bash
dotnet ef database drop --project src/Data --startup-project src/WebUI
dotnet run --project src/WebUI --launch-profile http
```

### 3.4 Primer recorrido

1. Entrar a `http://localhost:5232/Radicar`, llenar el formulario y adjuntar varios archivos. El único botón visible (`Adjuntar documentos` / `+ Agregar más documentos`) abre el selector nativo oculto y acumula archivos; se puede volver a usar las veces que haga falta.
2. Copiar el token de seguimiento de la pantalla de confirmación.
3. Abrir `http://localhost:5232/Seguimiento/Index?token=<token>` para ver faltantes, inválidos y reloj.
4. Entrar a `http://localhost:5232/Cuenta/Login` con `analista@demo.local` / `Demo123*`.
5. En `/Panel` ver los KPIs; en `/Panel/Detalle?id=<guid>` revisar timeline, documentos y registrar la decisión.

---

## 4. Rutas principales

| Verbo | Ruta | Acción |
|---|---|---|
| GET/POST | `/Radicar` | Radicación pública con anti-forgery y `multipart/form-data`. |
| GET | `/Seguimiento/Index?token={token}` | Estado público por `PublicToken`. |
| POST | `/Seguimiento/Complementar` | Agregar documentos; redirige de nuevo al seguimiento (PRG). |
| GET | `/Panel` y `/Panel/Index` | KPIs, filtros y listado. Requiere login. |
| GET | `/Panel/Detalle?id={guid}&doc={guid?}` | Timeline, checklist, reloj y visor PDF. |
| POST | `/Panel/ConfirmarTipo` | Corrección humana del tipo inferido. |
| POST | `/Panel/RegistrarDecision` | Pagar / objetar / pedir más documentos. |
| GET | `/Panel/Ver?id={documentoId}` | Stream del archivo con `nosniff`. |
| POST | `/Panel/RegistrarRevisionDocumento` | Marcar documento como válido o inválido. |
| GET/POST | `/Cuenta/Login`, `/Cuenta/Logout` | Autenticación del panel. |

En producción, las rutas públicas son `TODO_URL_CANAL` y `TODO_URL_PANEL`.

---

## 5. Reglas del negocio

### Checklist por tipo (`Business/Services/ClaimRules.cs`)

| Tipo | Documentos exigidos |
|---|---|
| `MuerteNatural` | Formulario de reclamación, registro civil, certificado médico, cédula del asegurado, cédula del beneficiario, SARLAFT firmado, certificación bancaria. |
| `MuerteAccidental` | Los 7 anteriores + informe de policía o fiscalía. |
| `IncapacidadTotalPermanente` | Formulario, dictamen de pérdida de capacidad, historia clínica resumida, cédula del asegurado, SARLAFT firmado, certificación bancaria. |

### Faltan ≠ presentes pero inválidos

- **Faltan:** no existe ningún archivo detectado para ese soporte.
- **PresentesPeroInvalidos:** existe el archivo pero está ilegible, sin firma, de otra persona o no corresponde. Se elige un único motivo por prioridad: `SinFirma` → `PersonaNoCoincide` → `Ilegible` → `TipoNoCorresponde` → `Pendiente`.
- El expediente solo queda completo cuando `Faltan` **y** `PresentesPeroInvalidos` están vacíos. El mensaje al beneficiario es distinto en cada caso.

### Reloj legal (`Business/Services/LegalClock.cs`)

- `DiasAlerta = 3`.
- `FechaLimite = inicio.AddMonths(1)`.
- `DiaDelPlazo = max(1, hoy - inicio + 1)`; `DiasRestantes = limite - hoy`.
- `EnRiesgo`: entre 0 y 3 días restantes. `Vencido`: hoy > límite.
- El inicio se fija en `Claim.MarcarCompleto` y se congela al pagar u objetar. Reconfirmar un expediente completo **no** reinicia el plazo.
- Si la IA falla o da baja confianza, el caso entra en `RequiereRevision` y el plazo **no arranca** hasta que una persona confirme el tipo.

### Idempotencia

| Clave | Dónde se aplica |
|---|---|
| `ExternalEventId` | `RadicacionService`: si ya existe, devuelve el mismo `PublicToken` y marca `EsNuevo = false`. |
| `PublicToken` | Índice único; usado por seguimiento y deep-link de Telegram. |
| `(ClaimId, ContentHash)` | `Claim.AgregarDocumento` y `ComplementacionService`: ignora el reenvío del mismo archivo y registra `DuplicadoIgnorado`. El archivo físico se guarda como `{SHA256}.bin`. |
| `(ClaimId, Canal, PayloadJson, Estado=Pendiente)` | `AddAvisoAsync` evita dos avisos iguales pendientes por canal. |
| Primer aviso y aviso de vencimiento | Eventos de una sola vez por reclamación. |

### Reconstrucción y avisos

`eventos` guarda tipo, actor, payload JSON y momento: `Radicado`, `DocumentoRecibido`, `Clasificado`, `ClasificacionCorregida`, `PrimerAviso`, `ExpedienteCompleto`, `AvisoVencimiento`, `DuplicadoIgnorado`, `DecisionHumana`, `DocumentoValidado`, `TelegramVinculado`, entre otros. El detalle del panel pinta ese timeline.

`outbox` guarda un mensaje por canal (`Telegram = 0`, `Web = 1`) con estado `Pendiente/Enviado/Fallido`, destino, payload, intentos y último error. Los workers lo drenan; Telegram solo entrega lo que ya está en outbox.

### IA y fallback (`Data/Adapters/HttpClassificationService.cs`)

- Prompt de sistema: clasifica entre los 3 tipos y reconoce soportes; prohíbe decidir pago, objeción o completitud.
- Confianza: 0.80–0.95 si es concreto, 0.55–0.79 si es dudoso, 0.10–0.49 si es insuficiente. Si el tipo es `Desconocido`, se limita a 0.49.
- Si falla HTTP/existe un timeout, se degradan a reglas locales con confianza ≤ 0.49 y se marca `RequiereRevision`.
- Toda llamada queda en `inferencias_ia`: proveedor, modelo, versión de prompt, solicitud, respuesta cruda, éxito, error, latencia y confianza. Se auditó el fallback `503` de `gemini-3.8-flash` antes de cambiar al modelo estable.

---

## 6. Las cuatro respuestas del punto 7

Todas se pueden ver en el panel (`TODO_URL_PANEL`) y se pueden verificar con SQL sobre PostgreSQL. Los identificadores van entre comillas porque EF genera columnas en `PascalCase`.

### 1. ¿Cuántos casos hay abiertos, en qué día del plazo va cada uno, cuántos están en riesgo y cuántos se vencieron?

**En la UI:** tarjetas `Abiertos`, `Radicados hoy`, `En riesgo`, `Vencidos` en `Views/Panel/Index.cshtml`; columna `Día del plazo` y colores por riesgo en la tabla (`PanelService.ObtenerResumenAsync`, `LegalClock`). El reloj por caso está en `Views/Panel/Detalle.cshtml`.

```sql
WITH hoy AS (
  SELECT (now() AT TIME ZONE 'UTC')::date AS d
)
SELECT
  count(*) FILTER (WHERE r."Estado" NOT IN (5,6,7)) AS "Abiertos",
  count(*) FILTER (
    WHERE r."CreadoEn" >= (h.d::timestamp AT TIME ZONE 'UTC')
      AND r."CreadoEn" <  ((h.d + 1)::timestamp AT TIME ZONE 'UTC')
  ) AS "RadicadosHoy",
  count(*) FILTER (
    WHERE r."FechaLimite" IS NOT NULL
      AND r."Estado" NOT IN (5,6,7)
      AND r."FechaLimite" >= h.d
      AND r."FechaLimite" <= h.d + 3
  ) AS "EnRiesgo",
  count(*) FILTER (
    WHERE r."FechaLimite" IS NOT NULL
      AND r."Estado" NOT IN (5,6,7)
      AND r."FechaLimite" < h.d
  ) AS "Vencidos"
FROM "reclamaciones" r
CROSS JOIN hoy h;
```

```sql
SELECT r."PublicToken", r."CompletadoEl", r."FechaLimite",
       r."FechaLimite" - (now() AT TIME ZONE 'UTC')::date AS "DiasRestantes",
       CASE WHEN r."FechaLimite" IS NOT NULL
                 AND r."Estado" NOT IN (5,6,7)
                 AND (now() AT TIME ZONE 'UTC')::date > r."FechaLimite"
            THEN true ELSE false END AS "Vencido"
FROM "reclamaciones" r
ORDER BY r."CreadoEn" DESC;
```

### 2. ¿Cuántas clasificaciones del modelo tuvo que corregir una persona?

**En la UI:** tarjeta `Correcciones humanas` en `/Panel` y etiqueta `Confirmado por humano` en el detalle. Se cuentan los eventos `ClasificacionCorregida`, generados por `PanelController.ConfirmarTipo` → `Claim.ConfirmarTipo`.

```sql
SELECT
  count(DISTINCT r."Id") AS "TotalGeneral",
  count(DISTINCT e."ClaimId") FILTER (WHERE e."Tipo" = 'ClasificacionCorregida') AS "CorreccionesHumanas",
  round(
    100.0 * count(DISTINCT e."ClaimId") FILTER (WHERE e."Tipo" = 'ClasificacionCorregida')
    / NULLIF(count(DISTINCT r."Id"), 0), 1
  ) AS "PorcentajeCorrecciones"
FROM "reclamaciones" r
LEFT JOIN "eventos" e ON e."ClaimId" = r."Id";
```

```sql
SELECT count(*) AS "InferenciasTotales",
       count(*) FILTER (WHERE "Exito" = false) AS "InferenciasFallidas",
       count(*) FILTER (WHERE "Proveedor" = 2) AS "Gemini",
       count(*) FILTER (WHERE "Proveedor" = 1) AS "Ollama",
       count(*) FILTER (WHERE "Proveedor" = 0) AS "SinIA"
FROM "inferencias_ia";
```

### 3. ¿Cuánto se demora desde la radicación hasta decirle al beneficiario qué le falta?

**En la UI:** tarjeta `Tiempo hasta 1er aviso` en `/Panel`, calculada como mediana de `PrimerAvisoEn - CreadoEn` en minutos. El evento `PrimerAviso` se marca una sola vez en `Claim.MarcarPrimerAviso`. El escenario del enunciado es 4,5 días = 6.480 minutos.

```sql
SELECT percentile_cont(0.5) WITHIN GROUP (
         ORDER BY EXTRACT(EPOCH FROM ("PrimerAvisoEn" - "CreadoEn")) / 60.0
       ) AS "LatenciaPrimerAvisoMin"
FROM "reclamaciones"
WHERE "PrimerAvisoEn" IS NOT NULL
  AND "PublicToken" NOT ILIKE 'DEMO-%';
```

> Los tokens `DEMO-*` se excluyen del KPI para que el dato sembrado no maquille la mediana.

### 4. ¿Se puede reconstruir la historia completa de un caso al azar?

**En la UI:** `/Panel/Detalle?id={guid}` muestra reloj, tipo inferido, tipo efectivo, confianza, faltantes, inválidos, decisiones y timeline completo. Las decisiones incluyen actor, fecha, acción, notas, `dentroDelPlazo` y `montoIndemnizacion`.

```sql
-- Historial de un caso
SELECT e."CreadoEn", e."Tipo", e."Actor", e."PayloadJson"
FROM "eventos" e
WHERE e."ClaimId" = (SELECT "Id" FROM "reclamaciones" WHERE "PublicToken" = 'DEMO-003')
ORDER BY e."CreadoEn";

-- Inferencias del mismo caso
SELECT i."Proveedor", i."Modelo", i."Exito", i."Error", i."LatenciaMs", i."Confianza"
FROM "inferencias_ia" i
WHERE i."ClaimId" = (SELECT "Id" FROM "reclamaciones" WHERE "PublicToken" = 'DEMO-003')
ORDER BY i."CreadoEn";

-- Decisiones humanas del mismo caso
SELECT d."Accion", d."Notas", d."MontoIndemnizacion", d."CreadoEn", a."Email" AS "Analista"
FROM "decisiones" d
JOIN "analistas" a ON a."Id" = d."AnalystId"
WHERE d."ClaimId" = (SELECT "Id" FROM "reclamaciones" WHERE "PublicToken" = 'DEMO-003')
ORDER BY d."CreadoEn";
```

```sql
-- Cuántas decisiones finales se tomaron dentro del plazo
WITH finales AS (
  SELECT r."Id", r."FechaLimite", r."CompletadoEl",
         (SELECT d."CreadoEn"
            FROM "decisiones" d
           WHERE d."ClaimId" = r."Id"
             AND d."Accion" <> 3
           ORDER BY d."CreadoEn" DESC
           LIMIT 1) AS "FinalizadoEn"
  FROM "reclamaciones" r
  WHERE r."Estado" IN (5,6,7)
)
SELECT count(*) AS "Finalizados",
       count(*) FILTER (
         WHERE "FinalizadoEn" IS NULL OR "FechaLimite" IS NULL
            OR ("FinalizadoEn" AT TIME ZONE 'UTC')::date <= "FechaLimite"
       ) AS "DentroDelPlazo",
       count(*) FILTER (
         WHERE "FinalizadoEn" IS NOT NULL AND "FechaLimite" IS NOT NULL
           AND ("FinalizadoEn" AT TIME ZONE 'UTC')::date > "FechaLimite"
       ) AS "FueraDelPlazo"
FROM finales;
```

---

## 7. Dataset de prueba

`src/WebUI/Seeders/DbInitializer.cs` siembra **30 radicaciones** (`DEMO-001` … `DEMO-030`) en el primer arranque con la base vacía. Hay 30 inferencias, 189 documentos y 16 mensajes de outbox ya enviados.

| Caso borde | Cómo se cubre |
|---|---|
| Tres tipos de reclamación | `(ClaimType)(1 + i % 3)` alterna MuerteNatural, MuerteAccidental e IncapacidadTotalPermanente. |
| Expedientes incompletos | 13 casos (`3, 5, 7, 10, 13, 14, 15, 17, 20, 21, 25, 28, 30`) les falta el último soporte. |
| Documento que llegó pero no sirve | `DEMO-003` SARLAFT sin firma; `DEMO-013` cédula ilegible; `DEMO-017` certificación bancaria de otra persona. |
| Misma radicación dos veces | `DEMO-007` usa `ExternalEventId = EVT-REPETIDO-007`; `DEMO-009` reenvía un documento con el mismo hash y se ignora. |
| Modelo falló | `DEMO-019` tiene `InferenciaIA.Fallido`, `RequiereRevision` y no se inventa tipo. |
| Corrección humana | `i % 6 == 0` tiene confianza 0.41 y el analista invierte el tipo. |
| Reloj cerca de vencer | `DEMO-006` se completó para que su límite quede a 5 días; entra en riesgo 3 días antes. |
| Vencidos | Casos completados hace 35 y 28 días. |

Consultas rápidas:

```bash
docker exec pg-reclamaciones psql -U postgres -d reclamaciones -c 'select count(*) from "reclamaciones";'
docker exec pg-reclamaciones psql -U postgres -d reclamaciones -c 'select "PublicToken", "Estado", "FechaLimite" from "reclamaciones" order by "PublicToken";'
docker exec pg-reclamaciones psql -U postgres -d reclamaciones -c 'select "Tipo", count(*) from "eventos" group by "Tipo" order by 2 desc;'
```

Verificación de IA real:

```bash
docker exec pg-reclamaciones psql -U postgres -d reclamaciones -c 'select "Proveedor", "Modelo", "Exito", left(coalesce("Error", '"'"'(null)'"'"'), 60) as error, "LatenciaMs", "Confianza" from inferencias_ia order by "CreadoEn" desc limit 3;'
```

---

## 8. Automatización y operación

| Worker | Frecuencia | Función |
|---|---|---|
| `TelegramWorker` | 3 s (30 s sin token) | Procesa `/start {token}`, vincula el `chatId`, reenvía outbox pendiente y confirma al beneficiario. |
| `VencimientoWorker` | 1 min | Busca casos en riesgo sin aviso, marca `AvisoVencimiento` y encola mensaje en Telegram y Web. |
| `CorreoWorker` | 5 s (30 s sin credenciales) | Drena outbox `Canal = Web` por SMTP y marca `Enviado` o `Fallido`. |

Los avisos salen **antes** del vencimiento (`DiasAlerta = 3`), nunca después. El dedup por canal evita reenviar el mismo payload pendiente.

---

## 9. Seguridad

- El canal público no requiere usuario y usa anti-forgery en el POST.
- El panel está bajo `[Authorize]`, cookie con expiración de 8 h, `SlidingExpiration`, `LocalRedirect` y middleware de autenticación/autorización.
- Contraseña del analista de prueba hasheada con `PasswordHasher<Analyst>`.
- Archivos: nombre original saneado con `Path.GetFileName`, guardado como hash SHA-256 + `.bin`, límite de 10 MB por archivo, `nosniff` al servirlos.
- HTTPS/HSTS habilitados fuera de Development.
- Secretos nunca en el repositorio; solo en user-secrets o, en Azure, en configuración de App Service/Key Vault.

---

## 10. Despliegue en Azure

**Estado actual:** el código está listo para publicarse, pero **aún no hay despliegue en Azure ni infraestructura como código en el repo** (no hay Dockerfile, `docker-compose`, `*.pubxml`, Bicep ni GitHub Actions). Para llevarlo allá haría falta:

1. **Base de datos:** Azure Database for PostgreSQL Flexible Server (plan gratuito/B1 para la demo), con BD `reclamaciones` y reglas de firewall que permitan el egreso de App Service.
2. **App Service .NET 8 en Linux:** publicar con:
   ```bash
   dotnet publish src/WebUI/WebUI.csproj -c Release -o ./publish
   cd publish && zip -r ../webui.zip .
   az webapp deployment source config-zip \
     --resource-group TODO_RG --name TODO_APP --src ../webui.zip
   ```
3. **Configuración de la app** (en App Service → Configuration → Application settings, con doble guion bajo):
   ```text
   ConnectionStrings__Default        = Host=...;Port=5432;Database=reclamaciones;Username=...;Password=...
   IA__Proveedor                     = Gemini
   IA__Gemini__ApiKey                = <key>
   IA__Gemini__Modelo                = gemini-3.5-flash-lite
   IA__Gemini__BaseUrl               = https://generativelanguage.googleapis.com/v1beta/openai
   Telegram__BotToken                = <token>
   Telegram__BotUsername             = <bot>
   Email__Usuario                    = <usuario>
   Email__Password                   = <password>
   Email__From                       = <remitente>
   ```
   En un entorno real, estos valores deberían ser referencias a **Azure Key Vault** con identidad administrada, no texto plano.
5. **Red y egreso:** permitir salida HTTPS a `generativelanguage.googleapis.com`, `api.telegram.org` y el servidor SMTP; integrar la app en VNet si el PostgreSQL usa acceso privado.
6. **Dominio y TLS:** hostname personalizado con certificado administrado y forzar HTTPS.
7. **Concurrencia de workers:** hoy los tres `HostedService` viven dentro del proceso web. Si App Service escala a más de una instancia, habría que moverlos a **Azure Functions + Storage Queues** o crear un lock distribuido; si no, dejar `Always On` y una sola instancia para la demo.
8. **Observabilidad:** agregar un endpoint `/health` y Application Insights para enterarse de fallos antes que el usuario. Hoy la señal disponible es el estado `Fallido` en `outbox` y `Exito = false` en `inferencias_ia`.
9. **Persistencia de archivos:** `App_Data/uploads` es disco local de App Service; para producción conviene Azure Blob Storage y guardar la URL en `"documentos"."Ruta"`.

---


## 11. Accesos

El repositorio será **público**: basta con enviar `https://github.com/dvnber10/ReclamacionesIA.git`. Igualmente se confirma acceso a:

| Correo | Rol |
|---|---|
| `jlopez@globalseguros.co` | Líder del frente AIX |
| `npinzon@globalseguros.co` | Dirección de Agilidad Empresarial |
| `dgrijalba@globalseguros.co` | Vicepresidencia de Transformación Digital |

Panel de prueba: `https://reclamacionesia-bkfvdkf8cfd4c8e2.westus-01.azurewebsites.net/` · `analista@demo.local` / `Demo123*`.

---

## 13. Decisiones y limitaciones conocidas

- No se implementó WhatsApp: se sustituyó por Telegram opt-in y correo SMTP real, ambos gobernados por el outbox.
- No se implementaron MediatR, AutoMapper ni CQRS; el flujo explícito es más fácil de auditar en una prueba de este tamaño.
- El dedup de outbox se hace en aplicación; no hay constraint SQL sobre `PayloadJson`. Con varios procesos en paralelo haría falta un índice único o locker.
- Las rutas de archivo sembradas (`seed/{i}/{req}.pdf`) no existen en disco: los documentos del dataset se pueden inspeccionar en la BD, pero `/Panel/Ver` solo sirve archivos subidos de verdad.
- `ComplementacionService` vuelve a llamar a la IA pero no inserta una fila en `inferencias_ia`; la auditoría completa de esas reclasificaciones es un siguiente paso.
- La cuenta gratuita de Gemini tiene cupo 0 para modelos `pro` y los `flash` de última generación estuvieron saturados; por eso se fijó `gemini-3.5-flash-lite` y se dejó fallback auditado.
