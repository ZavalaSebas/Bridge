# Prompt para continuar el desarrollo de Bridge

> Este archivo existe para un solo propósito: ser pegado como primer mensaje a un
> modelo de IA nuevo (de cualquier capacidad) que va a seguir desarrollando este
> proyecto sin haber visto la conversación anterior. No es documentación del
> proyecto en sí — esa vive en los archivos que se listan abajo. Borralo cuando
> ya no lo necesites, no pasa nada si queda desactualizado con el tiempo, solo
> actualizalo si vas a volver a cambiar de sesión/modelo.

---

## Copiá todo lo que sigue como tu primer mensaje al modelo nuevo

```
Vas a continuar el desarrollo de un proyecto llamado Bridge. No es un proyecto
nuevo — ya tiene documentación completa y parte del código escrito. Antes de
escribir una sola línea de código, leé estos archivos EN ESTE ORDEN, completos,
no en diagonal:

1. DEVELOPMENT.md — reglas de trabajo, convenciones de código (MVVM, DI,
   logging, async), estructura del proyecto, y una tabla llamada "Workflow
   Rules" con reglas que son obligatorias, no sugerencias.
2. PLAN.md — en qué fase está el proyecto ahora mismo, qué se hizo, qué sigue,
   y qué decisiones siguen abiertas a propósito (mirá la sección "Risk
   Register").
3. ARCHITECTURE.md — los ADRs (Architecture Decision Records). Cada decisión
   de diseño no obvia tiene un ADR explicando el porqué. Si vas a tomar una
   decisión de arquitectura, primero fijate si ya hay un ADR sobre eso.
4. PROJECT_FOUNDATION.md — esta es la más larga (3000+ líneas) y no hace falta
   leerla entera de una. Es la referencia verificada contra el código fuente
   real de Playnite (el proyecto que Bridge reescribe) de cómo funciona cada
   pieza. Al final de este prompt hay un índice de su sección 28 — cuando
   vayas a implementar algo específico (por ejemplo "cómo trackea Playnite el
   tiempo jugado"), buscá la subsección correspondiente en vez de adivinar o
   inventar el comportamiento.
5. Bridge.Core/ — el código que ya existe. Empezá por Bridge.Core/Entities/Game.cs.

REGLA MÁS IMPORTANTE: este proyecto tuvo un problema antes en su desarrollo
donde un modelo anterior agregó funcionalidad que nadie pidió (un framework de
UI específico, una fuente de metadata específica, features nuevas) basándose
en una mala lectura de lo que el usuario quería a futuro, no a corto plazo. El
usuario tuvo que pedir que se revirtiera todo. NO repitas ese error: si no
está en PLAN.md como parte del scope actual, no lo agregues sin preguntar
primero, aunque te parezca una buena idea o aunque aparezca mencionado como
posibilidad futura en algún lado. Cuando tengas dudas de alcance, preguntá en
vez de asumir.

Contame en qué querés que trabajemos y seguimos desde ahí.
```

---

## PRÓXIMO PASO CONCRETO (lo más reciente, léelo primero)

El usuario descargó el repo real de extensiones de Playnite en
`D:\Proyectos\PlayniteExtensions-master\PlayniteExtensions-master` y pidió
investigar puntualmente dos cosas — documentado en `PROJECT_FOUNDATION.md`
§28.26, y **la detección de Steam ya se implementó y se probó contra el
Steam real de esta máquina** (no es solo documentación, hay código):

1. **Importar Steam — YA IMPLEMENTADO Y PROBADO CON DATOS REALES**:
   `Bridge.Import` (proyecto nuevo, ver `ARCHITECTURE.md` ADR-11) tiene
   `SteamPaths` (lee el registro), `VdfParser` (parser VDF hecho a mano, sin
   depender de `SteamKit2`), y `SteamLibraryImporter`. Botón "Import Steam
   Library" en `MainWindow`, comando `ImportSteamLibraryCommand` en
   `MainViewModel` — dedupea por `(ExternalId, SourceId)`, en un re-import
   solo sincroniza `IsInstalled`/`InstallDirectory`, no toca campos editados
   a mano. **Probado contra el Steam real del usuario: encontró 29 juegos
   reales correctamente, en dos carpetas de biblioteca distintas (C: y
   D:\SteamLibrary), excluyó el redistribuible que no es un juego.** También
   probado que una segunda importación no duplica nada (0 nuevos, 29
   actualizados). 12 tests sintéticos nuevos en `Bridge.Tests/Import/`
   (`VdfParserTests`, `SteamLibraryImporterTests`) para que funcione en
   máquinas sin Steam instalado (como CI). Total: 38/38 tests pasando.
   **Pendiente**: Epic Games no se investigó (no estaba en el repo de
   extensiones revisado).

2. **Cómo hace Playnite el metadata de IGDB sin pedir credenciales**: confirmado
   que el addon oficial NO habla con IGDB directo — le pega a un backend
   propio del autor de Playnite (`https://api2.playnite.link/api/`, ver
   `IgdbClient.cs`), que tiene las credenciales reales guardadas del lado del
   servidor. Bridge sigue con el modelo de "cada usuario trae su propia
   credencial de Twitch" (más simple, sin servidor que mantener) — esto NO
   cambió, fue solo para responder la pregunta del usuario con evidencia real
   en vez de una suposición. Si el usuario pide después migrar a un modelo de
   proxy propio, es un cambio de infraestructura grande, no una tarde de
   trabajo — avisale eso antes de empezar.

**Nota de contexto de sesión**: esto se investigó con el usuario avisando
"queda muy poco uso, está a punto de quedarse sin" — por eso se priorizó
documentar bien sobre implementar apurado. Si arrancás una sesión nueva y el
usuario no menciona límite de tiempo, es buen momento para ofrecer implementar
el import de Steam ahora que ya está todo el análisis hecho.

---

## Estado exacto del proyecto al momento de escribir esto (2026-08-05)

**Existe, compila, y se probó en tiempo de ejecución** (`dotnet build Bridge.slnx`
→ 0 errores, 0 warnings):
- `Bridge.slnx` — solución, layout plano, sin carpeta `src/`
- `Bridge/` — proyecto WPF host (`net10.0-windows`), TODAVÍA es solo el scaffold
  default de Visual Studio (`App.xaml`, `MainWindow.xaml`) — no hay UI real
- `Bridge.Core/` — modelo de dominio completo: entidades, enums, DTO de import,
  contratos de repositorio. Ver `DEVELOPMENT.md` sección "`Bridge.Core` — qué hay
  adentro" para el árbol completo de archivos.
- `Bridge.Storage/` — implementación real de persistencia con SQLite vía EF Core
  (`BridgeDbContext`, `Repository<T>`, `GameRepository`). Se verificó contra un
  archivo SQLite real (no solo que compila): crear un juego con listas/objetos
  anidados, cerrar, reabrir en un contexto nuevo, y todo vuelve idéntico. Ver
  `DEVELOPMENT.md` sección "`Bridge.Storage` — qué hay adentro".

- `Bridge/` ya tiene `Config.cs` (con `AppDataPath`) y `App.xaml.cs` arma el
  contenedor de DI y crea la base de datos al arrancar. Se probó lanzando el
  `Bridge.exe` real (no solo `dotnet build`) y confirmando que `bridge.db` se
  crea con las 14 tablas correctas.
- `Bridge/ViewModels/MainViewModel.cs` carga `Games` desde `IGameRepository`, y
  ya tiene `AddGameCommand`/`DeleteGameCommand` funcionando de verdad contra la
  base de datos real (no un mock). `MainWindow.xaml` tiene lista + detalle +
  campo de alta + botón de borrar, todo bindeado (sin `LibraryView`/
  `GameDetailsView` separados todavía, va todo inline en `MainWindow.xaml`).
  Se probó lanzando `Bridge.exe` real varias veces (vacío y con datos) — el
  proceso queda `Responding: True` con el título correcto. **No se verificó
  visualmente** (no hay herramienta de captura de pantalla para apps de
  escritorio nativas en este entorno) — si querés confirmar que se ve y se usa
  bien, abrilo vos y probá el botón Add.

- Editar juegos existentes ya funciona: `Name`/`Description` editables +
  checkboxes de `Favorite`/`Hidden` + botón Save (`SaveGameCommand`), persiste
  de verdad en `bridge.db`. `Game` sigue siendo un POCO plano a propósito (sin
  `INotifyPropertyChanged`) — el refresco de la lista tras guardar usa un
  truco manual (re-set del item en el `ObservableCollection`), documentado en
  el comentario de `SaveGame()` en `MainViewModel.cs`.

- **Jugar y trackear tiempo también funciona de verdad**:
  `Bridge/Services/GameLauncher.cs` lanza un `GameAction` y hace polling hasta
  que el proceso termina, igual que el mecanismo real de Playnite (§28.9-28.10
  — sin evento `Process.Exited`, un loop con `Task.Delay`). Probado contra un
  proceso real de 3 segundos: la duración de sesión se midió bien,
  `PlayCount`/`LastActivity`/`PlaytimeSeconds` se actualizan bien. La UI tiene
  un campo "Set Play Action" (solo ruta de ejecutable) y un botón "▶ Play".
  **Ojo**: a propósito es más angosto que Playnite — solo soporta acciones
  tipo `File` (no Url/Emulator/Script), y solo trackea el PID exacto lanzado
  (sin caminar el árbol de procesos, sin tracking por directorio/nombre), así
  que el caso "el launcher lanza el juego real y se cierra" (tipo Steam)
  todavía no se maneja bien. Está documentado en el comentario de
  `GameLauncher.cs`.

- **Fase 4 (estadísticas) también cerrada**: `Bridge/Statistics/LibraryStatistics.cs`
  calcula todo al vuelo (total, instalados, favoritos, ocultos, playtime total,
  top jugados) desde la lista de juegos actual — igual que Playnite real, sin
  entidad persistida. Se ve en la barra de estado de `MainWindow`. Probado con
  datos conocidos, todos los conteos dieron exactos. `TopPlayed` se calcula
  pero todavía no se muestra en ningún lado de la UI.

- **Fase 6 (emulación) arrancó y el mecanismo central ya funciona**:
  `Bridge/Services/RomScanner.cs` escanea una carpeta por extensión (sin CRC/DAT
  — eso es scope futuro) y crea `Game`+`GameRom`+`GameAction(Emulator)`;
  `GameLauncher` ahora también sabe lanzar por emulador, sustituyendo
  `{RomPath}` en los argumentos. Probado de punta a punta con un "emulador"
  de mentira (cmd.exe): escaneo encontró los archivos correctos, lanzamiento
  sustituyó bien la ruta, tracking funcionó.
- **Ya hay formulario para configurar el emulador**: botón "Configure
  Emulator..." en `MainWindow` abre `EmulatorSetupWindow`. Editar dos veces
  no duplica el registro (probado). **Falta**: detección automática de
  emuladores ya instalados — hoy el usuario tiene que tipear la carpeta de
  instalación y el ejecutable a mano.

- **`Bridge.Tests` ya existe**: 17 tests, todos pasando (`dotnet test
  Bridge.slnx -c Release`). Cubre el round-trip real de `Game` contra SQLite,
  el dedup de `GetOrCreateByName`, `RomScanner`, y `LibraryStatistics`. Se
  encontró y arregló un bug real en la limpieza de los tests (pooling de
  conexiones SQLite dejaba el archivo bloqueado) — no era cosmético, hubiera
  hecho los tests inestables en CI.

**Ojo, esto es importante**: `Bridge.Import` y `Bridge.Emulation` NUNCA se
crearon como proyectos separados — esa lógica (`RomScanner`, la mitad de
emulador de `GameLauncher`, alta/importación manual) terminó viviendo adentro
de `Bridge` (el proyecto de la app) directamente. Es una desviación real del
plan original, no un error silencioso — está marcada explícitamente en
`PLAN.md` → Project Structure para que se decida a propósito antes de la
Fase 9: o se actualiza el plan para reflejar la realidad, o se separa esa
lógica en los proyectos que originalmente se pensaron.

- **Fase 5 (metadata) desbloqueada y construida**: el usuario confirmó IGDB
  como fuente (ver `ARCHITECTURE.md` ADR-10). `Bridge.Metadata` se armó como
  proyecto separado de verdad esta vez (no repetí la desviación de meterlo
  adentro de `Bridge`). Tiene `IgdbMetadataProvider`/`IgdbAuthClient`
  (OAuth2 vía Twitch, caché de token), botón "Download Metadata (IGDB)" en la
  UI que aplica descripción/fecha/portada/géneros al juego seleccionado.
  **Límite real e importante**: no tengo credenciales de IGDB, así que el
  flujo completo (auth + búsqueda + mapeo + manejo de errores) está probado
  contra un `HttpMessageHandler` falso con 9 tests — no contra los
  servidores reales de IGDB. La app arranca bien sin credenciales
  configuradas (probado), pero la primera prueba real de verdad es cuando
  vos cargues un Client ID/Secret real en "IGDB Settings..." y lo pruebes.

- **Fase 8 (empaquetado) hecha y con hallazgos reales**: el comando de
  publish documentado en el README, tal cual, generaba `Bridge.exe` MÁS 6
  DLLs nativas al lado (de WPF y de SQLite) — no era un solo archivo de
  verdad. Se arregló agregando `IncludeNativeLibrariesForSelfExtract=true` a
  `Bridge.csproj` y `DebugType=none` vía un `Directory.Build.props` nuevo en
  la raíz (aplica a todos los proyectos). Ahora sí: un solo `Bridge.exe` de
  148MB, nada más. Probé `PublishReadyToRun=true` para el arranque — salió
  MÁS LENTO (2671ms vs ~2000ms promedio) y más pesado, así que no se activó.
  Medí el arranque real 3 veces desde una carpeta aislada (no la de
  desarrollo): ~2 segundos hasta ventana visible, ~140-147MB de RAM en reposo.

**No existe todavía**: `Bridge.Import`/`Bridge.Emulation` como proyectos
separados (ver nota arriba), caché de resultados de metadata (hoy re-descarga
siempre), `SkipExistingValues`, descarga/caché local de imágenes (la portada
queda como URL cruda), mapeo de Developers/Publishers desde IGDB, acciones
tipo Url/Script, tracking por árbol de procesos/directorio/nombre, detección
automática de emuladores instalados. Ver `PLAN.md` → Development Phases para
el estado fase por fase.

**Ojo con esto**: `%LOCALAPPDATA%\Bridge\` coincidía con datos reales de tu
proyecto Bridge viejo (el de emuladores). Se respaldó (no se borró) a
`%LOCALAPPDATA%\Bridge_OLD_BACKUP_1785967008\` antes de crear el `bridge.db`
nuevo. Está documentado en `DEVELOPMENT.md` → Known Limitations.

**Decisión ya tomada**: SQLite vía EF Core (`ARCHITECTURE.md` ADR-4, estado
`Accepted`). Se decidió avanzando con la recomendación ya documentada bajo
presión de tiempo real (el usuario estaba por quedarse sin uso), no
preguntando de nuevo — quedó marcado bien visible en el chat en su momento
para que se pudiera corregir al toque si no era lo que quería. No hace falta
volver a preguntar esto salvo que el usuario diga explícitamente que quiere
cambiarlo.

---

## Índice de PROJECT_FOUNDATION.md §28 (referencia verificada contra el código real de Playnite)

No leas las 3000 líneas de una. Buscá la subsección que corresponde a lo que
estás implementando:

| Sección | Tema |
|---|---|
| §28.1 | Modelo de datos — campos exactos de `Game`, `GameMetadata`, entidades de referencia |
| §28.2 | `GameDatabase` — algoritmo real de import, persistencia (LiteDB), almacenamiento de archivos |
| §28.3 | `MetadataDownloader` — algoritmo de resolución de metadata campo por campo |
| §28.4 | Emulación — detección de emuladores, escaneo de ROMs, matching por CRC/serial/nombre |
| §28.5 | ViewModels — qué expone `GameDetailsViewModel` y `StatisticsViewModel` |
| §28.8 | `GameAction` y `GameRom` — estructura exacta |
| §28.9 | Ejecución real de Play/Install/Uninstall |
| §28.10 | Tracking de playtime — el mecanismo real (por polling, no por evento) |
| §28.11 | Verificación de estado de instalación |
| §28.12 | `PlayniteSettings` — modelo de configuración global |
| §28.13 | Algoritmo real de aplicación de filtros |
| §28.14 | Edición múltiple — mitad de lectura (`GameTools`) |
| §28.18 | Bootstrap real de la aplicación (sin contenedor de DI) |
| §28.19 | Sistema de temas |
| §28.20 | Contrato real de `LibraryPlugin` |
| §28.21 | Edición múltiple — mitad de escritura (`GameEditViewModel`) |
| §28.22 | Backup/restore de biblioteca |
| §28.23 | Wizard de primer arranque |
| §28.6, §28.16, §28.24 | Hallazgos sorprendentes de cada pasada de investigación — vale la pena leerlos, tienen trampas ya identificadas |
| §28.26 | **Nuevo**: cómo detecta Steam sus juegos instalados (VDF real, archivo por archivo) y confirmación de que IGDB en Playnite va vía proxy propio, no credenciales del usuario |

---

## Cómo pedirle trabajo a un modelo de baja capacidad (para el usuario, no para el modelo)

- Pedile cambios chicos, un archivo o una clase a la vez, no "armá todo Bridge.Storage".
- Después de cada cambio de código, pedile que corra `dotnet build Bridge.slnx` y
  te muestre el resultado — no confíes en que "ya debería compilar".
- Si el modelo propone algo que no está en `PLAN.md`, frenalo ahí — es
  exactamente el error que ya pasó una vez en este proyecto.
- Si tenés dudas de si el modelo entendió el contexto, pedile que te resuma en
  sus propias palabras en qué fase está el proyecto y qué sigue, antes de que
  toque código.
