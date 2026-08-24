> **Archivado en 1.0 — 2026-08-24:** Este documento es histórico. Todas las fases y módulos descritos (Fases 0–9, §16–§26) están implementados en Bridge 1.0 — ver docs/PLAN.md y docs/CHANGELOG.md. No usar como backlog activo.

DOCUMENTACION DE CONVERSACION â€” ARCHIVO DE INICIO DEL PROYECTO

Fecha: 2026-08-05
Idioma: es-ES

> **Nota de encuadre:** Este documento es **material de archivo** de cuando Bridge se concibiÃ³ por primera vez. Registra un anÃ¡lisis profundo de [Playnite](https://playnite.link/) como **inspiraciÃ³n original** para entender el dominio (biblioteca unificada, importaciÃ³n, metadatos, emulaciÃ³n). **No es una especificaciÃ³n de Bridge** â€” Bridge tiene arquitectura, cÃ³digo e interfaz propios e independientes. No usar este documento para comparar implementaciones ni como contrato de desarrollo.

1. OBJETIVO GENERAL DISCUTIDO

La conversacion se centro en comprender en profundidad como funciona un gestor de biblioteca unificada (Playnite fue el punto de partida e inspiracion original) para replantear el proyecto desde cero con una arquitectura mas simple y moderna. La idea principal del usuario fue:

- replicar el comportamiento funcional de Playnite,
- modernizarlo con C# y .NET 10,
- usar WPF como base inicial,
- luego mejorar visualmente con WPF UI o una libreria similar,
- empaquetarlo como single-file self-contained,
- usar una base de datos local ligera como SQLite o LiteDB,
- eliminar el sistema de plugins en la nueva version inicial,
- mantener cache, virtualizacion, sincronizacion incremental y automatizacion plug & play,
- soportar juegos de bibliotecas externas, juegos manuales y emulados,
- entender profundamente metadatos, vistas, estadisticas, temas y estructura interna.

2. CONTEXTO DEL PROYECTO ORIGINAL

El proyecto actual Playnite esta organizado sobre .NET Framework 4.8 y WPF. La solucion contiene varios proyectos:

- Playnite: nucleo principal,
- Playnite.DesktopApp: interfaz de escritorio,
- Playnite.FullscreenApp: interfaz fullscreen,
- Playnite.SDK: contratos para plugins,
- Tools: utilidades,
- Tests: pruebas.

La arquitectura original es potente, pero compleja, muy modular y fuertemente orientada a extensibilidad por plugins.

3. FLUJO DE ARRANQUE DE LA APLICACION

Se identificaron dos entradas principales:

A. Escritorio:
- archivo Playnite.DesktopApp/ProgramEntry.cs,
- lee argumentos,
- ajusta rutas de usuario,
- crea carpetas necesarias,
- activa perfiles JIT,
- valida entorno (Windows 7/8 no soportados, rutas temporales o invalidas),
- muestra splash screen,
- configura logging,
- crea DesktopApplication y ejecuta la app.

B. Fullscreen:
- archivo Playnite.FullscreenApp/FullscreenApplication.cs,
- inicializa SDL,
- audio,
- controles,
- navegacion fullscreen,
- ventanas especificas,
- usa la misma base de aplicacion pero con comportamientos visuales y de entrada distintos.

4. NUCLEO DE DATOS Y PERSISTENCIA

La clase central es GameDatabase.

Responsabilidades principales:
- mantener colecciones de juegos, plataformas, generos, companias, tags, categorias, series, age ratings, regiones, sources, features, emulators, scanners, filtros, exclusiones y estados de completado,
- administrar archivos adjuntos y cache de imagenes,
- abrir, inicializar y migrar la base de datos,
- cargar elementos usados,
- gestionar persistencia local por carpetas y archivos,
- mantener relaciones normalizadas por IDs.

La persistencia se apoya en:
- database.json para configuracion,
- carpetas por entidad,
- carpeta files para assets,
- colecciones separadas para cada tipo de dato.

[CORRECCION VERIFICADA CONTRA CODIGO FUENTE, ver S28 para el detalle completo: cada
"carpeta por entidad" arriba mencionada NO es un directorio de archivos JSON sueltos.
Es el nombre base de un unico archivo LiteDB v4 (ej. la coleccion "games" es en
realidad games.db, un archivo LiteDB con un documento BSON por juego). Solo
database.json (version de esquema) y los assets binarios bajo files/ son archivos
sueltos reales. Ver S28.2.]

5. MODELO DE DATOS PRINCIPAL

Se identifico que Playnite separa claramente el juego local del metadato descargado.

A. Game:
- representa el juego ya integrado en la biblioteca,
- contiene nombre, IDs externos, estado de instalacion, ruta, playtime, fechas, tags, generos, empresas, puntuaciones, enlaces, notas, icono, portada y fondo.

B. GameMetadata:
- representa datos importados o enriquecidos,
- puede contener texto, listas de propiedades, scores, imagenes, tamanos, enlaces, fecha de lanzamiento y otras propiedades.

C. MetadataProperty:
- se usa para representar datos normalizados y referencias a entidades de la base.

D. Entidades auxiliares:
- LinkItem,
- MetadataImage,
- GameFilter,
- GameStatistics,
- Emulator,
- LibrarySource,
- EmulatorProfile.

[CORRECCION VERIFICADA CONTRA CODIGO FUENTE, ver S28.1: la clase se llama Link, no
LinkItem. No existe MetadataImage â€” tanto GameMetadata.Icon como CoverImage y
BackgroundImage usan la misma clase MetadataFile. No existe una clase GameStatistics
dedicada â€” las estadisticas se calculan en tiempo real dentro de StatisticsViewModel
(clases internas GameStats/BaseStatInfo). "GameFilter" corresponde a FilterPresetSettings
(el filtro persistido se llama FilterPreset). "LibrarySource" no es una clase â€” la
identidad de biblioteca vive directo en Game como dos campos: GameId (string) y
PluginId (Guid); GameSource es una entidad separada, un tag de origen editable por el
usuario, no la identidad del plugin. Ver S28.1 para las listas de campos reales de
cada clase.]

6. IMPORTACION DE JUEGOS DESDE BIBLIOTECAS EXTERNAS

El flujo principal esta en GameDatabase.ImportGames(LibraryPlugin library, CancellationToken cancelToken, PlaytimeImportMode playtimeImportMode).

Dos caminos:

A. Importacion personalizada:
- si el plugin marca HasCustomizedGameImport,
- el propio plugin decide como importar usando ImportGames(...).

B. Importacion estandar:
- se llama a GetGames(...),
- se recorre cada elemento,
- se aplican exclusiones,
- se evita duplicidad por GameId + PluginId,
- si el juego no existe, se crea,
- si existe, se actualiza de forma incremental.

Campos actualizados en juegos existentes:
- IsInstalled,
- InstallDirectory,
- Playtime,
- LastActivity,
- InstallSize,
- CompletionStatus.

La politica de playtime depende del modo configurado y se evita sobrescribir datos utiles sin necesidad.

7. IMPORTACION Y NORMALIZACION DE METADATOS

La clase MetadataDownloader fue clave para entender como Playnite arma el enriquecimiento de datos.

Flujo general:
- procesa juegos uno por uno,
- recarga el juego desde la base para evitar usar una copia obsoleta,
- resuelve cada campo segun configuracion,
- respeta SkipExistingValues,
- llama a ProcessField(...),
- guarda imagenes localmente si corresponde,
- actualiza el juego solo si hubo cambios reales.

Las fuentes de metadatos son:
- LibraryMetadataProvider: metadatos asociados a la biblioteca del juego,
- MetadataPlugin + OnDemandMetadataProvider: proveedores externos por campo.

ProcessField(...) hace:
- recorrer fuentes configuradas,
- omitir fuentes invalidas,
- reutilizar datos ya descargados,
- verificar soporte de campo,
- pedir solo el dato requerido.

Campos soportados observados:
- Name,
- Genres,
- ReleaseDate,
- Developers,
- Publishers,
- Tags,
- Description,
- Links,
- CriticScore,
- CommunityScore,
- Icon,
- CoverImage,
- BackgroundImage,
- Features,
- AgeRating,
- Series,
- Region,
- Platform,
- InstallSize.

8. SISTEMA DE PLUGINS Y SDK

Se analizo ExtensionFactory como el motor de carga de extensiones.

Funciona de la siguiente forma:
- detecta manifests,
- valida IDs y compatibilidad,
- carga por reflexion,
- crea instancias de LibraryPlugin, MetadataPlugin, GenericPlugin y scripts,
- mantiene listas de plugins cargados y fallidos,
- maneja compatibilidad de SDK.

El SDK define:
- LibraryPlugin: fuente de juegos, importacion y metadatos de biblioteca,
- MetadataPlugin: proveedor de metadatos por campo,
- OnDemandMetadataProvider: API de lectura puntual de campos.

Conclusion importante:
- el sistema de plugins es muy flexible,
- pero para la nueva version se propuso eliminarlo inicialmente para simplificar y reducir complejidad.

9. EMULADORES Y JUEGOS EMULADOS

Se profundizo bastante en este subsistema porque el usuario queria entender como tratar emuladores y ROMs en la reescritura.

Componentes principales:

A. GameScannerConfig:
- define el escaneo de una carpeta emulada,
- incluye EmulatorId, EmulatorProfileId, Directory, InGlobalUpdate, ExcludeOnlineFiles, UseSimplifiedOnlineFileScan, ImportWithRelativePaths, ScanSubfolders, ScanInsideArchives, ExcludedFiles, ExcludedDirectories, OverridePlatformId y configuraciones de exclusiones.

B. GameScannersCollection:
- mantiene la configuracion de escaneo,
- guarda settings de CRC y tipos excluidos.

C. EmulationDatabase:
- usa bases .db por sistema,
- permite buscar juegos por CRC, Serial, RomName y RomNamePartial,
- sirve para identificar ROMs con coincidencias robustas.

D. EmulatorScanner:
- busca emuladores instalados,
- valida perfiles,
- usa expresiones regulares para detectar archivos de arranque o instalacion,
- comprueba archivos requeridos,
- agrupa emuladores encontrados,
- normaliza rutas si pertenecen al directorio del programa.

Flujo emulado:
1. detectar emulador,
2. asociar perfil,
3. escanear carpeta de ROMs,
4. excluir archivos o carpetas,
5. identificar el ROM,
6. crear o actualizar juego,
7. vincular con plataforma y datos locales.

Se concluyo que esta parte conviene mantener conceptualmente, pero simplificandola en la nueva version.

10. VISTAS DE BIBLIOTECA Y PRESENTACION DE JUEGOS

Se estudiaron las tres vistas principales de Playnite:

A. List:
- vista compacta,
- orientada a eficiencia,
- seleccion multiple,
- scroll suave.

B. Grid:
- portadas grandes,
- zoom configurable,
- virtualizacion,
- cambio de panel segun agrupacion,
- reinicio del scroll al cambiar filtros.

C. Details:
- lista detallada,
- mas informacion por juego,
- scroll virtualizado,
- orientada a inspeccion profunda.

El item visual individual se implementa con GameListItem y puede tener:
- icono,
- portada,
- boton de jugar,
- boton de informacion,
- menu contextual,
- doble click para iniciar.

11. DETALLE DEL JUEGO

El componente GameOverview y GameDetailsViewModel definen la experiencia rica de detalle.

Se identificaron campos visibles como:
- playtime,
- install size,
- install directory,
- last played,
- added,
- recent activity,
- completion status,
- library source,
- platform,
- genres,
- developers,
- publishers,
- release date,
- categories,
- tags,
- features,
- links,
- description,
- notes,
- age rating,
- series,
- region,
- source,
- version,
- critic score,
- community score,
- user score,
- cover,
- icon,
- background.

Tambien se detectaron acciones relevantes:
- jugar,
- instalar,
- verificar instalacion,
- verificar ejecucion,
- editar juego,
- abrir enlaces,
- filtrar por metadato al hacer click.

Conclusiones para la nueva version:
- conviene replicar la idea de un panel rico de detalle,
- pero simplificando estructura y numero de campos iniciales.

12. ESTADISTICAS

StatisticsViewModel calcula metricas generales y filtradas.

Se observaron datos como:
- total de juegos,
- instalados,
- no instalados,
- ocultos,
- favoritos,
- tiempo total jugado,
- tiempo medio,
- tamanio total instalado,
- top jugados,
- distribucion por proveedor,
- distribucion por filtros.

Filtros soportados:
- proveedor,
- generos,
- features,
- tags,
- plataformas,
- desarrolladores,
- publishers,
- categorias,
- ano de lanzamiento,
- series,
- age ratings,
- regiones,
- source,
- estado de completado,
- estado de instalacion.

Para la nueva version se propuso empezar con estadisticas basicas y luego ampliar.

13. TEMAS Y PERSONALIZACION

ThemeManager gestiona el sistema de temas de Playnite.

Puntos relevantes:
- tema actual,
- tema por defecto,
- verificacion de version de API de tema,
- carga de recursos XAML,
- cambio de cursor,
- prompts de botones para fullscreen.

El sistema se basa en ResourceDictionary y sustitucion de recursos del tema base.

Conclusiones:
- es potente pero complejo,
- en la reescritura conviene arrancar con un tema unico y evolucionar despues hacia WPF UI.

14. GESTION DE ARCHIVOS Y ASSETS

Se entendio que Playnite no guarda imagenes como blobs en el registro principal, sino como archivos locales gestionados por la base.

Funciones relevantes:
- AddFile(...): acepta archivos locales o URLs, descarga y convierte si hace falta,
- GetFileStoragePath(...): crea la ubicacion por entidad,
- GetFileAsImage(...): permite usar assets en la UI.

Esto refuerza la idea de que la nueva version debe mantener caches y assets locales.

15. EDICION MULTIPLE

GameTools.GetMultiGameEditObject(...) prepara un objeto dummy para edicion masiva.

La logica detecta:
- valores comunes entre juegos,
- valores distintos,
- campos que se pueden editar en bloque.

Se considero una idea util a conservar en el futuro.

16. ARQUITECTURA PROPUESTA PARA LA NUEVA VERSION

Se propuso una nueva arquitectura simple y sin sistema de plugins al inicio.

Proyectos sugeridos:
- GameLibrary.App,
- GameLibrary.Core,
- GameLibrary.Storage,
- GameLibrary.Import,
- GameLibrary.Metadata,
- GameLibrary.Emulation,
- GameLibrary.Tests.

Responsabilidades:
- App: WPF y ViewModels,
- Core: entidades, reglas, contratos,
- Storage: SQLite o LiteDB, repositorios y files,
- Import: importacion manual y desde bibliotecas,
- Metadata: proveedores, descarga y cache,
- Emulation: detection, scanning y matching,
- Tests: validacion de todo.

17. CLASES BASE DISEÃ‘ADAS

Se generaron plantillas iniciales de clases como base de arranque:

- Game,
- GameMetadata,
- Emulator,
- LibrarySource,
- GameFilter,
- GameStatistics,
- LinkItem,
- MetadataImage,
- IGameRepository,
- IGameImporter,
- IMetadataProvider,
- IEmulatorScanner,
- GameMergeService,
- GameFilterService,
- StatisticsService,
- AppDbContext,
- GameRepository,
- FileStorageService,
- MetadataCacheRepository,
- ManualGameImporter,
- LibraryImporter,
- GameSyncService,
- DuplicateResolver,
- MetadataDownloadService,
- MetadataFieldResolver,
- MetadataCacheService,
- EmulatorDetector,
- RomScanner,
- RomMatchService,
- MainViewModel,
- GameDetailsViewModel,
- StatisticsViewModel,
- ObservableObject.

Se acordaron propiedades base para juegos, filtros, estadisticas y almacenamiento.

18. ESTRUCTURA DE CARPETAS SUGERIDA PARA EL NUEVO REPOSITORIO

La estructura base recomendada fue:

GameLibrary.sln
src/
  GameLibrary.App/
  GameLibrary.Core/
  GameLibrary.Storage/
  GameLibrary.Import/
  GameLibrary.Metadata/
  GameLibrary.Emulation/
tests/
  GameLibrary.Tests/

Tambien se propuso una organizacion interna por carpetas:
- Domain,
- Contracts,
- Services,
- Repositories,
- Files,
- Importers,
- Sync,
- Providers,
- Detection,
- Scanning,
- Views,
- ViewModels,
- Controls.

19. FLUJO DE MIGRACION PROPUESTO

Orden recomendado:

1. crear Core,
2. crear Storage,
3. crear UI minima,
4. agregar importacion manual,
5. agregar metadatos,
6. agregar emulacion,
7. agregar estadisticas,
8. mejorar la UI con WPF UI,
9. empaquetar como single-file self-contained.

20. MVP PROPUESTO

El primer objetivo funcional minimo fue:
- abrir la app,
- guardar juegos,
- cargar lista,
- ver detalle,
- editar,
- agregar manualmente,
- calcular estadisticas basicas,
- importar ROMs simples,
- cachear imagenes.

21. DECISIONES IMPORTANTES DEL DISEÃ‘O

Se acordaron varias decisiones de arquitectura:

- no empezar con plugins,
- no crear fullscreen separado inicialmente,
- no construir un sistema de temas complejo desde el principio,
- mantener solo un nucleo simple,
- conservar caches, virtualizacion e importacion incremental,
- no mezclar logica de negocio con UI,
- hacer la modularidad interna solo para desarrollo, no para extensibilidad runtime.

22. RIESGOS IDENTIFICADOS

Se identificaron riesgos importantes para la reescritura:

- intentar copiar demasiada complejidad desde el inicio,
- mezclar UI y dominio,
- perder el control de identificadores de origen,
- no cachear metadatos e imagenes,
- hacer el sistema de emulacion demasiado generico,
- introducir plugins antes de estabilizar el nucleo,
- no validar por fases.

23. CRITERIOS DE EXITO PARA LA NUEVA VERSION

La nueva version sera satisfactoria si consigue:
- arrancar rapido,
- usar poca memoria,
- guardar datos localmente de forma confiable,
- importar bibliotecas y ROMs correctamente,
- mostrar lista y detalle fluidos,
- mantener estadisticas coherentes,
- cachear recursos,
- ser facil de mantener,
- permitir evolucion visual,
- no arrastrar complejidad innecesaria.

24. CONCLUSION GENERAL

El analisis completo del proyecto original permitio concluir que Playnite es, en esencia, un orquestador de:
- biblioteca de juegos,
- importacion desde fuentes externas,
- normalizacion de metadatos,
- soporte emulado,
- estadisticas,
- presentacion visual de la biblioteca,
- y persistencia local robusta.

La reescritura planteada debe conservar la esencia funcional, pero simplificar drÃ¡sticamente la arquitectura:
- .NET 10,
- WPF primero,
- WPF UI despues,
- SQLite o LiteDB,
- single-file self-contained,
- sin plugins al inicio,
- con importacion, metadatos, emulacion, estadisticas y UI basica como base.

25. MODULOS RECOMENDADOS PARA LA NUEVA VERSION

La forma mas practica de encarar la reescritura es dividirla en modulos internos claros, pero sin convertir la aplicacion en un sistema de plugins. La modularidad aqui es para desarrollo, mantenimiento y verificacion, no para extensibilidad runtime.

25.1 Modulo Core
Responsabilidad:
- entidades del dominio,
- reglas de negocio,
- validaciones,
- filtros,
- estadisticas basicas,
- contratos internos.

Debe contener:
- Game,
- GameMetadata,
- Emulator,
- LibrarySource,
- GameFilter,
- GameStatistics,
- LinkItem,
- MetadataImage,
- enums y contratos.

25.2 Modulo Storage
Responsabilidad:
- persistencia local,
- lectura y escritura de datos,
- archivos adjuntos,
- cache de imagenes,
- migraciones,
- consultas eficientes.

Debe contener:
- repositorios,
- contexto de base de datos,
- servicios de archivos,
- cache persistente,
- adaptadores para SQLite o LiteDB.

25.3 Modulo Import
Responsabilidad:
- importacion manual,
- importacion desde bibliotecas externas,
- sincronizacion incremental,
- deduplicacion,
- merge de datos.

Debe contener:
- importadores,
- resolucion de duplicados,
- sincronizacion con fuente,
- politicas de actualizacion.

25.4 Modulo Metadata
Responsabilidad:
- descarga de metadatos,
- resolucion por campo,
- cache de metadatos,
- guardado de imagenes,
- normalizacion de respuestas.

Debe contener:
- servicios de descarga,
- resolucion de campos,
- cache,
- proveedores web o locales.

25.5 Modulo Emulation
Responsabilidad:
- deteccion de emuladores,
- escaneo de ROMs,
- identificacion por CRC, serial o nombre,
- asociacion con plataformas,
- importacion emulada.

Debe contener:
- detector de emuladores,
- scanner de ROMs,
- matching,
- lectura de checksum,
- perfiles simples.

25.6 Modulo App
Responsabilidad:
- UI WPF,
- ViewModels,
- vistas,
- comandos,
- navegacion,
- detalle de juego,
- lista/grid/details,
- estadisticas,
- ajustes visuales.

Debe contener:
- MainWindow,
- MainViewModel,
- GameDetailsViewModel,
- StatisticsViewModel,
- LibraryView,
- controles visuales.

25.7 Modulo Tests
Responsabilidad:
- validar comportamiento,
- prevenir regresiones,
- verificar importacion,
- verificar persistencia,
- verificar metadatos,
- verificar emulacion,
- verificar estadisticas.

26. PLAN DE REESCRITURA DETALLADO Y RECOMENDADO

Este es el plan mas util para avanzar sin perder el control del proyecto.

26.1 Fase 0: definicion de base
Objetivo:
- fijar el alcance funcional minimo,
- decidir la estructura final,
- decidir SQLite o LiteDB,
- decidir como guardar imagenes,
- decidir el formato inicial de datos.

Entregables:
- estructura de solucion,
- lista de entidades,
- lista de modulos,
- criterios de exito del MVP.

26.2 Fase 1: nucleo y persistencia
Objetivo:
- crear Game, GameMetadata, Emulator, LibrarySource,
- crear repositorio de juegos,
- guardar y cargar datos,
- verificar que el estado persiste correctamente.

Entregables:
- proyecto Core,
- proyecto Storage,
- una base de datos funcional,
- operaciones basicas CRUD.

Validacion:
- crear juego,
- guardar,
- cerrar app,
- reabrir y comprobar que los datos siguen ahi.

26.3 Fase 2: UI minima funcional
Objetivo:
- abrir ventana principal,
- mostrar lista de juegos,
- mostrar detalle de un juego,
- permitir seleccion simple.

Entregables:
- MainWindow,
- MainViewModel,
- LibraryView,
- GameDetailsView,
- datos de prueba o carga real.

Validacion:
- la aplicacion abre,
- la lista carga,
- el detalle responde al cambio de seleccion.

26.4 Fase 3: edicion e importacion manual
Objetivo:
- crear, editar y eliminar juegos,
- marcar favorito u oculto,
- actualizar playtime,
- manejar metadatos manuales.

Entregables:
- formularios de edicion basicos,
- comandos de edicion,
- guardado de cambios.

Validacion:
- un juego editado conserva cambios tras reiniciar.

26.5 Fase 4: estadisticas basicas
Objetivo:
- calcular totales,
- contar instalados y no instalados,
- contar favoritos y ocultos,
- calcular playtime total,
- top jugados.

Entregables:
- StatisticsService,
- StatisticsViewModel,
- panel visual simple.

Validacion:
- los numeros coinciden con la base de datos real.

26.6 Fase 5: metadatos
Objetivo:
- descargar nombre, descripcion, imagenes y campos basicos,
- cachear resultados,
- evitar descargas repetidas,
- respetar valores existentes.

Entregables:
- MetadataDownloadService,
- MetadataCacheService,
- proveedores iniciales,
- guardado de imagenes.

Validacion:
- una descarga ya realizada no debe repetirse innecesariamente.

26.7 Fase 6: emulacion
Objetivo:
- detectar emuladores,
- escanear ROMs,
- asociar juegos,
- crear o actualizar entradas emuladas.

Entregables:
- detector,
- scanner,
- matching por checksum, serial o nombre,
- importacion de carpetas.

Validacion:
- una ROM detectada debe generar o vincular un juego consistente.

26.8 Fase 7: mejora visual progresiva
Objetivo:
- empezar con WPF simple,
- luego introducir WPF UI,
- mejorar temas,
- agregar Mica/Acrylic si aplica,
- agregar animaciones leves.

Entregables:
- estilos,
- templates,
- transiciones,
- panel visual mejorado.

Validacion:
- la UI mejora sin romper la funcionalidad.

26.9 Fase 8: optimizacion y empaquetado
Objetivo:
- convertir en single-file self-contained,
- reducir tiempo de arranque,
- controlar RAM,
- mantener caches efectivas,
- depurar rutas y assets.

Entregables:
- publicacion final,
- configuracion de empaquetado,
- validacion de arranque en limpio.

Validacion:
- ejecutable unico,
- sin dependencias externas obligatorias,
- comportamiento estable.

26.10 Fase 9: consolidacion final
Objetivo:
- revisar todos los flujos,
- contrastar con esta documentacion,
- detectar huecos funcionales,
- cerrar diferencias con el comportamiento objetivo.

Entregables:
- checklist final,
- comparacion con Playnite original,
- lista de diferencias aceptadas y pendientes.

27. ANEXO DE REFERENCIA VERIFICADA CON CODIGO FUENTE

Esta documentacion no se apoyo solo en la conversacion; tambien se contrasto con archivos concretos del codigo fuente para asegurar que las conclusiones fueran compatibles con la implementacion real observada.

Archivos clave revisados:
- Playnite.DesktopApp/ProgramEntry.cs
- Playnite.DesktopApp/DesktopApplication.cs
- Playnite.FullscreenApp/FullscreenApplication.cs
- Playnite.Database/GameDatabase.cs
- Playnite.Metadata/MetadataDownloader.cs
- Playnite.Plugins/ExtensionFactory.cs
- PlayniteSDK/Plugins/LibraryPlugin.cs
- PlayniteSDK/Plugins/MetadataPlugin.cs
- PlayniteSDK/MetadataProvider.cs
- PlayniteSDK/Models/GameScannerConfig.cs
- Playnite.Emulators/EmulationDatabase.cs
- Playnite.Emulators/Scanner.cs
- Playnite.DesktopApp/Controls/Views/LibraryListView.cs
- Playnite.DesktopApp/Controls/Views/LibraryGridView.cs
- Playnite.DesktopApp/Controls/Views/LibraryDetailsView.cs
- Playnite.DesktopApp/Controls/Views/Library.cs
- Playnite.DesktopApp/Controls/Views/GameOverview.cs
- Playnite.DesktopApp/ViewModels/GameDetailsViewModel.cs
- Playnite.DesktopApp/ViewModels/StatisticsViewModel.cs
- Playnite.DesktopApp/Controls/GameListItem.cs
- Playnite/Themes.cs
- Playnite.GameTools.cs

Hechos verificados relevantes:

- El arranque del escritorio pasa por ProgramEntry y DesktopApplication.
- El modo fullscreen tiene inicializacion y flujo propios.
- La persistencia principal se centraliza en GameDatabase.
- La importacion de bibliotecas puede ser estandar o personalizada.
- Los metadatos se resuelven por campo y por prioridad de fuente.
- El sistema de plugins carga extensiones por reflexion y valida compatibilidad.
- El sistema de emulacion usa escaneo de carpetas, perfiles, CRC, serial y nombre de ROM.
- La UI principal ofrece lista, grid y details como modos de presentacion.
- La vista de detalle expone un panel rico de metadatos y acciones.
- Las estadisticas se calculan sobre la coleccion real de juegos con filtros.
- Los temas se basan en recursos XAML y sustitucion de diccionarios.

Reglas de replicacion para la nueva version:

- conservar importacion incremental,
- conservar cache local de imagenes y metadatos,
- conservar virtualizacion de listas,
- conservar persistencia local robusta,
- conservar soporte emulado,
- conservar estadisticas basicas y luego ampliarlas,
- evitar plugins en la version inicial,
- evitar duplicar escritorio y fullscreen al principio,
- empezar con un nucleo pequeno y verificable,
- separar dominio, almacenamiento, importacion, metadatos, emulacion y UI solo como modularidad interna de desarrollo.

Criterio de uso de esta documentacion:

- debe servir como referencia base de arquitectura,
- debe ser suficiente para reconstruir el comportamiento esencial,
- debe apoyar decisiones de diseÃ±o durante la reescritura,
- y debe permitir verificar en cualquier momento si una implementacion nueva mantiene la logica funcional original.

================================================================================
28. REFERENCIA TECNICA VERIFICADA CONTRA CODIGO FUENTE REAL (agregada 2026-08-05,
sesion posterior, para cerrar los huecos de implementacion detectados al revisar
si esta documentacion alcanzaba para empezar Fase 1 de Bridge)

Fuente: D:\Proyectos\Playnite-master\Playnite-master\source (codigo real de Playnite,
no la conversacion original). A diferencia de las secciones 1-27, que resumen una
conversacion de analisis, esta seccion es extraccion directa de codigo: nombres de
campos, tipos y algoritmos citados literalmente, con archivo y rango de lineas.
Todas las rutas son relativas a esa carpeta source/.

Esta seccion NO propone diseÃ±o para Bridge â€” es referencia pura de quÃ© hace Playnite
hoy. La adaptacion a las entidades reales de Bridge.Core es un paso aparte.

--------------------------------------------------------------------------------
28.1 MODELO DE DATOS â€” CAMPOS REALES

Game â€” PlayniteSDK/Models/Game.cs (lineas 188-2462)
Hereda de DatabaseObject (da Guid Id y string Name). El constructor genera
GameId = Guid.NewGuid().ToString() automaticamente.

Campos persistidos:
  string BackgroundImage
  string Description                    [default ""]
  string Notes                          [default ""]
  List<Guid> GenreIds
  bool EnableSystemHdr
  bool Hidden
  bool Favorite
  string Icon
  string CoverImage
  string InstallDirectory
  DateTime? LastActivity
  string SortingName
  string GameId
  Guid PluginId = Guid.Empty
  bool IncludeLibraryPluginAction = true
  ObservableCollection<GameAction> GameActions
  List<Guid> PlatformIds
  List<Guid> PublisherIds
  List<Guid> DeveloperIds
  ReleaseDate? ReleaseDate
  List<Guid> CategoryIds
  List<Guid> TagIds
  List<Guid> FeatureIds
  ObservableCollection<Link> Links
  ObservableCollection<GameRom> Roms
  bool IsInstalling / IsUninstalling / IsLaunching / IsRunning / IsInstalled
  bool OverrideInstallState
  ulong Playtime = 0                    (segundos)
  DateTime? Added
  DateTime? Modified
  ulong PlayCount = 0
  ulong? InstallSize = null             (bytes)
  DateTime? LastSizeScanDate
  List<Guid> SeriesIds
  string Version
  List<Guid> AgeRatingIds
  List<Guid> RegionIds
  Guid SourceId
  Guid CompletionStatusId
  int? UserScore / CriticScore / CommunityScore = null
  string PreScript / PostScript / GameStartedScript     [default ""]
  bool UseGlobalPostScript / UseGlobalPreScript / UseGlobalGameStartedScript = true
  string Manual                         [default ""]

Campos no persistidos ([DontSerialize], se resuelven en vivo via una referencia
estatica interna a IGameDatabase): Genres, Developers, Publishers, Tags, Features,
Categories, Platforms, Series, AgeRatings, Regions, Source, CompletionStatus,
ReleaseYear, RecentActivity, *ScoreRating/*ScoreGroup (x3), *Segment (LastActivity/
Recent/Added/Modified), PlaytimeCategory, InstallSizeGroup, IsCustomGame
(=> PluginId == Guid.Empty), InstallationStatus.

Metodos clave: GetCopy() (clon profundo), CopyDiffTo(object target) (copia campo a
campo, usado por ItemCollection.Update), GetDifferences(Game) -> List<GameField>
(deteccion de cambios para eventos), GetNameGroup(), GetInstallSizeGroup().

GameField (enum, mismo archivo, lineas 19-183): 60+ valores con numeros explicitos
NO secuenciales (hay huecos, ej. saltan 8, 13-15, 32, 34-35, 46-49, 52 â€” reflejan
campos eliminados historicamente). No asumir que la numeracion es secuencial si se
porta este enum.

GameMetadata â€” PlayniteSDK/Models/GameMetadata.cs (lineas 276-465)
DTO de "datos de juego importables" que usan los plugins. Los campos de referencia
usan MetadataProperty (no Guid/string crudo) para poder resolver por nombre o por id:
  string Name / GameId / Description / InstallDirectory / SortingName / Version
  ulong? InstallSize
  List<GameAction> GameActions
  ReleaseDate? ReleaseDate
  List<Link> Links
  List<GameRom> Roms
  bool IsInstalled / Hidden / Favorite
  ulong Playtime / PlayCount
  DateTime? LastActivity
  MetadataProperty CompletionStatus / Source
  int? UserScore / CriticScore / CommunityScore
  MetadataFile Icon / CoverImage / BackgroundImage
  HashSet<MetadataProperty> Series / AgeRatings / Regions / Platforms / Developers /
    Publishers / Genres / Categories / Tags / Features

MetadataProperty (jerarquia, mismo archivo, lineas 98-271):
  abstract class MetadataProperty {}
  MetadataIdProperty     : Guid Id     â€” referencia un objeto existente por id
  MetadataNameProperty   : string Name â€” resuelto/creado por nombre (match case-insensitive)
  MetadataSpecProperty   : string Id   â€” "specification id" (ej. slugs de IGDB para
                                          Platform/Region), NO tiene resolucion
                                          generica en ItemCollection, se usa a mano
                                          en sitios puntuales (ej. escaneo de emuladores)

Link (NO "LinkItem") â€” PlayniteSDK/Models/Link.cs
  string Name
  string Url

MetadataFile (NO existe clase separada "MetadataImage") â€” GameMetadata.cs lineas 13-93
  string FileName
  byte[] Content
  string Path        (URL/ruta original de origen)
  bool HasContent    => FileName no vacio && Content != null
  bool HasImageData  => HasContent || Path no vacio
  3 constructores: por path/URL solo; por (name, data); por (name, data, originalUrl)

Emulator / EmulatorProfile â€” PlayniteSDK/Models/Emulator.cs
  abstract class EmulatorProfile : ObservableObject
    string Id / Name / PreScript / PostScript / ExitScript

  class BuiltInEmulatorProfile : EmulatorProfile
    Id = "#builtin_" + Guid.NewGuid()
    string BuiltInProfileName
    bool OverrideDefaultArgs
    string CustomArguments

  class CustomEmulatorProfile : EmulatorProfile
    Id = "#custom_" + Guid.NewGuid()
    string StartupScript
    List<Guid> Platforms          (ids de Platform que soporta este perfil)
    List<string> ImageExtensions
    string Executable / Arguments / WorkingDirectory
    TrackingMode TrackingMode = Default
    string TrackingPath

  class Emulator : DatabaseObject
    string BuiltInConfigId        (referencia a EmulatorDefinition.Id)
    string InstallDir
    ObservableCollection<BuiltInEmulatorProfile> BuiltinProfiles
    ObservableCollection<CustomEmulatorProfile> CustomProfiles
    [no persistido] SelectableProfiles / AllProfiles
    GetProfile(string profileId)  â€” busca primero en CustomProfiles, luego Builtin

  Tipos de definicion (cargados de YAML/JSON embebido, NO persistidos en la DB):
    EmulatorDefinitionProfile { Name; List<string> Platforms; ImageExtensions;
      ProfileFiles; InstallationFile; StartupArguments; StartupExecutable;
      bool ScriptStartup; bool ScriptGameImport; }
    EmulatorDefinition { DirectoryName; Id; Name; Website; List<EmulatorDefinitionProfile> Profiles; }
    EmulatedRegion { Id; Name; bool DefaultImport; ulong IgdbId; List<string> Codes; }
    EmulatedPlatform { ulong IgdbId; string Name; Id; List<string> Databases; List<string> Emulators; }

GameScannerConfig â€” PlayniteSDK/Models/GameScannerConfig.cs (lineas 36-334)
  Guid EmulatorId
  string EmulatorProfileId
  List<string> CrcExcludeFileTypes
  string Directory                          (raiz de escaneo, soporta variables expandibles)
  bool InGlobalUpdate = true
  bool ExcludeOnlineFiles = false
  bool UseSimplifiedOnlineFileScan = false
  bool ImportWithRelativePaths = true
  bool ScanSubfolders = true
  bool ScanInsideArchives = true
  List<string> ExcludedFiles / ExcludedDirectories
  Guid OverridePlatformId
  ScannerConfigPlayActionSettings PlayActionSettings = ScannerSettings
  bool MergeRelatedFiles = true
  enum ScannerConfigPlayActionSettings { ScannerSettings, SelectProfiteOnStart, SelectEmulatorOnStart }

Entidades normalizadas (todas subclases finas de DatabaseObject: Guid Id + string
Name heredados, mas un sentinel estatico Empty):
  Genre           â€” sin campos extra
  Company (base), Developer : Company, Publisher : Company
                  â€” Developer/Publisher NO agregan campos, son solo tipos marcadores
                    para MetadataProperty; la DB real guarda todo en una unica
                    coleccion compartida Companies
  Category / Tag / Series          â€” sin campos extra
  AgeRating                        â€” sin campos extra (AgeRatingOrg es un enum
                                      separado: PEGI, ESRB â€” no un campo de AgeRating)
  Region            â€” agrega string SpecificationId
  Platform          â€” agrega string SpecificationId, string Icon, string Cover,
                      string Background (imagenes por defecto del sistema)
  CompletionStatus  â€” sin campos extra (el estado "jugado"/"default" es externo,
                      vive en CompletionStatusSettings â€” ver S28.2)
  GameFeature       â€” sin campos extra (entidad separada de Category/Tag)
  GameSource        â€” sin campos extra; ES la entidad normalizada de "fuente/origen"
                      (ej. "Steam", "GOG", "Retail"), pero es ortogonal a la
                      identidad de plugin (ver mas abajo)

Identidad de biblioteca en Game: NO existe una clase separada "LibrarySource" para
esto. Es directamente dos campos crudos en Game:
  string GameId   â€” id nativo de la tienda (ej. Steam AppID)
  Guid PluginId   â€” id del LibraryPlugin responsable (Guid.Empty si es manual;
                    Game.IsCustomGame es literalmente PluginId == Guid.Empty)
GameSource (arriba) es un tag de origen aparte, editable por el usuario â€” mas
parecido a una categorizacion manual que a la identidad de plugin.

FilterPreset â€” PlayniteSDK/Models/FilterPreset.cs
  class FilterPreset : DatabaseObject
    FilterPresetSettings Settings
    SortOrder? SortingOrder
    SortOrderDirection? SortingOrderDirection
    GroupableField? GroupingOrder
    bool ShowInFullscreeQuickSelection = true

  class FilterPresetSettings   (NO es DatabaseObject, es un blob de settings plano)
    bool UseAndFilteringStyle / IsInstalled / IsUnInstalled / Hidden / Favorite
    string Name / Version
    StringFilterItemProperties ReleaseYear
    IdItemFilterItemProperties Genre / Platform / Publisher / Developer / Category /
      Tag / Series / Region / Source / AgeRating / Library / CompletionStatuses / Feature
    EnumFilterItemProperties UserScore / CriticScore / CommunityScore / LastActivity /
      RecentActivity / Added / Modified / PlayTime / InstallSize

  IdItemFilterItemProperties { List<Guid> Ids; string Text; }   (lista de ids O texto libre)
  StringFilterItemProperties { List<string> Values; }
  EnumFilterItemProperties { List<int> Values; }

GameStatistics: NO existe como clase de modelo dedicada. Confirmado â€” no hay ninguna
clase con ese nombre en todo el codigo. Las estadisticas se calculan al vuelo dentro
de StatisticsViewModel.FillData() (ver S28.4).

--------------------------------------------------------------------------------
28.2 GameDatabase â€” PERSISTENCIA Y FLUJO DE IMPORTACION REAL

Playnite/Database/GameDatabase.cs (GameDatabase : IGameDatabaseMain, IDisposable),
Playnite/Database/Collections/ItemCollection.cs, .../GamesCollection.cs

Superficie publica (IGameDatabaseMain, lineas 26-81): 17 colecciones IItemCollection<T>
â€” Games, Platforms, Emulators, Genres, Companies (Developer+Publisher compartido),
Tags, Categories, Series, AgeRatings, Regions, Sources, Features, SoftwareApps,
GameScanners, FilterPresets, ImportExclusions, CompletionStatuses.

ItemCollection<TItem> â€” CRUD generico:
  TItem Get(Guid id) / List<TItem> Get(IList<Guid> ids)
  bool ContainsItem(Guid id)
  TItem GetOrGenerate(MetadataProperty property)
  TItem Add(MetadataProperty property)   â€” resuelve MetadataNameProperty por nombre
                                            (case-insensitive) o crea
  TItem Add(string itemName) / Add(string, Func<TItem,string,bool> comparer)
  IEnumerable<TItem> Add(List<string> itemsToAdd)
  void Add(TItem) / Add(IEnumerable<TItem>)   â€” throw si el Id ya existe
  bool Remove(Guid id) / Remove(TItem) / Remove(IEnumerable<TItem>)
  void Update(TItem) / Update(IEnumerable<TItem>)
  BeginBufferUpdate() / EndBufferUpdate() / BufferedUpdate()  â€” agrupa eventos

GamesCollection : ItemCollection<Game> sobreescribe Add/Remove/Update para: marcar
timestamps Added/Modified al agregar; borrar archivos huerfanos de Icon/CoverImage/
BackgroundImage (o limpiar HttpFileCache si el background es una URL http) al
eliminar/actualizar.

ImportGames(LibraryPlugin library, CancellationToken, PlaytimeImportMode) â€”
algoritmo real (GameDatabase.cs lineas 1205-1338):
  1. Toda la operacion dentro de BufferedUpdate() (difiere eventos de cambio).
  2. Carga CompletionStatusSettings una sola vez (DefaultStatus, PlayedStatus).
  3. Rama A â€” library.Properties?.HasCustomizedGameImport == true: llama
     library.ImportGames(...) directamente (el plugin controla TODO el import,
     Playnite no dedupea ni mergea nada), solo aplica updateCompletionStatus a cada
     Game devuelto.
  4. Rama B â€” flujo estandar, por cada GameMetadata newGame que devuelve
     library.GetGames(...):
     - Chequeo de exclusion PRIMERO: se salta si
       ImportExclusions[ImportExclusionItem.GetId(newGame.GameId, library.Id)] != null
       (el id de exclusion es un hash MD5 deterministico de "{gameId}_{libraryId}").
     - Clave de dedup: Games.FirstOrDefault(a => a.GameId == newGame.GameId &&
       a.PluginId == library.Id) â€” match exacto sobre el par (GameId, PluginId),
       nada mas difuso que eso.
     - Juego NUEVO (existingGame == null):
       - El modo de importacion de playtime se aplica ANTES de crear: si
         newGame.Playtime != 0, se pone en 0 salvo que playtimeImportMode sea
         Always o NewImportsOnly.
       - Llama ImportGame(newGame, library.Id) (ver abajo), aplica
         updateCompletionStatus, y si eso cambio algo hace un segundo
         Games.Update(importedGame).
     - Juego EXISTENTE (existingGame != null) â€” solo estos campos se tocan, y
       solo condicionalmente (el resto â€” Name, Description, imagenes, generos,
       etc. â€” NUNCA se toca en un re-import, solo lo toca MetadataDownloader):
       - Si !existingGame.IsCustomGame && !existingGame.OverrideInstallState:
         sincroniza IsInstalled e InstallDirectory (comparacion case-insensitive)
         desde newGame.
       - Si playtimeImportMode == Always && newGame.Playtime > 0: sobreescribe
         Playtime; sobreescribe LastActivity solo si newGame.LastActivity es mas
         reciente que el existente (o el existente es null); vuelve a correr
         updateCompletionStatus.
       - Si !existingGame.IsInstalled && newGame.InstallSize > 0 &&
         existingGame.InstallSize != newGame.InstallSize: sobreescribe InstallSize.
       - Un solo Games.Update(existingGame) al final si algo cambio.
  updateCompletionStatus(game, settings) (funcion local): si Playtime > 0 y el
  estado esta vacio/default -> PlayedStatus; si Playtime == 0 y esta vacio ->
  DefaultStatus.

ImportGame / GameInfoToGame (lineas 1081-1203) â€” conversion GameMetadata -> Game:
GameInfoToGame mapea campos escalares directo (Name, GameId, Description,
InstallDirectory, SortingName, GameActions, ReleaseDate, Links, Roms, IsInstalled,
Playtime, PlayCount, LastActivity, Version, UserScore, CriticScore, CommunityScore,
Hidden, Favorite, InstallSize). Para cada coleccion de entidades de referencia en
GameMetadata (Platforms, Regions, Developers, Publishers, Genres, Categories, Tags,
Features, AgeRatings, Series, Source unico, CompletionStatus unico), llama al
overload .Add(...) de la coleccion correspondiente que acepta MetadataProperty (o
IEnumerable<MetadataProperty>), que resuelve-o-crea por nombre y guarda solo los
Ids resultantes en el Game.
ImportGame(GameMetadata, Guid pluginId): llama GameInfoToGame, luego para Icon/
CoverImage/BackgroundImage (si estan presentes) llama
database.AddFile(MetadataFile, toAdd.Id, isImage: true, ...) para materializarlos,
setea IncludeLibraryPluginAction = true, llama Games.Add(toAdd).

PERSISTENCIA EN DISCO â€” correccion importante sobre lo asumido en S4:
Playnite NO guarda un archivo JSON por entidad. Cada "carpeta por entidad" es en
realidad UN SOLO ARCHIVO LiteDB por coleccion (ItemCollection.cs lineas 89-197,
GameDatabase.cs lineas 126-248):
  - DatabasePath contiene: database.json (DatabaseSettings global, version de
    esquema), una carpeta files/ (assets binarios sueltos), y una ruta por
    coleccion: games, platforms, emulators, genres, companies, tags, categories,
    series, ageratings, regions, sources, features, tools, scanners,
    filterpresets, importexclusions, completionstatuses.
  - Cada una de esas rutas de coleccion es en realidad el nombre base de un
    archivo LiteDB: ItemCollection.InitializeCollection(path) abre
    new LiteDatabase($"Filename={path}.db;Mode=Exclusive;Cache Size=0", mapper)
    â€” o sea la ruta "games" se convierte en games.db en disco (una base LiteDB v4
    embebida de un solo archivo, con un documento BSON por Game), NO una carpeta
    games/ llena de <guid>.json.
  - Comentario explicito en el codigo: "We currently use LiteDB for permanent
    storage. We don't use latest LiteDB 5, but instead latest LiteDB 4."
  - Existe recuperacion ante corrupcion: si loadCollections() lanza excepcion,
    hace backup del .db, usa un lector interno LiteDBConversion.FileReaderV7 para
    rescatar los documentos BSON crudos, y reescribe un archivo LiteDB limpio.
  - BsonMapper configurado por coleccion via el metodo estatico
    MapLiteDbEntities(mapper) de cada clase de coleccion (ej.
    GamesCollection.MapLiteDbEntities registra un serializador custom para
    ReleaseDate e Ignore() cada propiedad [DontSerialize] mas las navegaciones
    expandidas como Genres, Developers, etc.).
  - database.json (el de nivel superior, no el de cada coleccion) guarda solo
    DatabaseSettings { Version } â€” gate de version de esquema (NewFormatVersion
    = 4; abrir lanza excepcion si Settings.Version > NewFormatVersion o si hace
    falta migracion).
  CONCLUSION: los datos de entidades son LiteDB (BSON), no JSON por archivo. Solo
  imagenes/assets binarios son archivos sueltos en disco.

Almacenamiento de archivos â€” carpeta files/, AddFile/GetFileStoragePath/
GetFileAsImage (lineas 787-1032):
  - GetFileStoragePath(Guid parentId) -> Path.Combine(FilesDirectoryPath,
    parentId.ToString()), crea el directorio si no existe. Cada objeto de la DB
    (tipicamente Game.Id) tiene su propia subcarpeta bajo files/.
  - AddFile(string path, Guid parentId, bool isImage, CancellationToken):
    - Si path es una URL http: descarga via HttpDownloader.DownloadFile a
      files/<parentId>/<nuevoGuid><ext>; si isImage, ademas corre
      Images.ConvertToCompatibleFormat (puede cambiar extension/formato) antes
      de fijar el dbPath final.
    - Si path es un archivo local que YA esta dentro del directorio destino: se
      reusa en el lugar, sin copiar (dbPath = parentId/<nombreOriginal>).
    - En cualquier otro caso: el archivo local se copia, renombrado a
      <nuevoGuid><ext> (o convertido si es imagen).
    - El dbPath devuelto siempre es una ruta RELATIVA con forma
      "<guid-parentId>/<nombreArchivo>" (no un hash â€” un Guid nuevo por archivo,
      excepto en el caso "ya esta en el directorio destino" donde se conserva el
      nombre original).
  - GetFullFilePath(dbPath) -> Path.Combine(FilesDirectoryPath, dbPath) â€” resuelve
    un dbPath relativo guardado a una ruta absoluta.
  - GetFileAsImage(dbPath, loadProperties) -> resuelve la ruta completa, bloquea
    por archivo (diccionario fileLocks por dbPath), lee via
    BitmapExtensions.BitmapFromStream.
  - RemoveFile(dbPath) borra el archivo y, si la carpeta padre queda vacia, borra
    tambien la carpeta.
  - Constantes: MaximumRecommendedIconSize = 0.1, MaximumRecommendedCoverSize = 1,
    MaximumRecommendedBackgroundSize = 4 â€” solo orientativas (unidades: MB).

--------------------------------------------------------------------------------
28.3 MetadataDownloader â€” FLUJO REAL DE RESOLUCION DE METADATOS

Playnite/Metadata/MetadataDownloader.cs, .../MetadataDownloaderSettings.cs,
PlayniteSDK/Plugins/MetadataPlugin.cs

ProcessField â€” algoritmo exacto por campo (lineas 136-309):
  GameMetadata ProcessField(Game game, MetadataFieldSettings fieldSettings,
    MetadataField gameField, Dictionary<Guid,GameMetadata> existingStoreData,
    Dictionary<Guid,OnDemandMetadataProvider> existingPluginData, CancellationToken)

  1. Si fieldSettings.Sources esta vacio -> devuelve null de inmediato (el campo
     no tiene ninguna fuente configurada).
  2. Itera fieldSettings.Sources EN EL ORDEN CONFIGURADO (el orden literal que el
     usuario definio en Ajustes > Metadatos â€” la primera fuente que da datos
     validos gana):
     - Salta la pseudo-fuente "Store" (Guid.Empty) si el juego es manual
       (game.PluginId == Guid.Empty).
     - Si los datos de esta fuente ya se pidieron en esta corrida
       (existingStoreData.ContainsKey(source)), los reusa: los devuelve si
       FieldHasValidData es true para este campo, si no continue a la siguiente
       fuente (el cache es por FUENTE, no por campo â€” una sola descarga de store
       se reusa entre todos los campos).
     - Si source != Guid.Empty, busca el MetadataPlugin por id y chequea
       downloader.SupportedFields?.Contains(gameField) â€” salta la fuente entera
       si no puede proveer este campo.
     - Descarga: source == Guid.Empty -> ProcessStoreDownload(game) (llama
       LibraryMetadataProvider.GetMetadata(game) del LibraryPlugin dueÃ±o),
       cacheado en existingStoreData. Si no, obtiene/crea un
       OnDemandMetadataProvider del plugin (cacheado en existingPluginData por
       fuente), chequea provider.AvailableFields.Contains(gameField), y llama al
       getter especifico por campo (GetName, GetGenres, GetReleaseDate,
       GetDevelopers, GetPublishers, GetTags, GetDescription, GetLinks,
       GetCriticScore, GetCommunityScore, GetIcon, GetCoverImage,
       GetBackgroundImage, GetFeatures, GetAgeRatings, GetRegions, GetSeries,
       GetPlatforms, GetInstallSize) y envuelve el resultado en un GameMetadata
       nuevo con solo ese campo lleno.
     - Si metadata != null && FieldHasValidData(gameField, metadata) -> devuelve
       eso (para de iterar fuentes). Si no, sigue con la siguiente fuente.
  3. Si ninguna fuente dio datos -> devuelve null.

  FieldHasValidData (lineas 84-134): chequeo null/vacio por MetadataField (ej.
  Genres.HasItems(), !Name.IsNullOrWhiteSpace(), Icon != null).

SkipExistingValues â€” patron literal repetido por campo en DownloadMetadataAsync
(lineas 311-636): para cada campo el chequeo es
  if (!settings.SkipExistingValues || (settings.SkipExistingValues && <campo-vacio>))
â€” o sea siempre se procesa si SkipExistingValues == false; si es true, solo se
procesa cuando el valor actual del juego para ese campo es null/vacio/default. Este
chequeo se hace ANTES de llamar ProcessField, asi que un campo ya lleno con
SkipExistingValues = true nunca dispara ni una llamada de red.

Lista exacta de campos que resuelve (confirmada contra el enum MetadataField y los
bloques por campo en DownloadMetadataAsync): Name, Genres (GenreIds), ReleaseDate,
Developers (DeveloperIds), Publishers (PublisherIds), Tags (TagIds), Features
(FeatureIds), Description, Links, AgeRating (AgeRatingIds), Region (RegionIds),
Series (SeriesIds), Platform (PlatformIds), CriticScore, CommunityScore,
BackgroundImage, CoverImage, Icon, InstallSize.
Esto confirma y completa la lista de S7 â€” CommunityScore y CriticScore estan
ambos presentes (campos separados), e InstallSize esta presente con un guard
especial: isInstalledAndHasValue = game.IsInstalled && game.InstallSize != null â€”
si es true, la descarga de InstallSize se salta por completo sin importar
SkipExistingValues, porque un tamaÃ±o escaneado localmente en un juego instalado
se considera mas confiable que el tamaÃ±o que reporta la fuente de metadatos.
NO estan en el loop de resolucion (o sea NUNCA los llena MetadataDownloader):
UserScore, Notes, Version, Manual, los scripts â€” son campos manuales/de usuario
exclusivamente.

Guardado de imagenes â€” mismo mecanismo de archivos que S28.2, pero con logica
distinta segun el campo:
  - BackgroundImage tiene logica propia distinta de Icon/Cover: controlada por
    playniteSettings.DownloadBackgroundsImmediately. Si es true (o como fallback
    cuando el MetadataFile.Path devuelto esta vacio), se materializa via
    database.AddFile(gameData.BackgroundImage, game.Id, true, cancelToken) (la
    misma llamada de S28.2). Si DownloadBackgroundsImmediately == false y el
    MetadataFile tiene un .Path no vacio, el BackgroundImage del juego se setea
    directo a la URL CRUDA (gameData.BackgroundImage.Path) â€” o sea los fondos
    pueden quedar como URLs http de carga perezosa guardadas tal cual en la DB,
    por eso GamesCollection.Update/Remove tienen un caso especial para
    BackgroundImage.IsHttpUrl() que limpia HttpFileCache en vez de llamar
    RemoveFile.
  - CoverImage e Icon: siempre se materializan de inmediato via
    database.AddFile(gameData.CoverImage/Icon, game.Id, true, cancelToken) â€” sin
    opcion de URL perezosa.
  - Al terminar de procesar todos los campos: solo escribe de vuelta a la DB
    (database.Games.Update(game)) si dataModified es true (rastreado con un
    handler de PropertyChanged en el clon en memoria), y solo si el juego sigue
    existiendo en la DB (re-chequeado por id) â€” marca game.Modified = DateTime.Now
    primero.
  - Opera sobre un CLON: game = database.Games[games[i].Id]?.GetClone() â€” nunca
    muta la instancia viva de la DB directamente, para no bloquear la edicion de
    otros juegos durante una descarga por lote.

--------------------------------------------------------------------------------
28.4 EMULACION â€” DETECCION, ESCANEO Y MATCHING REALES

Playnite/Emulators/Scanner.cs, .../EmulationDatabase.cs, .../DatModels.cs

Deteccion de emuladores â€” EmulatorScanner.SearchForEmulators (lineas 55-146):
  static List<ScannedEmulator> SearchForEmulators(string path,
    IList<EmulatorDefinition> definitions, CancellationToken)
  1. Enumera recursivamente todos los archivos bajo path (SafeFileEnumerator,
     AllDirectories).
  2. Por cada archivo x cada EmulatorDefinition x cada EmulatorDefinitionProfile
     de esa definicion:
     - detectionStr = defProfile.InstallationFile ?? defProfile.StartupExecutable
       â€” un PATRON REGEX, compilado al vuelo por cada par archivo/perfil (new
       Regex(detectionStr, IgnoreCase)), matcheado contra file.Name solamente
       (no la ruta completa).
     - Si matchea y el perfil declara ProfileFiles (archivos hermanos
       requeridos), todos deben existir en el mismo directorio o se rechaza el
       match.
     - Si tiene exito, agrupa los matches por importId = definition.Id +
       currentDir en un ScannedEmulator (la clave de dedup es
       definicion+directorio, asi que varios perfiles matcheando en la misma
       carpeta se mergean en un solo ScannedEmulator con varios Profiles). Si el
       directorio encontrado esta dentro del propio directorio de Playnite,
       InstallDir se reescribe usando el token %PlayniteDir%
       (ExpandableVariables.PlayniteDirectory) en vez de la ruta literal.
  3. Devuelve la lista dedupeada; esto NO escribe nada en disco â€” solo propone
     candidatos (ScannedEmulator.Import / ScannedEmulatorProfile.Import son bool
     que por defecto son true, pensados para un paso de revision en UI antes de
     confirmar en la DB).

Escaneo de ROMs â€” GameScanner.Scan (lineas 148-1300):
  Entrada: Scan(CancellationToken, out List<Platform> newPlatforms, out
    List<Region> newRegions, Action<string> fileScanCallback)
  1. Resuelve Emulator y perfil (CustomEmulatorProfile si EmulatorProfileId
     empieza con "#custom_", BuiltInEmulatorProfile si empieza con "#builtin_" â€”
     si no, lanza "not supported"). Lanza excepcion si EmulatorId/
     EmulatorProfileId no resuelven.
  2. importedFiles = database.GetImportedRomFiles(emulator.InstallDir) â€” arma el
     set de exclusion de rutas de ROM ya importadas (en minusculas) expandiendo
     cada Game.Roms[].Path existente via game.ExpandVariables.
  3. Mergea las listas de extensiones a excluir de CRC globales
     (database.GetGameScannersSettings().CrcExcludeFileTypes) y por-scanner
     (scanner.CrcExcludeFileTypes).
  4. dirToScan = PlaynitePaths.ExpandVariables(scanner.Directory,
     emulator.InstallDir, true) â€” expande tokens como el directorio del emulador
     relativo al Directory configurado del scanner.
  5. Parsea scanner.ExcludedFiles/ExcludedDirectories via ParseExclusions â€” un
     mini-DSL: un ">" al inicio de una entrada significa "match relativo" (match
     por sufijo en vez de ruta absoluta), un "?" al inicio significa "tratar como
     regex". Se pueden combinar ambos.
  6. Llama al overload privado ScanDirectory (la variante de perfil custom
     resuelve los Guids de profile.Platforms a sus Platform.SpecificationId
     string; la variante builtin resuelve via Emulation.GetProfile(...) y
     ademas soporta un import totalmente scripteado si
     emuProf.ScriptGameImport == true, ejecutando un script PowerShell
     (Emulation.GetGameImportScriptPath) con variables ligadas CancelToken,
     Emulator, EmulatorProfile, ScanDirectory, PlayniteApi, ImportedFiles).
  7. ScanDirectoryBase â€” el recorrido recursivo real:
     - Lista Directory.GetFiles/GetDirectories del directorio actual (usa
       Paths.FixPathLength para soporte de rutas largas).
     - Quita archivos que matchean fileExclusions (GetFileExclusionMatches).
     - Los archivos de playlist tienen PRIORIDAD y consumen a sus hijos
       referenciados: .cue (via CueSheet.GetFileEntries), .m3u (via
       M3U.GetEntries), .gdi (via GdiFile.GetEntries, "Dreamcast dumps") se
       parsean cada uno, sus archivos hijos referenciados se quitan de la lista
       principal (dedupeado para no escanearlos dos veces), y el lookup CRC/db
       se intenta contra los HIJOS (processPlayListFile prueba cada hijo hasta
       que uno devuelve datos de DB), mientras que el ScannedRom resultante se
       crea para la ruta del propio archivo de playlist.
     - Para cada archivo restante: la extension debe matchear literalmente una
       de supportedExtensions (de las ImageExtensions del perfil), incluyendo un
       sentinel especial "<none>" que significa "sin extension" y soporte para
       extensiones anidadas/con puntos (ej. .p8.png). Los archivos ya importados
       (set importedFiles) se saltan.
     - El escaneo de CRC se salta condicionalmente: si scanner.ExcludeOnlineFiles
       y el archivo no esta disponible localmente (IsFileDataAvailable â€” chequea
       FILE_ATTRIBUTE_OFFLINE/FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS/
       FILE_ATTRIBUTE_SPARSE_FILE via Kernel32.GetFileAttributesW, mas una
       heuristica especial para Google Drive que busca "content-entry" en un
       stream NTFS alternativo :user.drive.itemprotostr), el escaneo se salta
       por completo salvo que UseSimplifiedOnlineFileScan este activo (en cuyo
       caso se salta el CRC pero igual corre un match por nombre contra la DB).
       Los archivos que matchean crcExludePatterns tambien se saltan de CRC.
     - Lookup en DB (LookupGameInDb) prueba, EN ESTE ORDEN EXACTO, parando en el
       primer hit: (1) match por CRC â€” si el archivo es un tipo de archivo
       comprimido soportado (rar,7z,zip,tar,bzip2,gzip,lzip) y scanArchives esta
       activo, calcula el CRC32 de cada entrada con extension matcheable DENTRO
       del archivo comprimido (si no encuentra ninguna, cae a CRC32 del archivo
       completo); si no, CRC32 del archivo crudo; se prueba contra cada DB de
       emulacion configurada via db.GetByCrc. (2) match exacto de RomName sobre
       el nombre de archivo (db.GetByRomName). (3) match parcial de RomName
       (db.GetByRomNamePartial â€” "principalmente para juegos XBLA con nombres de
       archivo raros"). (4) match de Serial usando el nombre de archivo sin
       extension (db.GetBySerial â€” "para casos raros donde el nombre del archivo
       ROM es igual al serial del juego"). Itera las DBs configuradas (una por
       plataforma emulada aplicable) en orden de lista y devuelve el primer hit;
       se llama ClearStatementCache() de cada DB antes de cada archivo para
       acotar el crecimiento de memoria del cache de statements de SQLNado.
     - Recursion a subcarpetas solo si scanSubfolders, despues de aplicar
       directoryExclusions.
  8. Enriquecimiento post-escaneo por cada ScannedGame resultante: asigna
     SourceEmulator/SourceConfig; deriva Region desde rom.DbData.Region (si no,
     cae a parsear tokens entre corchetes/parentesis del nombre como (USA)/[E]
     via RomName.Properties y matcheando contra Emulation.GetRegionByCode);
     deriva Platform desde la DB de origen de rom.DbData
     (Emulation.GetPlatformByDatabase) o desde la plataforma declarada del
     perfil builtin, salvo que scanner.OverridePlatformId != Guid.Empty (fuerza
     una sola plataforma para todo). Las Platforms/Regions recien descubiertas
     que no estan aun en la DB se juntan en los out newPlatforms/newRegions en
     vez de escribirse de inmediato (el caller decide si las persiste).

EmulationDatabase â€” formato confirmado SQLITE, un .db por nombre de base de datos
de plataforma (Playnite/Emulators/EmulationDatabase.cs):
  static EmulationDatabaseReader GetDatabase(string databaseName, string databaseDir)
    // abre Path.Combine(databaseDir, $"{databaseName}.db") solo-lectura via
    // SqlNado (SQLiteDatabase)
  Lee una unica tabla DatGame (debe TableExists("DatGame") o todos los lookups
  devuelven vacio) con estos metodos de consulta exactos: GetByCrc(checksum) ->
  WHERE UPPER(RomCrc) = '<val>'; GetBySerial(serial); GetByRomName(romName);
  GetByRomNamePartial(romNamePart) -> WHERE INSTR(UPPER(RomName), '<val>') > 0.
  Todo el interpolado de strings escapa comillas simples a mano
  (Replace("'", "''")) â€” SQL crudo armado por concatenacion, NO queries
  parametrizadas.

  Modelo DatGame (DatModels.cs, atributos [SQLiteColumn]/[DatProperty] de SqlNado
  mas un atributo custom de parseo de archivos DAT): int Id (PK autoincrement),
  string Name, string Region, string ReleaseYear, string Serial (indexado),
  string RomCrc (indexado), string RomName, mas campos
  [SQLiteColumn(Ignore=true)] RomSerial, Origin, Comment que existen solo para
  parsear archivos DAT pero no se persisten en SQLite.

Mapeo de campos de GameScannerConfig a comportamiento real (cruzado con S28.1 y el
algoritmo de arriba): CrcExcludeFileTypes se mergea con la config global para
saltar el calculo de CRC por extension; ExcludedFiles/ExcludedDirectories
alimentan ParseExclusions; ScanSubfolders habilita la recursion; ScanInsideArchives
habilita la extraccion de CRC dentro de archivos comprimidos; MergeRelatedFiles
controla si ROMs con el mismo nombre base (ej. multi-disco) se mergean en un solo
ScannedGame (la funcion local addRom se ramifica segun esto);
ImportWithRelativePaths controla si ScannedGame.ToGame() reescribe el prefijo
comun de ruta de ROM a un token %EmulatorDir%/%PlayniteDir% en vez de una ruta
absoluta; OverridePlatformId fuerza una sola plataforma en todos los resultados
del escaneo, evitando la deteccion de plataforma derivada de la DB;
PlayActionSettings (ScannerSettings/SelectProfiteOnStart/SelectEmulatorOnStart)
controla si el EmulatorProfileId/EmulatorId del GameAction generado se
prellenan o se dejan null para "elegir al iniciar."

--------------------------------------------------------------------------------
28.5 VISTAS Y VIEWMODELS â€” SUPERFICIE REAL DE CAMPOS Y COMANDOS

GameDetailsViewModel â€” Playnite.DesktopApp/ViewModels/GameDetailsViewModel.cs
El objeto bindable central es GamesCollectionViewEntry Game (NO Game directamente
â€” ver mas abajo). Bools de estado/computados: IsRunning, IsInstalling,
IsUninstalling, IsLaunching, IsInstalled, IsPlayAvailable, IsContextAvailable,
IsInstallAvailable. string ContextActionDescription (label localizado segun
estado). ~30 propiedades Visibility controladas tanto por un flag
PlayniteSettings.DetailsVisibility.* COMO por un chequeo de presencia de datos,
ej.: SourceLibraryVisibility, PlayTimeVisibility, InstallSizeVisibility,
InstallDirectoryVisibility, LastPlayedVisibility, AddedVisibility,
RecentActivityVisibility, CompletionStatusVisibility, PlatformVisibility,
GenreVisibility, DeveloperVisibility, PublisherVisibility, ReleaseDateVisibility,
CategoryVisibility, TagVisibility, FeatureVisibility, LinkVisibility,
DescriptionVisibility, NotesVisibility, CoverVisibility, BackgroundVisibility,
IconVisibility, AgeRatingVisibility, SeriesVisibility, SourceVisibility,
RegionVisibility, VersionVisibility, CommunityScoreVisibility,
CriticScoreVisibility, UserScoreVisibility, NameVisibility.
Comandos: SetLibraryFilterCommand, SetPlatformFilterCommand,
SetPublisherFilterCommand, SetDeveloperFilterCommand, SetGenreFilterCommand,
SetReleaseDateFilterCommand, SetCategoryFilterCommand, SetTagFilterCommand,
SetFeatureFilterCommand, SetAgeRatingFilterCommand, SetSeriesFilterCommand,
SetSourceFilterCommand, SetRegionFilterCommand, SetVersionFilterCommand,
SetCompletionStatusFilterCommand, OpenGameLocationCommand, OpenLinkCommand,
PlayCommand, InstallCommand, CheckSetupCommand, CheckExecutionCommand,
EditGameCommand, ContextActionCommand. Cada Set*FilterCommand escribe en
settings.FilterSettings.<Campo> y fuerza settings.FilterPanelVisible = true â€”
clickear un tag en el detalle de un juego salta a la vista filtrada. Play/
Install/EditGame/CheckSetup/CheckExecution delegan todos en un
DesktopGamesEditor editor inyectado.

StatisticsViewModel â€” Playnite.DesktopApp/ViewModels/StatisticsViewModel.cs
Lista de dimensiones de filtro List<FilterSection> Filters â€” un set fijo de
valores GameField usables como eje de filtro de estadisticas: None, PluginId,
Genres, Features, Tags, Platforms, Developers, Publishers, Categories,
ReleaseYear, Series, AgeRatings, Regions, Source, CompletionStatus,
InstallationStatus. SelectedFilter/FilterObjects/SelectedFilterObject manejan un
dropdown dependiente (LoadFilterObjects() puebla desde las listas database.Used*
â€” los caches de ids-en-uso que GameDatabase mantiene, ver S28.2).

Las estadisticas calculadas viven en clases planas anidadas (esto es lo mas
parecido a "GameStatistics" que existe â€” no hay clase de nivel superior separada):
  class GameStats
    List<BaseStatInfo> TopPlayed          (top 50 por Playtime, incluye Game)
    List<BaseStatInfo> CompletionStates   (conteo por CompletionStatus, desc)
    List<BaseStatInfo> GameProvider       (conteo por library plugin, desc)
    ulong TotalCount
    BaseStatInfo Installed, NotInstalled, Hidden, Favorite
    ulong TotalPlayTime, AvaragePlayTime, TotalInstallSize
  class BaseStatInfo { string Name; ulong Value; int Percentage; Game Game; }

GlobalStats/FilteredStats son dos instancias paralelas calculadas por el mismo
metodo FillData(bool filtered) â€” una sobre toda la biblioteca (sin ocultos salvo
que IncludeHidden), otra ademas filtrada por PassesFilter(game) segun el
SelectedFilter/SelectedFilterObject actual. Calculate() es el punto de entrada
(GlobalStats = FillData(false); FilteredStats = FillData(true);). El toggle
IncludeHidden vuelve a disparar Calculate(). Comandos: NavigateToGameCommand,
NavigateBackCommand.

GameListItem / estructura del item de lista: GameListItem
(Playnite.DesktopApp/Controls/GameListItem.cs) es un CONTROL de WPF (elemento de
UI con template, TemplateParts PART_PanelHost, PART_ImageIcon, PART_ImageCover,
PART_ButtonPlay, PART_ButtonInfo), no una clase de datos â€” no tiene lista de
campos propia, hace binding a un GamesCollectionViewEntry como DataContext.

El verdadero wrapper de datos de "item de lista/grid/details" es
GamesCollectionViewEntry (Playnite/GamesCollectionViewEntry.cs) â€” envuelve un
Game + LibraryPlugin + PlayniteSettings y re-expone/deriva todo lo que la UI
bindea (es la proyeccion del CollectionView, una por juego, regenerada/
refrescada en Game.PropertyChanged):
  - Identidad/passthrough: LibraryPlugin, Id, PluginId, GameId, ReleaseDate,
    ReleaseYear, LastActivity, Links, Icon, CoverImage, BackgroundImage, Hidden,
    Favorite, InstallDirectory, GameActions, DisplayName, Description, Notes,
    IsInstalled, IsInstalling, IsUninstalling, IsLaunching, IsRunning,
    IsCustomGame, Playtime, Added, Modified, PlayCount, InstallSize, Version,
    UserScore, CriticScore, CommunityScore, *ScoreGroup/*ScoreRating (x3 cada
    uno), *Segment (LastActivity/Added/Modified/RecentActivity),
    PlaytimeCategory, InstallationState, RecentActivity, OverrideInstallState,
    Roms, RomList (string de display), y cada lista *Ids (CategoryIds,
    GenreIds, DeveloperIds, PublisherIds, TagIds, SeriesIds, AgeRatingIds,
    RegionIds, SourceId, PlatformIds, FeatureIds, CompletionStatusId).
  - Colecciones de objetos resueltos envueltas en un ComparableDbItemList<T>
    custom (para estabilidad de agrupado/ordenado en CollectionView): Tags,
    Features, Genres, Developers, Publishers, Categories, AgeRatings, Series,
    Regions, Platforms.
  - Propiedades de "grupo actual" de valor unico, usadas solo en vistas
    avanzadas/agrupadas, con default al sentinel Empty de cada tipo: Serie,
    Platform, Region, Source, CompletionStatus, AgeRating, Category, Genre,
    Developer, Publisher, Tag, Feature (settable solo via el factory estatico
    GetAdvancedGroupedEntry, uno por cada colGroupType soportado).
  - Accesores de objetos de imagen (cada uno resuelve via ImageSourceManager,
    cacheado vs no-cacheado, con BitmapLoadProperties por modo de vista â€”
    DetailsListIconProperties, GridViewCoverProperties, BackgroundImageProperties,
    FullscreenListCoverProperties, todos estaticos y reinicializados via
    InitItemViewProperties): LibraryIcon, IconObject(Cached),
    CoverImageObject(Cached), Default*Object(Cached), DisplayBackgroundImage
    (Object), DetailsListIconObjectCached, GridViewCoverObjectCached,
    Default*ObjectCached, FullscreenListItemCoverObject,
    DefaultFullscreenListItemCoverObject.
  - Name (display) cae de SortingName a Name si SortingName esta vacio;
    NameGroup (letra de agrupado alfabetico) delega en Game.GetNameGroup().
  - Expone el Game subyacente (Game Game { get; }) y string Library { get; }
    (nombre de plugin resuelto, o literal "Playnite" si no hay plugin).
  - Operador de conversion explicito: explicit operator Game(GamesCollectionViewEntry e).

--------------------------------------------------------------------------------
28.6 HALLAZGOS QUE CONTRADICEN SUPUESTOS PREVIOS DE ESTE DOCUMENTO

1. La persistencia es LiteDB, no JSON por entidad (ver S28.2) â€” la correccion mas
   importante de esta seccion.
2. "LinkItem" no existe â€” la clase se llama Link.
3. No existe "MetadataImage" â€” Icon, CoverImage y BackgroundImage de GameMetadata
   usan todos la misma clase MetadataFile.
4. No existe una clase "GameStatistics" dedicada â€” se calcula enteramente dentro
   de StatisticsViewModel en clases anidadas locales (GameStats, BaseStatInfo).
5. Developer/Publisher son subclases casi vestigiales de Company â€” la coleccion
   real de la DB es una unica IItemCollection<Company> Companies compartida;
   Developer/Publisher no agregan campos, existen sobre todo por conveniencia de
   tipado en MetadataProperty para codigo de plugins, no como storage separado.
6. La "base de datos" de emulacion SI es SQLite de verdad (via el wrapper
   micro-ORM SqlNado, tabla DatGame) â€” coincide con lo asumido â€” pero se
   consulta con SQL armado a mano por interpolacion de strings (escapado manual,
   no queries parametrizadas), un archivo .db por nombre de base de datos de
   plataforma emulada (ej. nointro-nes.db), bajo PlaynitePaths.EmulationDatabasePath.
7. Los valores del enum GameField NO son secuenciales, tienen huecos (ej. saltan
   de 4 a 9, se saltean 8, 13, 14, 15, 32, 34, 35, 46-49, 52) â€” claramente
   refleja eliminaciones historicas de campos; si se porta este enum 1:1 a
   Bridge, no asumir que la numeracion importa (no parece ser [Flags] ni estar
   serializado en datos persistentes â€” se usa sobre todo en diffing en memoria â€”
   pero conviene verificar antes de confiar en este supuesto para Bridge).
8. El flujo ImportGames estandar (sin HasCustomizedGameImport) re-sincroniza MUY
   poco de un juego ya existente. En un re-import, solo se tocan IsInstalled,
   InstallDirectory, Playtime/LastActivity (segun modo), InstallSize, y estado
   de completado â€” Name, imagenes, generos, descripcion, etc. NUNCA se
   refrescan por un re-escaneo de biblioteca, solo por la pasada separada de
   MetadataDownloader. Este es un contrato de comportamiento importante a
   replicar (o cambiar deliberadamente) en Bridge.
9. Los fondos (BackgroundImage) pueden quedar como URLs http sin materializar en
   la DB (Game.BackgroundImage puede contener literalmente una URL viva, no una
   ruta relativa a files/) cuando
   PlayniteSettings.DownloadBackgroundsImmediately == false â€” por eso
   GamesCollection tiene ramas especiales con IsHttpUrl() para limpiar cache en
   vez de borrar el archivo local. Icon y CoverImage no tienen ese camino
   perezoso â€” siempre se materializan.

--------------------------------------------------------------------------------
28.7 (continua en 28.8) â€” LOS HUECOS DE LA PRIMERA PASADA (GameAction, ejecucion,
tracking de playtime, settings, filtros, edicion multiple, notificaciones) se
cerraron en una segunda pasada de investigacion, ver 28.8 en adelante.

--------------------------------------------------------------------------------
28.8 GameAction Y GameRom â€” ESTRUCTURA EXACTA

PlayniteSDK/Models/GameAction.cs (lineas 1-436)

enum TrackingMode (lineas 15-42), valores:
  Default = 0         â€” "Playnite intenta usar el mejor modo automaticamente."
  Process = 1         â€” se trackea el proceso original y todos sus hijos
  Directory = 2       â€” se trackea cualquier proceso que corra desde una carpeta dada
  OriginalProcess = 3 â€” se trackea SOLO el proceso originalmente lanzado
  ProcessName = 4     â€” se trackea cualquier proceso por nombre de proceso dado

enum GameActionType (lineas 47-69): File = 0, URL = 1, Emulator = 2, Script = 3.

class GameAction : ObservableObject, IEquatable<GameAction> (linea 74):
  GameActionType Type
  string Arguments              (argumentos del ejecutable, tipo File)
  string AdditionalArguments    (argumentos adicionales, tipo Emulator)
  bool OverrideDefaultArgs      (si true, sobreescribe totalmente los argumentos
                                  del emulador con los de la accion; solo aplica a
                                  tipo Emulator)
  string Path                   (ruta del ejecutable [File] o URL [URL])
  string WorkingDir             (directorio de trabajo, tipo File)
  string Name
  bool IsPlayAction
  Guid EmulatorId
  string EmulatorProfileId
  TrackingMode TrackingMode = Default
  string TrackingPath
  string Script                 (script de arranque, tipo Script)
  int InitialTrackingDelay = 0  (delay en ms antes de empezar a trackear)
  int TrackingFrequency = 2000  (intervalo de polling en ms)

Uso de campos por tipo:
  File:     Path, Arguments, WorkingDir, TrackingMode, TrackingPath,
            InitialTrackingDelay, TrackingFrequency
  URL:      solo Path (la URL) se usa para lanzar (ProcessStarter.StartUrl); el
            tracking cae a tracking por directorio de InstallDirectory si
            TrackingMode es Default (una URL no tiene proceso propio que trackear)
  Emulator: EmulatorId, EmulatorProfileId, OverrideDefaultArgs, Arguments (solo
            si OverrideDefaultArgs), AdditionalArguments (se agrega a los args
            del perfil si no). Path/WorkingDir NO se usan â€” el ejecutable/workdir
            salen del EmulatorProfile resuelto.
  Script:   solo Script (texto PowerShell); Path/Arguments no se usan.

Subclase en tiempo de ejecucion (no persistida): EmulationPlayAction : GameAction
(Playnite/Controllers/GenericGameController.cs lineas 28-32) agrega
EmulatorProfile SelectedEmulatorProfile y string SelectedRomPath â€” es la forma
resuelta y lista-para-lanzar que se arma en el momento de jugar a partir de un
GameAction crudo de tipo Emulator.

GameRom â€” PlayniteSDK/Models/GameRom.cs (lineas 1-103)
class GameRom : ObservableObject, IEquatable<GameRom> â€” solo dos campos de datos:
  string Name
  string Path
Nada mas â€” sin tamaÃ±o, sin checksum, sin lista de extensiones. (El checksum/CRC
vive en el flujo de escaneo de S28.4, no en GameRom.)

--------------------------------------------------------------------------------
28.9 EJECUCION REAL DE PLAY / INSTALL / UNINSTALL

Jerarquia de controladores:
  - SDK: Playnite.SDK.Plugins.ControllerBase (PlayniteSDK/Plugins/Actions.cs
    lineas 91-118: Name, Game, contexto de ejecucion interno), y clases abstractas
    PlayController, InstallController, UninstallController (mismo archivo, lineas
    123-255) â€” cada una con metodos abstractos Play/Install/Uninstall y helpers
    protegidos InvokeOnStarted/Stopped/Installed/InstallationCancelled/Uninstalled
    que reenvian el evento al SynchronizationContext capturado (para volver al
    hilo de UI).
  - Playnite.SDK.Plugins.AutomaticPlayController : PlayController (sellada,
    lineas 29-86) â€” un controlador de conveniencia para plugins con
    AutomaticPlayActionType Type (File/Url), TrackingMode, TrackingPath,
    Arguments, Path, WorkingDir, InitialTrackingDelay, TrackingFrequency. Su
    propio Play() no hace nada â€” Playnite detecta este tipo y arma internamente
    un GenericPlayController para correrlo de verdad.
  - Implementacion generica concreta: Playnite.Controllers.GenericPlayController
    : PlayController (Playnite/Controllers/GenericGameController.cs lineas
    34-820). Es el UNICO controlador usado para GameAction nativos y para
    EmulationPlayAction (tambien maneja AutomaticPlayController via
    Start(AutomaticPlayController), linea 460).
  - Orquestacion: Playnite.Controllers.GameControllerFactory
    (Playnite/Controllers/GameControllerFactory.cs lineas 15-206) â€” mantiene
    List<PlayController> PlayControllers, List<InstallController>
    InstallControllers, List<UninstallController> UninstallControllers; reenvia
    los eventos Started/Stopped/Installed/Uninstalled/InstallationCancelled de
    cada controlador como eventos propios.
  - Driver de mas alto nivel: Playnite.GamesEditor (Playnite/GamesEditor.cs
    lineas 72-1841), subclaseado por Playnite.DesktopApp.DesktopGamesEditor
    (Playnite.DesktopApp/DesktopGamesEditor.cs, solo 76 lineas â€” apenas agrega
    SetGameCategories, SetGamesCategories, EditGame(s)). TODA la logica de play/
    install/uninstall/tracking vive en la clase base GamesEditor, COMPARTIDA
    entre Desktop y Fullscreen. No hay DesktopPlayController/
    FullscreenPlayController separados â€” ambas apps usan exactamente el mismo
    pipeline de controladores; solo difiere el IActionSelector
    (DesktopActionSelector vs FullscreenActionSelector, que muestran dialogos de
    seleccion distintos cuando hay varias acciones/controladores candidatos).

PLAY â€” paso a paso (GamesEditor.PlayGame, Playnite/GamesEditor.cs lineas 189-422):
  1. Si !game.IsInstalled -> redirige a InstallGame(game) y retorna.
  2. Recarga el Game autoritativo desde Database.Games.Get(game.Id); aborta con
     dialogo de error si no existe.
  3. Guard: si game.IsRunning || game.IsLaunching -> log warning y retorna (sin
     doble lanzamiento).
  4. Llama GetPlayActions(game) (lineas 1682-1795) que devuelve
     Tuple<List<PlayController>, List<GameAction>>:
     - Item1 = controladores aportados por plugins de biblioteca/genericos
       (plugin.Plugin.GetPlayActions(...)).
     - Item2 = las GameAction propias del juego filtradas a IsPlayAction == true.
       Para acciones tipo Emulator con EmulatorId == Guid.Empty ("elegir emulador
       al iniciar"), se expande en una EmulationPlayAction por cada combinacion
       compatible de emulador x perfil x ROM (via
       game.GetCompatibleEmulators(Database)); para un EmulatorId/
       EmulatorProfileId especifico se resuelve el unico EmulatorProfile que
       matchea y, si hay mas de una ROM (game.Roms.Count > 1), se emite una
       accion por ROM.
     - Si ambas listas estan vacias -> error LOC.ErrorNoPlayAction.
  5. Resolucion de accion: si Item1.Count + Item2.Count > 1 ->
     actionSelector.SelectPlayAction(Item1, Item2) (muestra un dialogo picker:
     ActionSelectionViewModel en Desktop, FullscreenActionSelector en
     Fullscreen). Si no, se usa directamente el unico candidato disponible â€” no
     hay heuristica de "primer match" basada en IsPlayAction mas alla del filtro
     .Where(a => a.IsPlayAction) del paso 4. O sea: IsPlayAction decide
     elegibilidad, el selector (o "solo un candidato") decide cual corre.
  6. Se crea un PowerShellRuntime por juego y se guarda en
     scriptRuntimes[game.Id] (cae a un DummyPowerShellRuntime si no hay
     PowerShell 5.1 instalado, con un aviso de advertencia una sola vez).
  7. Construccion del controlador segun el tipo de accion resuelta:
     - AutomaticPlayController -> se envuelve en new GenericPlayController(...).
     - PlayController de plugin -> se usa directo.
     - EmulationPlayAction o GameAction plano -> se envuelve en
       new GenericPlayController(...).
  8. Cualquier otro controlador de plugin en Item1 que no se eligio se
     Dispose()ea.
  9. controllers.RemovePlayController(game.Id); controllers.AddController(...);
     RunningGames.Add(...); UpdateGameState(game.Id, null, null, null, null,
     launching: true).
  10. Dispara controllers.InvokeOnStarting(this, startingArgs) (un
      OnGameStartingEventArgs { Game, SourceAction, SelectedRomFile },
      PlayniteSDK/Events/ApplicationEvents.cs lineas 128-149) â€” las extensiones
      pueden setear startingArgs.CancelStartup = true para abortar aca.
  11. Si el plugin dueÃ±o del juego tiene un timer pendiente de apagado de
      cliente, se cancela.
  12. HDR: si es el primer juego HDR-controlado que se lanza, guarda
      wasHdrEnabled = HdrUtilities.IsHdrEnabled(); si game.EnableSystemHdr,
      fuerza HDR.
  13. Corre el PreScript GLOBAL (AppSettings.PreScript, condicionado a
      game.UseGlobalPreScript) y luego el PreScript POR JUEGO (game.PreScript,
      siempre se intenta) via ExecuteScriptAction(...); si cualquiera falla o
      setea CancelStartup, se aborta.
  14. Despacho al controlador segun el tipo:
      - GenericPlayController + EmulationPlayAction ->
        genCtrl.StartEmulator(emuAct, asyncExec: true, startingArgs).
      - GenericPlayController + AutomaticPlayController ->
        genCtrl.Start(autoAction).
      - GenericPlayController + GameAction plano ->
        genCtrl.Start(act, asyncExec: true, startingArgs).
      - PlayController de plugin -> controller.Play(new PlayActionArgs()).
  15. UpdateJumpList() al final.

MECANICA DE LANZAMIENTO â€” GenericPlayController.Start(GameAction, bool asyncExec,
OnGameStartingEventArgs) (GenericGameController.cs lineas 481-668):
  1. Clona el juego y la accion; action = action.ExpandVariables(gameClone)
     (expande {InstallDir} etc.); resuelve/repara action.Path y
     action.WorkingDir via CheckPath(...) (cae a buscar la misma ruta en otra
     letra de unidad si la original no existe, con warning).
  2. Si Type == Script -> corre el script PowerShell via RunStartScript(...) y
     retorna.
  3. Si no, lanza el proceso:
     - Type == File -> Process proc = ProcessStarter.StartProcess(action.Path,
       action.Arguments, action.WorkingDir) â€” un wrapper fino de
       Process.Start(new ProcessStartInfo(...)) (Playnite/Common/
       ProcessStarter.cs lineas 95-121).
     - Type == URL -> Process proc = ProcessStarter.StartUrl(action.Path) â€”
       Process.Start(url), con fallback a cmd /C start {url} si la llamada
       directa lanza excepcion (ProcessStarter.cs lineas 70-83).
  4. Caso especial UWP: si action.Path == "explorer.exe" y Arguments matchea
     shell:AppsFolder\(.+)!.+ (regex), Playnite NO confia en el proceso
     explorer.exe lanzado (no es el juego) â€” en vez de eso resuelve el
     directorio de trabajo real via Programs.GetUWPApps().FirstOrDefault(a =>
     a.AppId == gameClone.GameId).WorkDir y trackea ese directorio con
     MonitorDirectory en vez del PID.
  5. Si no, despacho de tracking segun action.TrackingMode:
     - Default: para acciones no-URL (y sin el caso especial UWP), si
       proc != null dispara InvokeOnStarted(new GameStartedEventArgs {
       StartedProcessId = proc.Id }) de inmediato y trackea con
       MonitorProcessTree(proc.Id) (proceso + todos los descendientes). Para
       acciones URL bajo Default, no hay proceso que trackear â€” en vez de eso
       monitorea gameClone.InstallDirectory via MonitorDirectory.
     - Process: igual que el caso de proceso de Default â€”
       MonitorProcessTree(proc.Id).
     - OriginalProcess: MonitorProcess(proc) â€” solo el PID exacto lanzado, sin
       hijos.
     - Directory: MonitorDirectory(action.TrackingPath ??
       gameClone.InstallDirectory).
     - ProcessName: MonitorProcessName(action.TrackingPath) â€” matchea por
       Process.ProcessName.
     - Para los casos Directory/ProcessName, el proceso LANZADO nunca se asume
       que es el juego â€” StartTracking recibe una funcion startupCheck que
       hace polling hasta que aparece un proceso que matchea, y recien ahi
       dispara GameStartedEventArgs. Este es exactamente el caso de "el
       launcher lanza un hijo y se cierra": launchers de Steam/Epic/GOG o
       frontends de emuladores.
  6. Las acciones tipo Emulator se rechazan explicitamente aca (throw
     "Cannot start emulator using this configuration.") â€” van por el flujo
     StartEmulator separado.

LANZAMIENTO DE EMULADOR â€” GenericPlayController.StartEmulator(EmulationPlayAction,
bool asyncExec, OnGameStartingEventArgs) (GenericGameController.cs lineas 72-219):
  1. Busca el Emulator en database.Emulators[action.EmulatorId]; lo clona;
     expande {PlayniteDir} en emulator.InstallDir y verifica que exista.
  2. Determina currentEmuProfile â€” un CustomEmulatorProfile o
     BuiltInEmulatorProfile desde action.SelectedEmulatorProfile.
  3. Expande romPath = Game.ExpandVariables(action.SelectedRomPath, ...),
     verifica que exista (CheckPath).
  4. Rama de perfil CUSTOM: expandedProfile =
     emuProf.ExpandVariables(Game, emulator.InstallDir, romPath); valida que
     Executable/WorkingDirectory existan.
     - Si expandedProfile.StartupScript esta seteado -> lo corre como script de
       arranque PowerShell en vez de lanzar un proceso directo (RunStartScript,
       variables incluyen Emulator, EmulatorProfile, RomPath).
     - Si no, calcula startupArgs: si action.OverrideDefaultArgs usa
       action.Arguments (expandido); si no usa expandedProfile.Arguments mas
       action.AdditionalArguments agregado (expandido). startupPath/startupDir
       vienen del perfil, NO de action.Path/WorkingDir. Llama
       StartEmulatorProcess(...) con expandedProfile.TrackingMode/TrackingPath.
  5. Rama de perfil BUILT-IN: resuelve una definicion hardcodeada via
     Emulation.GetProfile(emulator.BuiltInConfigId, builtIn.BuiltInProfileName).
     - Si profileDef.ScriptStartup -> corre
       Emulation.GetStartupScriptPath(def) como archivo de script.
     - Si no, startupPath = Emulation.GetExecutable(emulator.InstallDir,
       profileDef, true) (busqueda del ejecutable por regex via
       profileDef.StartupExecutable), args desde builtIn.OverrideDefaultArgs ?
       builtIn.CustomArguments : profileDef.StartupArguments mas
       AdditionalArguments. El tracking se fuerza a TrackingMode.Process para
       los built-in.
  6. StartEmulatorProcess(path, args, workDir, emulatorDir, romPath, asyncExec,
     emulator, profile, trackingMode, trackingPath) (lineas 221-328):
     - Corre el PreScript propio del emulador (del perfil) antes de lanzar, el
       PostScript justo despues de confirmar el arranque, el ExitScript cuando
       el tracking detecta que termino â€” todo via ExecuteEmulatorScript(...)
       (un helper de ejecucion PowerShell distinto de
       GamesEditor.ExecuteScriptAction).
     - process = ProcessStarter.StartProcess(path, args, workDir); ante
       Win32Exception con NativeErrorCode == 2 (archivo no encontrado) lanza un
       FileNotFoundException mas amigable.
     - El despacho de tracking espeja la logica de Start(GameAction,...):
       Default/Process -> MonitorProcessTree; OriginalProcess ->
       MonitorProcess; Directory -> MonitorDirectory; ProcessName ->
       MonitorProcessName. El closure gameStoppedAction de cada camino dispara
       el ExitScript del emulador.

INSTALL / UNINSTALL â€” NO hay un tipo de accion separado. Confirmado por lectura
exhaustiva de GameAction.cs y Actions.cs: instalar/desinstalar se maneja
enteramente via objetos InstallController/UninstallController provistos por
plugins (clases abstractas, PlayniteSDK/Plugins/Actions.cs lineas 123-199),
obtenidos via plugin.Plugin.GetInstallActions(...) / GetUninstallActions(...).
Playnite mismo NO tiene ningun instalador integrado para acciones File/URL/
Emulator â€” un juego agregado manualmente, o un juego de biblioteca cuyo plugin no
provee un InstallController, simplemente NO se puede "instalar" a traves de
Playnite; el usuario debe instalarlo externamente y Playnite solo cambia
IsInstalled cuando se le informa. No existe un controlador generico/local de
install/uninstall equivalente a GenericPlayController.

GamesEditor.InstallGame(Game) (GamesEditor.cs lineas 1112-1156):
  1. GetInstallActions(game) junta InstallController de todos los plugins
     (lineas 1797-1818).
  2. Si hay mas de un candidato -> actionSelector.SelectInstallAction(...); si
     no, el unico.
  3. controllers.AddController(controller); UpdateGameState(...,
     installing: true, ...); controller.Install(new InstallActionArgs()).
  4. En el evento async Installed (Controllers_Installed, lineas 1563-1591):
     setea dbGame.IsInstalling = false; dbGame.IsInstalled = true; y, si el
     controlador aporto GameInstalledEventArgs.InstalledInfo (un
     GameInstallationData { string InstallDirectory; List<GameRom> Roms; },
     Actions.cs lineas 363-374), copia InstallDirectory/Roms al juego, despues
     recalcula InstallSize (UpdateGameSize) y persiste.

GamesEditor.UnInstallGame(Game) (GamesEditor.cs lineas 1158-1210) espeja
exactamente esto usando UninstallController/GetUninstallActions/
Controllers_Uninstalled (lineas 1604-1615), pero ademas guarda
if (game.IsRunning || game.IsLaunching) -> dialogo de error, no desinstala
mientras corre. Al completar setea dbGame.IsUninstalling = false;
dbGame.IsInstalled = false; dbGame.InstallDirectory = string.Empty;.

SCRIPTS Pre/Post/GameStarted â€” puntos de invocacion y motor:
Motor: POWERSHELL, via Playnite.Scripting.PowerShell.PowerShellRuntime (implementa
IPowerShellRuntime), instanciado por juego como
new PowerShellRuntime($"{game.Name} {game.Id} runtime") y guardado en
scriptRuntimes[game.Id] (un ConcurrentDictionary<Guid, IPowerShellRuntime>,
GamesEditor.cs linea 80). Se chequea PowerShellRuntime.IsInstalled antes de usar;
si no hay PowerShell 5.1 se sustituye un DummyPowerShellRuntime no-op con aviso
una sola vez.

Llamada real: GamesEditor.ExecuteScriptAction(IPowerShellRuntime runtime, string
script, Game game, bool execute, bool global, GameScriptType type,
Dictionary<string,object> vars) (GamesEditor.cs lineas 1617-1680) â€” arma
scriptVars = { "PlayniteApi": ..., "Game": game.GetCopy() } mergeado con vars del
caller, expande variables {...} en el texto del script, y llama
runtime.Execute(expandedScript, workDir, scriptVars) donde workDir es el
InstallDirectory expandido si existe, si no PlaynitePaths.ProgramPath.

enum GameScriptType (GamesEditor.cs lineas 34-44): Starting, Started, Exit, None
â€” se usa solo para el titulo del dialogo de error.

Orden respecto al lanzamiento (confirmado en PlayGame y los handlers
Controllers_Started/Controllers_Stopped):
  1. PreScript GLOBAL (AppSettings.PreScript, solo si game.UseGlobalPreScript) â€”
     antes de invocar el controlador de play (PlayGame linea 358).
  2. PreScript POR JUEGO (game.PreScript, siempre se intenta) â€” justo despues,
     todavia antes de correr el controlador (linea 370). Si cualquiera de los
     dos falla, o setea startingArgs.CancelStartup = true, se aborta el
     arranque (deshace el estado "launching", dispara
     controllers.InvokeOnGameStartupCancelled).
  3. El controlador lanza el proceso/emulador/script (genCtrl.Start(...) /
     StartEmulator(...)).
  4. En el evento Started del controlador -> Controllers_Started
     (GamesEditor.cs lineas 1365-1414): corre game.GameStartedScript y despues
     AppSettings.GameStartedScript (condicionado a
     game.UseGlobalGameStartedScript) â€” ambos disparan solo despues de que el
     tracking confirma que el proceso del juego arranco de verdad (o sea,
     despues de InvokeOnStarted).
  5. En el evento Stopped del controlador -> Controllers_Stopped
     (GamesEditor.cs lineas 1416-1561): corre el PostScript POR JUEGO y despues
     el PostScript GLOBAL (condicionado a game.UseGlobalPostScript) (lineas
     1480-1481), despues de que el playtime ya se sumo a dbGame.Playtime.

Los scripts especificos de emulador (CustomEmulatorProfile.PreScript/
PostScript/ExitScript, distintos de los scripts a nivel juego de arriba) se
invocan desde adentro de GenericPlayController.StartEmulatorProcess/
ExecuteEmulatorScript (GenericGameController.cs lineas 240, 263, 273, etc.) â€”
PreScript justo antes de ProcessStarter.StartProcess, PostScript justo despues
de que el tracking confirma el arranque, ExitScript cuando el loop de tracking
detecta que el proceso/arbol/directorio/nombre ya no esta corriendo.

--------------------------------------------------------------------------------
28.10 TRACKING DE PLAYTIME â€” EL MECANISMO REAL DE MONITOREO DE PROCESOS

Clases de monitoreo (Playnite/Common/ProcessMonitor.cs, archivo completo,
lineas 1-197):
  - MonitorProcess(Process process) â€” IsProcessRunning() => !process.HasExited.
    Trackea el handle de Process EXACTO lanzado (modo OriginalProcess).
  - MonitorProcessTree(int originalId) â€” mantiene una List<int> relatedIds
    creciente, empezando con el PID lanzado. Cada poll (IsProcessTreeRunning())
    enumera TODOS los procesos del SO (Process.GetProcesses().Where(a =>
    a.SessionId != 0)), usa proc.TryGetParentId(out var parent) para caminar
    relaciones padre->hijo, y agrega cualquier proceso cuyo padre ya este en
    relatedIds. Asi se manejan los launchers que generan hijos bajo tracking
    Process/Default: el arbol crece para incluir hijos, y relatedIds se
    reconstruye en cada tick con solo los que siguen vivos; devuelve
    relatedIds.Count > 0.
  - MonitorDirectory(string directory) â€” resuelve la ruta final via
    Paths.GetFinalPathName (resolucion de junctions/symlinks) y agrega un
    separador final para evitar que C:\Fallout matchee C:\Fallout 2\.
    IsProcessRunning() escanea TODOS los procesos y devuelve el PID del primero
    cuyo TryGetMainModuleFileName(out var procPath) empiece con el directorio
    trackeado (case-insensitive). IsTrackable() solo chequea que el directorio
    exista.
  - MonitorProcessName(string processName) (declarada fuera del namespace
    Playnite.Common, mismo archivo lineas 168-197) â€” escanea todos los procesos
    buscando process.ProcessName == ProcessName, devuelve su PID.
  - MonitorProcessNames(string directory) (plural, lineas 67-117) â€” un matcher
    de nombres de ejecutable derivado de un directorio: enumera todos los
    *.exe bajo el directorio (recursivo) y matchea procesos corriendo por
    nombre de archivo o nombre de proceso. (Esta clase existe pero NO es la que
    se conecta a TrackingMode.ProcessName en GenericPlayController â€” esa usa la
    MonitorProcessName singular por nombre de proceso literal desde
    TrackingPath.)

DETECCION DE INICIO â€” NO siempre confia en lo que devuelve Process.Start:
  Para TrackingMode.Process/Default/OriginalProcess, el objeto Process
  devuelto por ProcessStarter.StartProcess/StartUrl SI se confia como el juego
  y GameStartedEventArgs dispara de inmediato con su PID
  (GenericGameController.cs lineas 568, 602, 618, y el equivalente de emulador
  en linea 269/277).
  Para TrackingMode.Directory/ProcessName (y el caso especial UWP-explorer, y
  cualquier TrackingMode.Default sobre una accion URL), el proceso lanzado (si
  existe) explicitamente NO se confia â€” en vez de eso StartTracking(...) recibe
  una funcion startupCheck no-nula que hace polling (MonitorDirectory.
  IsProcessRunning() / MonitorProcessName.IsProcessRunning()) cada
  trackingFrequency ms en loop hasta encontrar un PID > 0, y recien ahi invoca
  gameStartedAction(id) y dispara GameStartedEventArgs { StartedProcessId = id }.
  Este es exactamente el caso "el launcher genera el juego real y se cierra."

DETECCION DE PARADA, Y EL LOOP DE TRACKING â€”
GenericPlayController.StartTracking (GenericGameController.cs lineas 670-775):
  Firma: StartTracking(Func<bool> trackingAction, Func<int> startupCheck = null,
  Action<int> gameStartedAction = null, Action gameStoppedAction = null,
  int trackingFrequency = 2000, int trackingStartDelay = 0). Es un
  Task.Run(async () => {...}) de fire-and-forget:
  1. Lanza excepcion si watcherToken != null (ya esta trackeando).
  2. Task.Delay(trackingStartDelay, ...) opcional antes de todo (esto es
     TrackingFrequency/InitialTrackingDelay del GameAction).
  3. Fase de ARRANQUE (solo si startupCheck != null): loop llamando
     startupCheck() cada trackingFrequency ms hasta que devuelve id > 0; hasta
     maxFailCount = 5 excepciones consecutivas antes de rendirse y disparar
     GameStoppedEventArgs(0).
  4. Fase de EJECUCION: loop infinito â€”
     - trackingWatch.Restart(), llama trackingAction() (el predicado
       IsProcessRunning()/IsProcessTreeRunning()). Si devuelve false -> llama
       gameStoppedAction?.Invoke() y despues
       InvokeOnStopped(new GameStoppedEventArgs(playTimeMs / 1000)) y sale del
       loop â€” ESTA ES LA DETECCION REAL DE "EL JUEGO SE DETUVO": polling, NO un
       evento nativo de fin de proceso (Process.Exited).
     - Ante excepcion, incrementa failCount; despues de 5 fallos consecutivos,
       para y reporta.
     - await Task.Delay(trackingFrequency, ...); despues
       trackingWatch.Stop(). Guard de suspension/hibernacion: si el tiempo real
       transcurrido supera trackingFrequency + 30_000 ms, el tick se descarta
       (continue) sin sumar a playTimeMs â€” esto compensa que el sistema se haya
       suspendido para que el playtime no se corrompa.
     - Si no, playTimeMs += (ulong)trackingWatch.ElapsedMilliseconds â€” ESTE ES
       EL STOPWATCH EN VIVO: el playtime se acumula puramente de los deltas del
       intervalo de polling de este loop en memoria, no de timestamps DateTime
       en este acumulador especifico (aunque existe un mecanismo paralelo de
       timestamps, ver abajo).
  5. watcherToken.Cancel() (en Dispose()) para el loop; Dispose() tambien hace
     playTask?.Wait(5000) para darle tiempo a un script de arranque de cerrar
     bien.

SEGUNDA CONTABILIDAD DE PLAYTIME, INDEPENDIENTE, EN GamesEditor (respaldo/
seguridad ante crash):
  gameStartups es un ConcurrentDictionary<Guid, DateTime> (GamesEditor.cs linea
  79). En Controllers_Started (linea 1370):
  gameStartups.TryAdd(game.Id, DateTime.Now). Esto NO se usa para calcular el
  playtime final en una parada normal (Controllers_Stopped usa
  args.SessionLength, el valor derivado de playTimeMs de StartTracking) â€” pero
  SI lo usa CancelGameMonitoring(Game game) (GamesEditor.cs lineas 1270-1308),
  el metodo que el usuario dispara via el dialogo "cancelar monitoreo"
  (CheckSetupCommand/CheckExecutionCommand en GameDetailsViewModel, ver S28.11).
  Ahi: ellapsedTime = (DateTime.Now - startupTime).TotalSeconds se calcula
  desde el timestamp de reloj de pared y se suma a dbGame.Playtime â€” un camino
  de respaldo independiente del acumulador de StartTracking, usado
  especificamente cuando el usuario fuerza manualmente la cancelacion del
  tracking.

PUNTOS DE ACTUALIZACION de Playtime/LastActivity/PlayCount:
  - GamesEditor.UpdateGameState(Guid id, bool? installed, bool? running, bool?
    installing, bool? uninstalling, bool? launching) (GamesEditor.cs lineas
    1310-1363): cuando running == true, setea game.LastActivity = DateTime.Now
    y game.PlayCount += 1 â€” PLAYCOUNT SE INCREMENTA AL INICIAR, NO AL PARAR.
    Tambien hace aca la auto-actualizacion de estado de completado: lee
    Database.GetCompletionStatusSettings(); si comSettings.PlayedStatus ==
    Constants.MaxGuidVal no hace nada; si no, si game.CompletionStatusId ==
    Guid.Empty || game.CompletionStatusId == comSettings.DefaultStatus entonces
    game.CompletionStatusId = comSettings.PlayedStatus.
  - Controllers_Started (linea 1369): llama UpdateGameState(game.Id, null,
    true, null, null, false) (setea IsRunning=true, limpia IsLaunching), que es
    lo que dispara la logica de PlayCount/LastActivity/estado-de-completado de
    arriba.
  - Controllers_Stopped (GamesEditor.cs lineas 1416-1561): dbGame.IsRunning =
    false; dbGame.IsLaunching = false; dbGame.Playtime += args.SessionLength;
    despues Database.Games.Update(dbGame) â€” AQUI ES DONDE PLAYTIME (ulong,
    segundos) SE ACUMULA DE VERDAD, en una sola suma al parar, con origen en
    GameStoppedEventArgs.SessionLength que viene del acumulador
    playTimeMs / 1000 de StartTracking.

RECUPERACION ANTE CRASH / CIERRE ABRUPTO: NO EXISTE mecanismo de reanudacion de
sesion. Dos datos relevantes:
  1. El constructor de GamesCollection (Playnite/Database/Collections/
     GamesCollection.cs lineas 15-21) registra una funcion de transformacion
     que corre sobre CADA Game cargado desde LiteDB: game.IsInstalling = false;
     game.IsUninstalling = false; game.IsLaunching = false; game.IsRunning =
     false;. O sea, en cada arranque de Playnite, cualquier juego que estaba a
     mitad de sesion (o a mitad de install/uninstall) cuando Playnite cerro/
     crasheo por ultima vez simplemente tiene sus flags transitorios reseteados
     a false en silencio â€” sin dialogo, sin recuperacion de tiempo transcurrido,
     y (porque los acumuladores playTimeMs/gameStartups viven solo en memoria
     del proceso ya muerto) CUALQUIER PLAYTIME PARCIAL DE ESA SESION ABORTADA
     SE PIERDE PARA SIEMPRE, no se recupera ni se estima.
  2. PlaynitePaths.SafeStartupFlagFile ("safestart.flag") NO tiene relacion con
     sesiones de juego â€” es un guardian anti-crash-loop del propio Playnite: se
     crea un archivo flag al arrancar y se borra al cerrar limpio; si sigue
     presente en el proximo arranque, se le pregunta al usuario si quiere
     arrancar en "modo seguro" con todas las extensiones/temas de terceros
     deshabilitados (Playnite/App/PlayniteApplication.cs lineas 131-146). Vale
     la pena marcarlo en "sorprendentes" â€” suena relacionado a
     playtime/sesiones por el nombre pero no lo esta.

--------------------------------------------------------------------------------
28.11 VERIFICACION DE ESTADO DE INSTALACION

Game.IsInstalled es un BOOL GUARDADO QUE SE CONFIA CIEGAMENTE, no se re-verifica
al cargar. Evidencia:
  - La logica de merge de GameDatabase.ImportGames (GameDatabase.cs lineas
    1281-1288): if (!existingGame.IsCustomGame &&
    !existingGame.OverrideInstallState) { if (existingGame.IsInstalled !=
    newGame.IsInstalled) { existingGame.IsInstalled = newGame.IsInstalled;
    ... } } â€” o sea, IsInstalled solo se sobreescribe durante un RE-ESCANEO DE
    BIBLIOTECA, y solo con lo que el scanner del plugin de biblioteca reporte
    como newGame.IsInstalled (el plugin mismo es responsable de chequear el
    estado de instalacion, ej. el plugin de Steam enumerando appmanifests
    instalados) â€” el nucleo de Playnite NUNCA hace
    Directory.Exists(game.InstallDirectory) el mismo para cambiar este flag. Un
    booleano Game.OverrideInstallState deja que el usuario "fije" el valor para
    que sobreviva re-escaneos sin tocarse.
  - No se encontro ningun chequeo de existencia de archivo/directorio que
    controle IsInstalled en GamesEditor.cs, GameDatabase.cs, ni
    GameExtensions.cs fuera de los resultados de InstallController/plugin de
    biblioteca provistos por plugins.

CheckSetupCommand/CheckExecutionCommand (Playnite.DesktopApp/ViewModels/
GameDetailsViewModel.cs lineas 306-307, 349-350, 514-534) â€” estos comandos NO
"chequean" ni "verifican" nada a pesar del nombre. Son literalmente identicos:
ambos muestran un dialogo de confirmacion Si/No ("Â¿Cancelar el monitoreo de
instalacion/ejecucion?") y, si es Si, llaman editor.CancelGameMonitoring
(game.Game) â€” que fuerza la parada del controlador de install/uninstall/play y
resetea los flags IsInstalling/IsUninstalling/IsRunning/IsLaunching (ver
CancelGameMonitoring en S28.10). ContextActionCommand (lineas 352-370) redirige
a CheckSetup() si IsInstalling/IsUninstalling, a CheckExecution() si
IsRunning/IsLaunching, si no a Install()/Play(). No existe en ningun lado del
codigo bajo estos comandos una funcionalidad de "verificar archivos / re-chequear
instalacion" â€” hallazgo sorprendente respecto de lo que sugiere el nombre.

--------------------------------------------------------------------------------
28.12 PlayniteSettings â€” MODELO DE CONFIGURACION GLOBAL

Archivo: Playnite/Settings/PlayniteSettings.cs (2775 lineas), clase
PlayniteSettings : ObservableObject (linea 261).

PERSISTENCIA â€” Archivos JSON planos, NO LiteDB, via Newtonsoft.Json
(JsonConvert.SerializeObject/DeserializeObject), a traves de helpers privados
LoadSettingFile<T>(string path) / SaveSettingFile(object settings, string path)
(lineas 2303-2323).
Tres archivos JSON separados (rutas en Playnite/Settings/PlaynitePaths.cs):
  - config.json (ConfigFileName) â€” el objeto PlayniteSettings principal.
  - fullscreenConfig.json (FullscreenConfigFileName) â€” un objeto
    FullscreenSettings separado, se carga/guarda independiente y solo se
    cuelga de settings.Fullscreen.
  - windowPositions.json (WindowPositionsFileName) â€” objeto WindowPositions
    separado.
  Cada uno tiene un espejo en carpeta Backup\ usado como fallback si el
  archivo principal falla al cargar/parsear (LoadSettings(), lineas 2352-2364,
  y LoadExternalConfig<T>, lineas 2500-2519).
PlayniteSettings.LoadSettings() (estatico, lineas 2352-2498) tiene una ESCALERA
DE MIGRACION DE VERSION manual: un campo int settings.Version se chequea e
incrementa (Version == 1 -> 2 -> ... -> 7) con codigo de upgrade ad-hoc por
paso (ej. resetear BackgroundImageBlurAmount, sembrar nuevos
MetadataFieldSettings, remapear ids de plugin de nombres pre-addon-store a IDs
*_Builtin, migrar campos planos viejos como UpdateLibStartup/
ForcePlayTimeSync desde un Dictionary<string,object> crudo del mismo archivo).
SaveSettings() (linea 2521+) escribe los tres archivos juntos y tambien llama
FileSystem.CreateDirectory(PlaynitePaths.ConfigRootPath) primero.
Divergencia importante respecto del patron de datos de juegos:
CompletionStatusSettings (ver abajo) NO se guarda en config.json â€” se guarda
como UNA SOLA FILA en una coleccion LiteDB DENTRO de la base de datos de juegos
misma (Playnite/Database/Collections/CompletionStatusesCollection.cs lineas
12-61), via GetSettings()/SetSettings() sobre
CompletionStatusesCollection : ItemCollection<CompletionStatus>, usando un
patron singleton de fila fija ([BsonId(false)] int Id = 0)
(SettingsCollection.Insert(...) si esta vacio, si no
FindAll().First(); SetSettings hace settings.Id = 0;
SettingsCollection.Upsert(settings)).

FORMA / GRUPOS DE SETTINGS ANIDADOS: PlayniteSettings es una clase grande y
plana pero compone objetos de grupo de settings anidados como propiedades
(confirmado por grep directo del archivo):
  DetailsVisibilitySettings DetailsVisibility (linea 271)
  FilterSettings FilterSettings (linea 914) â€” el estado de filtro ACTIVO/
    ultimo-usado (distinto del modelo FilterPresetSettings del SDK usado para
    presets guardados)
  ViewSettings ViewSettings (linea 928) â€” entre otras cosas,
    ListViewColumsOrder (List<GameField>) y ListViewColumns (un
    ListViewColumnsProperties con flags Visible por campo)
  MetadataDownloaderSettings MetadataSettings (linea 1448) â€” objetos
    MetadataFieldSettings(bool enabled, List<Guid> sourcePriority) por campo
    (Feature, AgeRating, Series, Platform, Region, etc.), conecta con S28.3
  AutoClientShutdownSettings ClientAutoShutdown (linea 1551) â€” ShutdownClients
    (bool), MinimalSessionTime, GraceTimeout, ShutdownPlugins (set de ids de
    plugin) â€” usado en la logica de auto-apagado de cliente tras cerrar el
    juego en Controllers_Stopped
  WindowPositions WindowPositions (linea 2248) â€” archivo separado
  FullscreenSettings Fullscreen (linea 2254) â€” archivo separado; incluye al
    menos IsMusicMuted, MinimizeAfterGameStartup (usado en Controllers_Started)
  SearchWindowVisibilitySettings SearchWindowVisibility (linea 2275)
  GameSearchFilterSettings GameSearchFilterSettings (linea 2178), emparejado
    con un bool plano SaveGlobalSearchFilterSettings (linea 2171)

Otros campos planos de nivel superior observados en uso en esta pasada (no
exhaustivo): PreScript, PostScript, GameStartedScript (texto de script global),
AfterLaunch (AfterLaunchOptions: Close/Minimize/...), AfterGameClose
(AfterGameCloseOptions: Restore/RestoreOnlyFromUI/Exit/...),
DiscordPresenceEnabled, QuickLaunchItems (cantidad, int),
ShowHiddenInQuickLaunch, InstallSizeScanUseSizeOnDisk,
CheckForLibraryUpdates/CheckForEmulatedLibraryUpdates
(LibraryUpdateCheckFrequency), PlaytimeImportMode, DisabledPlugins
(List<string>).

CompletionStatusSettings â€” campos exactos (CompletionStatusesCollection.cs
lineas 12-18):
  [BsonId(false)] int Id = 0
  Guid DefaultStatus
  Guid PlayedStatus
Confirmado como el unico consumidor destino de
Database.GetCompletionStatusSettings() usado en GamesEditor.UpdateGameState
(S28.10): DefaultStatus es el estado que reciben los juegos nuevos; PlayedStatus
se asigna automaticamente la primera vez que un juego pasa a IsRunning == true,
salvo que PlayedStatus == Constants.MaxGuidVal (sentinel documentado de "no
hacer nada") o que el estado actual del juego no sea Guid.Empty/DefaultStatus
(o sea, el usuario ya eligio a mano otro estado, se respeta y no se
sobreescribe).

--------------------------------------------------------------------------------
28.13 ALGORITMO REAL DE APLICACION DE FILTROS

Archivo: Playnite/Database/GameDatabase_Filters.cs (archivo completo, lineas
1-372). Clase FilterMatcher (interna, lineas 49-371), construida desde un
FilterSettings (el modelo interno de filtro, convertido desde el
FilterPresetSettings del SDK via FilterSettings.FromSdkFilterSettings(...)) mas
un bool useFuzzyNameMatch.

GameDatabase.GetGameMatchesFilter(...)/GetFilteredGames(...) (lineas 11-46) son
los puntos de entrada publicos; GetFilteredGames es un generador yield return
sobre Games aplicando FilterMatcher.Match(game) de forma perezosa (usado para
alimentar el predicado Filter de un CollectionView).

Match(Game game) (lineas 60-147) es una cadena plana de ~26 chequeos
if (!MatchX(game)) return false; (estado de instalacion, favorito, oculto,
biblioteca, nombre, aÃ±o de lanzamiento, bucket de playtime, bucket de tamaÃ±o de
instalacion, version, estado de completado, bucket de ultima actividad, bucket
de actividad reciente, bucket de fecha agregada, bucket de fecha modificada,
buckets de puntaje usuario/comunidad/critica, series, regiones, fuente, age
ratings, generos, plataformas, publishers, developers, categorias, tags,
features) â€” TODOS los chequeos a nivel de campo se combinan con AND (un juego
debe pasar TODOS los filtros activos para incluirse). Esto es AND incondicional
ENTRE campos distintos, sin importar UseAndFilteringStyle.

UseAndFilteringStyle solo gobierna el matching DENTRO de un mismo campo
multi-valor, adentro de IsFilterMatchingList (lineas 310-362), que respalda
Genre/Platform/Publisher/Developer/Category/Tag/Feature/Series/Region/AgeRating:
  - Si el filtro tiene Texts libre seteado:
    filterSettings.UseAndFilteringStyle ?
    filter.Texts.All(gameHasItemWithStringMatch) :
    filter.Texts.Any(gameHasItemWithStringMatch).
  - Si el filtro tiene Ids seteado:
    filterSettings.UseAndFilteringStyle ?
    filter.Ids.All(gameHasItemWithIdMatch) :
    filter.Ids.Any(gameHasItemWithIdMatch) â€” O SEA: una sola lista
    IdItemFilterItemProperties.Ids con varios ids seleccionados es ALL-of
    cuando UseAndFilteringStyle == true, ANY-of (OR) si no (el default es
    OR/Any). Caso especial adentro de gameHasItemWithIdMatch: filterId ==
    Guid.Empty matchea juegos que NO tienen ningun valor para esa propiedad
    (gamePropertyIds == null || gamePropertyIds.Count == 0) â€” asi aparece
    "(ninguno)" como pseudo-valor filtrable para campos multi-valor.

Campos de valor unico (Library, CompletionStatuses, Source) van por
IsFilterMatchingSingle (lineas 278-301) en vez de eso â€” siempre semantica
OR/ANY contra filter.Ids/filter.Texts (sin variante AND, porque un juego solo
puede tener un valor para estos campos, asi que "AND entre varios ids
seleccionados" seria vacuamente falso salvo el caso de no-match â€” el codigo ni
siquiera ramifica sobre UseAndFilteringStyle aca).

Campos con bucket de enum (PlayTime, InstallSize, LastActivity, RecentActivity,
Added, Modified) van por MatchEnumField (lineas 263-269):
enumFilter.Values.Contains(enumFieldValue) â€” chequeo OR plano de "el bucket
calculado del juego esta en el set seleccionado", calculado desde propiedades
derivadas de Game como PlaytimeCategory, InstallSizeGroup,
LastActivitySegment, RecentActivitySegment, AddedSegment, ModifiedSegment (son
propiedades enum calculadas/agrupadas en Game, NO campos guardados crudos â€”
vale la pena anotarlo para la reescritura como valores derivados, no
persistidos).

Campos de puntaje (UserScore, CommunityScore, CriticScore) usan
IsScoreFilterMatching (lineas 364-370), mismo patron Contains((int)score)
contra un enum ScoreGroup precalculado.

Matching de nombre (MatchName, lineas 175-198) tiene sintaxis especial: un ^
al inicio mas un caracter agrupa juegos alfabeticamente (game.GetNameGroup() ==
filterSettings.Name[1]); un ! al inicio (o useFuzzyNameMatch == false) hace un
IndexOf de substring ordinal plano (saltando el primer caracter del string de
filtro, aparentemente un byte marcador); si no usa
SearchViewModel.MatchTextFilter(...) para matching difuso (fuzzy).

--------------------------------------------------------------------------------
28.14 EDICION MULTIPLE (GameTools) â€” ALGORITMO REAL

Archivo: Playnite/GameTools.cs (archivo completo, lineas 1-240).
MultiEditGame : Game (lineas 12-24) agrega diez propiedades de lista
DistinctXIds (DistinctGenreIds, DistinctDeveloperIds, DistinctPublisherIds,
DistinctCategoryIds, DistinctTagIds, DistinctFeatureIds, DistinctPlatformIds,
DistinctRegionIds, DistinctAgeRatingIds, DistinctSeriesIds) ademas de las
comunes heredadas.

GameTools.GetMultiGameEditObject(IEnumerable<Game> games) (lineas 28-237) â€” el
algoritmo completo:
  1. Si no hay juegos, devuelve un MultiEditGame vacio.
  2. firstGame = games.First().
  3. CAMPOS ESCALARES (Name, SortingName, ReleaseDate, Description, Notes,
     Manual, LastActivity, Playtime, Added, PlayCount, InstallSize, Version,
     SourceId, CompletionStatusId, UserScore, CriticScore, CommunityScore,
     Hidden, IsInstalled, InstallDirectory, Favorite, PreScript, PostScript,
     GameStartedScript, UseGlobalPreScript, UseGlobalPostScript,
     UseGlobalGameStartedScript, IncludeLibraryPluginAction): para cada uno,
     toma el valor de firstGame y solo lo asigna en dummyGame SI
     games.All(a => a.Campo == valorPrimero) â€” o sea, la propiedad del objeto
     de edicion solo se puebla (no-default/no-null) cuando TODOS los juegos
     seleccionados comparten exactamente el mismo valor; si no, queda en su
     default de tipo, que la UI de edicion presumiblemente interpreta como
     "mixto/en blanco" (comun == mismo valor, distinto == en blanco).
  4. CAMPOS DE LISTA DE IDS MULTI-VALOR (Genre, Developer, Publisher, Category,
     Tag, Feature, Platform, Series, Region, AgeRating): se calculan via
     ListExtensions.GetCommonItems(games.Select(a => a.XIds)) para el set
     "comun a todos" (asignado a la propiedad XIds real, ej.
     dummyGame.GenreIds) Y ListExtensions.GetDistinctItems(...) para el set
     union (asignado a la propiedad DistinctXIds correspondiente en
     MultiEditGame). Esto significa que la UI de edicion multiple puede
     mostrar tanto "marcado para todos" (comun) como "el set completo de
     valores presentes en la seleccion" (distinct/union) â€” patron de UI de
     checkbox tri-estado (marcado/desmarcado/indeterminado).
  5. Devuelve el dummyGame poblado.

Nota: GameTools.cs no contiene la mitad de "aplicar de vuelta a todos los
juegos seleccionados" â€” esa logica (presumiblemente diffear el MultiEditGame
editado contra cada Game original y escribir solo los campos cambiados/
tocados explicitamente) vive en el path de guardado de GameEditViewModel
(Playnite.DesktopApp/ViewModels/GameEditViewModel.cs, referenciado desde
DesktopGamesEditor.EditGames) que NO se abrio en esta pasada â€” marcar como
hueco pendiente si se necesita especificamente el algoritmo de escritura-de-
vuelta; la mitad de lectura/diff (GetMultiGameEditObject) esta completamente
capturada arriba.

--------------------------------------------------------------------------------
28.15 SISTEMA DE NOTIFICACIONES / TAREAS EN BACKGROUND

INotificationsAPI (PlayniteSDK/INotificationsAPI.cs) implementado por
Playnite.API.NotificationsAPI : ObservableObject, INotificationsAPI
(Playnite/API/NotificationsAPI.cs, archivo completo, lineas 1-112):
  - Mantiene ObservableCollection<NotificationMessage> Messages y un
    int Count calculado.
  - Add(NotificationMessage) / Add(string id, string text, NotificationType
    type) / Remove(string id) / RemoveAll() â€” las cuatro se despachan sobre el
    SynchronizationContext capturado via context.Send(...) para poder llamarse
    siempre desde un hilo de background y aun asi mutar la
    ObservableCollection bindeada a UI en el hilo de UI.
  - Dedupea por Id (guard Messages.Any(a => a.Id == message.Id)).
  - Conecta los eventos Activated/Closed de cada NotificationMessage agregado
    para que burbujeen como ActivationRequested/CloseRequested en el objeto
    API mismo (asi un click en un toast se puede manejar centralizado, ej.
    para abrir una vista relevante).
  - Es una LISTA DE MENSAJES PASIVA, no un sistema de progreso/porcentaje â€” es
    para notificaciones tipo toast ("Actualizacion disponible", "Fallo la
    instalacion del juego X", etc.), no para progreso de tareas largas.

Aparte, el PROGRESO DE TAREAS LARGAS se expone como
IDialogsFactory.ActivateGlobalProgress(...) (referenciado en
GamesEditor.UpdateGameSizeWithDialog, GamesEditor.cs lineas 691-699, que llama
Dialogs.ActivateGlobalProgress((a) => { UpdateGameSize(...); }, ...)); es un
patron de barra de progreso modal con callback â€” se le pasa un delegate un
objeto de contexto de reporte-de-progreso/cancelacion y la factory de dialogos
muestra una UI de progreso bloqueante mientras el delegate corre en un hilo de
background, patron comun en todo el codebase para escaneos de tamaÃ±o, imports,
y descargas de metadata (consistente con lo que ya toco la primera pasada
sobre MetadataDownloader). Las definiciones completas de tipo de
IDialogsFactory/GlobalProgressOptions viven en PlayniteSDK/IDialogsFactory.cs â€”
no se expandieron del todo en esta pasada porque la tarea solo pedia "lo
suficiente para conocer el patron."

--------------------------------------------------------------------------------
28.16 HALLAZGOS SORPRENDENTES â€” SEGUNDA PASADA

1. El tracking de playtime es 100% por POLLING, nunca por evento. A pesar de
   que System.Diagnostics.Process expone un evento Exited (con
   EnableRaisingEvents), Playnite nunca lo usa. Cada modo de tracking â€” incluso
   OriginalProcess, que trackea un unico PID conocido â€” corre con un loop
   Task.Run haciendo polling cada TrackingFrequency ms (default 2000)
   chequeando !process.HasExited. Esto significa que la latencia de deteccion
   de parada tiene un piso igual al intervalo de polling, no es instantanea.
2. CheckSetupCommand/CheckExecutionCommand NO "chequean" ni "verifican" nada â€”
   son literalmente un dialogo de confirmacion de "cancelar monitoreo", ambos
   llamando el mismo CancelGameMonitoring. El nombre es enganoso respecto de
   lo que uno adivinaria desde GameDetailsViewModel.
3. IsInstalled NUNCA se re-verifica activamente por el nucleo de Playnite (no
   hay ningun chequeo Directory.Exists en ningun lado que controle este flag)
   â€” se setea una vez en un flujo manual de "agregar juego", se cambia por un
   evento de completado de InstallController/UninstallController, o se
   sobreescribe entero por lo que reporte el scanner de un plugin de
   biblioteca durante ImportGames, salvo que el usuario haya seteado
   Game.OverrideInstallState para fijarlo.
4. COEXISTEN DOS CAMINOS INDEPENDIENTES de acumulacion de playtime: el
   acumulador stopwatch principal playTimeMs adentro de
   GenericPlayController.StartTracking (usado para el inicio/parada normal), y
   un diccionario separado de timestamps DateTime gameStartups en GamesEditor
   usado solo como respaldo para el flujo de "cancelar monitoreo" disparado
   por el usuario (CancelGameMonitoring). Pueden divergir (ej. la logica de
   descarte de tick por suspension/hibernacion de 30 segundos solo aplica al
   primero).
5. En CADA arranque de la app, el cargador de LiteDB pone en cero
   IsRunning/IsLaunching/IsInstalling/IsUninstalling incondicionalmente para
   TODOS los juegos (transformacion del constructor de GamesCollection) â€”
   esta es TODA la historia de "recuperacion ante crash"; no hay ningun
   intento de detectar si el proceso previamente trackeado sigue corriendo de
   verdad, y ningun intento de recuperar/estimar el playtime perdido de una
   sesion interrumpida â€” simplemente se descarta en silencio.
6. safestart.flag suena relacionado a playtime/sesiones por el nombre pero no
   lo esta â€” es el protector anti-crash-loop del propio Playnite para activar
   "modo seguro" (deshabilitar extensiones/temas de terceros), sin relacion
   con procesos de juegos.
7. Las apps UWP (Windows Store) reciben manejo a medida: lanzar via
   explorer.exe shell:AppsFolder\... se detecta por regex sobre los
   argumentos, y Playnite deliberadamente NO trackea el proceso explorer.exe
   que el mismo lanzo â€” busca el WorkDir real de la app via
   Programs.GetUWPApps() y cambia a tracking por directorio en su lugar.
8. NO HAY mecanismo generico integrado de install/uninstall â€” GenericPlayController
   no tiene contraparte para install/uninstall; eso es 100% delegado a objetos
   InstallController/UninstallController provistos por plugins. Un juego
   agregado manualmente, o un juego de biblioteca cuyo plugin no implementa
   GetInstallActions, simplemente no tiene ningun camino de instalacion a
   traves de la UI de Playnite.
9. CompletionStatusSettings se guarda en la base de datos LiteDB de juegos (como
   fila singleton, Id = 0), NO en el archivo config.json de PlayniteSettings
   donde vive la mayoria de los demas grupos de settings â€” una ubicacion de
   persistencia inconsistente, vale la pena decidir deliberadamente (un solo
   almacen vs dos) para la reescritura de Bridge.
10. PlayCount se incrementa AL INICIAR el juego, no al pararlo (adentro de
    UpdateGameState cuando running == true), y la auto-transicion de estado de
    completado a "Jugado" tambien pasa al iniciar, no despues de ningun tiempo
    minimo de sesion â€” un lanzamiento accidental y cierre inmediato igual
    cuenta como "jugado" e incrementa PlayCount.

--------------------------------------------------------------------------------
28.17 QUE SIGUE â€” ESTO ES REFERENCIA, NO DISEÃ‘O DE BRIDGE

Esta seccion (28.1 a 28.16) documenta que hace Playnite HOY, verificado contra
su codigo real, en dos pasadas de investigacion. No propone todavia las clases
concretas de Bridge.Core, ni el esquema de storage de Bridge, ni decide SQLite
vs LiteDB para Bridge (aunque el hallazgo de que Playnite mismo usa LiteDB v4 en
produccion para este dominio exacto es evidencia a favor a considerar en el
ADR-4 de ARCHITECTURE.md). Tampoco decide todavia, para Bridge, si el tracking
de playtime sera por polling (como Playnite) o si conviene explorar
Process.Exited nativo ahora que .NET 10 esta disponible (Playnite es .NET
Framework 4.8), ni si Bridge tendra su propio InstallController generico local
en vez de depender de que cada fuente lo provea (Playnite no lo tiene â€” es un
hueco funcional real del original que Bridge podria decidir cerrar, si quiere
mejorar en vez de solo replicar).

Adaptar todo esto a las entidades y flujos reales de Bridge.Core (simplificando
lo que ya no aplica sin sistema de plugins, mientras se preserva lo que si
importa â€” el patron GameId+fuente para dedupe, el orden de scripts, el patron
de tracking por polling, la semantica de filtros AND/OR) es un paso de diseÃ±o
aparte, todavia no hecho.

--------------------------------------------------------------------------------
28.18 BOOTSTRAP REAL DE LA APLICACION (Desktop)

ProgramEntry.Main â€” Playnite.DesktopApp/ProgramEntry.cs lineas 18-91, en orden:
  1. Parsea argumentos de linea de comandos via CommandLine.Parser a
     CmdLineOptions.
  2. Si cmdLine.UserDataDir esta seteado: recorta barras/comillas finales,
     FileSystem.CreateDirectory, PlaynitePaths.UpdateUserDataDir(...); si falla
     muestra un MessageBox y retorna (sin dialogo de crash, solo sale).
  3. FileSystem.CreateDirectory(PlaynitePaths.JitProfilesPath), despues
     ProfileOptimization.SetProfileRoot(...) + ProfileOptimization.
     StartProfile("desktop") â€” mecanismo nativo de .NET de perfil de arranque
     JIT, no algo custom.
  4. Rechaza Windows 7/8 (Computer.WindowsVersion) con MessageBox y retorna.
  5. Rechaza correr desde una ruta de extraccion RAR temporal (substring
     temp\rar$) o una ruta que contenga # (ambos son guards reales motivados
     por tickets de soporte).
  6. Splash screen: cuenta otros procesos de Playnite via
     Process.GetProcesses().Where(IsProcessPlayniteProcess); solo muestra
     SplashScreen("SplashScreen.png") si cmdLine.Start esta vacio,
     HideSplashScreen no esta seteado, y este es el unico proceso.
  7. PlayniteSettings.ConfigureLogger() (setup de NLog) y loguea los argv.
  8. Construye new DesktopApplication(() => new App(), splash, cmdLine) y
     llama app.Run(). () => new App() es una factory diferida de Application
     de WPF, todavia no invocada.

NO HAY CONTENEDOR DE DI EN NINGUN LADO. Confirmado por lectura completa de
PlayniteApplication y DesktopApplication â€” todo es composicion manual con new.
No hay Microsoft.Extensions.DependencyInjection, ni abstraccion de service
locator, ni registro basado en interfaces via contenedor. Los constructores
reciben dependencias concretas directamente.

El composition root real esta partido en dos lugares:
  - El constructor protegido de PlayniteApplication (Playnite/App/
    PlayniteApplication.cs lineas 92-442) â€” corre ANTES de que exista ningun
    recurso de WPF. Orden: workaround TLS1.2 -> guarda CmdLine/Mode/Current ->
    hookea AppDomain.UnhandledException/AssemblyResolve -> CHEQUEO DE INSTANCIA
    UNICA (CheckOtherInstances()) -> chequeo/dialogo de flag de arranque seguro
    -> CurrentNative = appInitializer() (aca es donde se construye de verdad
    la Application de WPF) -> appMutex = new Mutex(true, instanceMuxet) ->
    arranca PipeService/PipeServer (con reintentos Polly) ->
    PlayniteSettings.MigrateSettingsConfig() + AppSettings =
    PlayniteSettings.LoadSettings() -> chequeo de auto-backup al arrancar
    (escribe un BackupActionFile y setea CmdLine.Backup, causando una rama de
    reinicio-hacia-modo-backup) -> logica de seleccion de tema default/custom
    -> InitializeNative() (abstracto, llama App.InitializeComponent()) ->
    aplicacion de fuente/localizacion -> Notifications = new
    NotificationsAPI(), UriHandler = new PlayniteUriHandler().
  - DesktopApplication.InstantiateApp() (Playnite.DesktopApp/
    DesktopApplication.cs lineas 145-172) â€” llamado desde
    DesktopApplication.Startup() (linea 89) DESPUES de que ConfigureApplication()
    tiene exito. Este es el composition root literal de los servicios core:
      Database = new GameDatabase();
      Database.SetAsSingletonInstance();
      Controllers = new GameControllerFactory(Database);
      Extensions = new ExtensionFactory(Database, Controllers, GetApiInstance);
      GamesEditor = new DesktopGamesEditor(Database, Controllers, AppSettings,
        Dialogs, Extensions, this, new DesktopActionSelector());
      Game.DatabaseReference = Database;
      ImageSourceManager.SetDatabase(Database);
      MainModel = new DesktopAppViewModel(Database, new MainWindowFactory(),
        Dialogs, new ResourceProvider(), AppSettings,
        (DesktopGamesEditor)GamesEditor, Extensions, this);
      PlayniteApiGlobal = GetApiInstance();
      SDK.API.Instance = PlayniteApiGlobal;
    Todo es una llamada de constructor plana pasando objetos ya construidos
    hacia abajo â€” inyeccion por constructor de manual de libro de texto, sin
    contenedor basado en reflexion.

  DesktopApplication.Startup() (lineas 82-106), secuencia: ConfigureApplication()
  -> InstantiateApp() -> setea AppUriHandler -> ProcessStartupWizard() (devuelve
  isFirstStart) -> MigrateDatabase() -> condicionalmente InitSDL()/SetupInputs()
  para soporte de control -> OpenMainViewAsync(isFirstStart) (fire-and-forget
  async) -> LoadTrayIcon() -> StartUpdateCheckerAsync() (fire-and-forget) ->
  ProcessArguments() -> cierra el splash screen.

  OpenMainViewAsync (lineas 196-236): carga plugins/scripts
  (Extensions.LoadPlugins/LoadScripts, se saltea si es solo-plugins en primer
  arranque), dispara OnExtensionsLoaded(), arma la lista de herramientas de
  terceros, abre la ventana principal, despues o hace el primer UpdateLibrary+
  DownloadMetadata (primer arranque) o ProcessStartupLibUpdate(), finalmente
  borra el archivo de flag de arranque seguro (este es el marcador real de
  "sobrevivimos al arranque").

  ConfigureApplication() (PlayniteApplication.cs lineas 1010-1087): setea
  HtmlRendererSettings, opcionalmente fuerza renderizado por software,
  opcionalmente limpia el cache web de CEF, CefTools.ConfigureCef(...) (falla
  duro â€” muestra error y Quit() si CefSharp no inicializo), ExtensionFactory.
  CreatePluginFolders(), registra arranque-con-el-sistema / protocolo URI /
  extensiones de archivo via SystemIntegration.

INSTANCIA UNICA â€” real y en dos capas:
  1. MUTEX con nombre "PlayniteInstaceMutex" (const instanceMuxet,
     PlayniteApplication.cs lineas 38-39, 155). CheckOtherInstances() (lineas
     925-1008) intenta Mutex.TryOpenExisting(instanceMuxet, ...); si una
     instancia ya lo tiene, el proceso nuevo NO crea su propia ventana â€” se
     vuelve un cliente.
  2. IPC POR PIPE CON NOMBRE (net.pipe://localhost/PlaynitePipe, configurado en
     Playnite/Common.config linea 9 como app-setting PipeEndpoint) via
     PipeServer/PipeService/PipeClient. La instancia ya corriendo hostea un
     PipeServer; una segunda instancia recien lanzada usa PipeClient para
     mandarle un CmdlineCommand (Focus, Start, UriRequest, ExtensionInstall,
     SwitchMode, Shutdown, BackupData, RestoreBackup), y despues el proceso
     nuevo llama Environment.Exit(0). Si ningun proceso tiene el mutex pero
     siguen vivos varios procesos crudos (race al arrancar), gana el de PID
     MAS ALTO y los demas se auto-terminan. CmdLineOptions.MasterInstance
     saltea este chequeo por completo (usado por Restart()).

--------------------------------------------------------------------------------
28.19 SISTEMA DE TEMAS

Manifiesto: theme.yaml (PlaynitePaths.ThemeManifestFileName), deserializado de
YAML via Playnite.Manifests/ThemeManifest.cs lineas 32-57.

ThemeManifest (lineas 13-64) extiende BaseExtensionManifest (Id, Name, Author,
Version, Links, mas DirectoryPath/DirectoryName/DescriptionPath no
serializados). ThemeManifest agrega: ThemeApiVersion (string), Mode
(enum ApplicationMode â€” Desktop/Fullscreen, LOS TEMAS SON ESPECIFICOS DE MODO,
NUNCA COMPARTIDOS), mas IsBuiltInTheme/IsCustomTheme/IsCompatible calculados.

Layout en disco: <Themes>/<Desktop|Fullscreen>/<NombreCarpetaTema>/theme.yaml
mas archivos .xaml de ResourceDictionary arbitrarios y carpetas de assets (ej.
Images/ButtonPrompts/<NombrePrompt>/<NombrePrompt>.xaml, cursor.cur/cursor.ani).
Se escanean y mergean dos raices (ThemeManager.GetAvailableThemes
(ApplicationMode)): PlaynitePaths.ThemesUserDataPath (instalados por el
usuario) tiene prioridad, despues PlaynitePaths.ThemesProgramPath
(integrados), dedupeado por Id.

Chequeo de compatibilidad de version de API â€” dos copias independientes de
esencialmente la misma logica:
  - Al cargar el manifiesto, en el constructor de ThemeManifest: IsCompatible
    = true solo si themeVersion.Major == apiVersion.Major && themeVersion <=
    apiVersion.
  - Al aplicar, en ThemeManager.ApplyTheme: rechaza con
    AddonLoadError.SDKVersion si themeVersion.Major != apiVersion.Major ||
    themeVersion > apiVersion.
  - Las versiones de API actuales son constantes hardcodeadas:
    DesktopApiVersion = "2.9.0", FullscreenApiVersion = "2.9.0". Misma regla
    que las extensiones: el major debe matchear, la version del tema no puede
    superar la de la app.

MECANISMO DE SUSTITUCION DE ResourceDictionary (ThemeManager.ApplyTheme) â€” la
parte interesante/sorprendente:
  1. Valida que theme.Id este presente y que la version de API sea compatible.
  2. Enumera app.Resources.MergedDictionaries, encuentra cada diccionario cuyo
     Source empieza con la ruta raiz del TEMA DEFAULT (Themes/<Mode>/Default/)
     â€” estos son los nombres de archivo xaml "aceptables" que un tema custom
     tiene PERMITIDO sobreescribir.
  3. Pre-carga cada uno de esos archivos desde la carpeta del tema custom (si
     esta presente) solo para atrapar errores de parseo antes de mutar
     recursos en vivo â€” aborta con AddonLoadError.Uknown si falla.
  4. Aplica cursor custom si existe cursor.cur/cursor.ani en el tema.
  5. QUITA CADA DICCIONARIO DEL TEMA DEFAULT ACTUALMENTE CARGADO de
     app.Resources.MergedDictionaries.
  6. LOS VUELVE A AGREGAR EN UN ORDEN INTERCALADO ESPECIFICO: por cada archivo
     xaml aceptable, recarga y reagrega primero la version DEFAULT, y despues
     â€” si el tema provee un override para esa misma ruta relativa â€” carga y
     agrega la version del TEMA justo despues. El comentario del codigo
     explica que es deliberado: las referencias StaticResource solo resuelven
     contra diccionarios ya presentes en el pool mergeado en el orden de carga
     al momento del lookup, asi que un "simplemente agregar los diccionarios
     del tema al final" ingenuo rompe referencias estaticas cruzadas entre
     archivos (issue #2328 referenciado en el codigo).
  7. Localization.LoadAddonLocalization(theme.DirectoryPath) para agregar las
     strings de traduccion del tema.

  Se llama desde el constructor de PlayniteApplication, explicitamente DESPUES
  de InitializeNative() (los recursos default de App.xaml ya inicializados),
  pero la RESOLUCION del manifiesto del tema pasa ANTES de InitializeNative()
  (comentario en el codigo: "el tema debe fijarse ANTES de que los recursos
  default de la app se inicialicen para que el markup ThemeFile aplique las
  rutas del tema custom").

  ThemeFile (Playnite/Extensions/Markup/ThemeFile.cs) es la markup extension
  del lado XAML que usan los archivos de tema individuales para referenciar
  recursos (ej. imagenes) â€” {markup:ThemeFile Images\AlgunIcono.png} â€” resuelve
  contra CurrentTheme.DirectoryPath primero, cayendo a
  DefaultTheme.DirectoryPath si no esta, asi que los assets no tematizados
  caen en silencio a la copia del tema default.

--------------------------------------------------------------------------------
28.20 CONTRATO REAL DE LibraryPlugin

PlayniteSDK/Plugins/LibraryPlugin.cs:

  class LibraryGetGamesArgs { CancellationToken CancelToken; }   // unico miembro
  class LibraryImportGamesArgs { CancellationToken CancelToken; }   // unico miembro

  class LibraryPluginProperties : PluginProperties {   // base: bool HasSettings
    bool CanShutdownClient = false;
    bool HasCustomizedGameImport = false;
  }

  abstract class LibraryPlugin : Plugin {
    LibraryPluginProperties Properties
    abstract string Name
    virtual string LibraryIcon
    virtual string LibraryBackground
    virtual LibraryClient Client

    virtual IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args) => []
    virtual IEnumerable<Game> ImportGames(LibraryImportGamesArgs args) => []
    virtual LibraryMetadataProvider GetMetadataDownloader() => null
    override string ToString() => Name
  }

O SEA: NO hay miembros estrictamente obligatorios de verdad. GetGames/
ImportGames/GetMetadataDownloader tienen implementaciones default (vacias); lo
unico realmente obligatorio es Name (y Id, heredado abstracto de Plugin).
LibraryGetGamesArgs lleva SOLO un cancellation token â€” sin callback de
progreso; el reporte de progreso de una actualizacion de biblioteca lo maneja
el CALLER envolviendo toda la llamada a GetGames() en un dialogo de progreso
global, no el plugin empujando eventos de progreso.

Rama de HasCustomizedGameImport â€” consumida en
GameDatabase.ImportGames(LibraryPlugin, CancellationToken,
PlaytimeImportMode) (ya documentado en S28.2):
  if (library.Properties?.HasCustomizedGameImport == true) {
    // el plugin llama library.ImportGames(...) y devuelve Game ya terminados
    // que EL MISMO ya persistio/preparo â€” controla toda la logica de
    // creacion/dedup/escritura en la DB
  } else {
    // camino estandar: GameDatabase.ImportGames itera library.GetGames(...),
    // hace su propio chequeo de ImportExclusions, busca existingGame por
    // (GameId, PluginId), llama ImportGame(newGame, library.Id) interno para
    // crear el Game desde GameMetadata, aplica reglas de playtime-import-mode,
    // actualiza estado de completado
  }
O SEA: HasCustomizedGameImport = true significa "el plugin mismo llama a las
APIs de base de datos/import y devuelve objetos Game terminados"; false
(default) significa "el plugin solo entrega GameMetadata crudo y el
GameDatabase.ImportGames propio de Playnite hace el trabajo de dedup/creacion."

LibraryMetadataProvider (PlayniteSDK/MetadataProvider.cs lineas 13-29):
  abstract class LibraryMetadataProvider : IDisposable {
    virtual void Dispose() {}
    abstract GameMetadata GetMetadata(Game game);   // unico miembro obligatorio
  }
Contrato de un solo metodo, matchea exacto con el patron de consumo
ProcessStoreDownload de S28.3 â€” LibraryPlugin.GetMetadataDownloader() devuelve
uno de estos (o null), y los callers hacen provider.GetMetadata(game) y
despues lo disponen.

--------------------------------------------------------------------------------
28.21 EDICION MULTIPLE â€” MITAD DE ESCRITURA (GameEditViewModel)

Playnite.DesktopApp/ViewModels/GameEditViewModel.cs (principal + ConfirmDialog),
GameEditViewModelFieldChecks.cs (tracking de "sucio", clase parcial).

Construccion: para edicion multiple, EditingGame =
GameTools.GetMultiGameEditObject(Games) arma el dummy MultiEditGame (ya
documentado en S28.14). Un segundo clon, originalMultiGameObj, se guarda solo
como referencia de partida pero NO se diffea contra el en el guardado â€” ver
abajo.

MECANISMO DE TRACKING DE "SUCIO": cada propiedad editable con forma de Game en
el dummy tiene un flag bool Use<Campo>Changes correspondiente (~40:
UseNameChanges, UseGenresChanges, UseDescriptionChanges, UsePlaytimeChanges,
UseGameActionsChanges, etc.), cada uno una propiedad tipo SetValue que ademas
dispara un ShowXChangeNotif agregado para banners de UI. Estos flags los
maneja EditingGame_PropertyChanged, suscripto una vez en Init() a
EditingGame.PropertyChanged. La logica por propiedad:
  case nameof(Game.Name):
    if (IsSingleGameEdit)
        UseNameChanges = Game.Name != EditingGame.Name;   // compara contra el ORIGINAL
    else
        UseNameChanges = true;                             // multi-edit: CUALQUIER toque
                                                              // lo deja en true PARA SIEMPRE
Este patron se repite para cada campo (campos escalares usan !=/Equals,
campos de lista de ids usan IsListEqual, campos con forma JSON como Links/Roms
usan IsEqualJson). HALLAZGO CLAVE: en modo de edicion simple el flag puede
volver a false si el usuario edita un campo de vuelta a su valor original
(diff real contra Game). EN MODO MULTI-EDIT NO HAY NINGUNA COMPARACION: tocar
una vez el control de una propiedad bindeada setea el Use*Changes
correspondiente a true y nunca vuelve a false por el resto de la sesion del
dialogo. O sea "que campos se tocaron" esta manejado por interaccion de UI, no
por un diff de valores entre EditingGame y originalMultiGameObj.

ALGORITMO DE GUARDADO/APLICACION â€” ConfirmDialog(bool alreadyClosing) (loop de
escritura empieza linea 662):
  1. Validacion (ruta de carpeta de tracking requerida, nombre requerido si
     UseNameChanges).
  2. Para cada campo de lista seleccionable con Use<Categoria>Changes == true
     (Genres, Developers, Publishers, Categories, Tags, Features, Platforms,
     Series, AgeRatings, Regions) y campos de lookup de valor unico (Source,
     CompletionStatus), llama AddNewItemsToDb/AddNewItemToDb primero â€” asi
     nombres de tag/genero/etc. nuevos tipeados en la UI de edicion se
     persisten en la DB ANTES de asignarse a los juegos.
  3. gamesToUpdate = IsMultiGameEdit ? Games : new List<Game> { Game } â€” LOS
     CLONES REALES DE LOS Game ORIGINALMENTE SELECCIONADOS, no el dummy.
  4. database.Games.BeginBufferUpdate(), despues RECORRE TODOS los
     gamesToUpdate y, PARA CADA CAMPO INDEPENDIENTEMENTE, SOLO SI su flag
     Use<Campo>Changes es true, copia EditingGame.<Campo> a game.<Campo> (ej.
     if (UseNameChanges) game.Name = EditingGame.Name;). Los campos cuyo flag
     es false quedan COMPLETAMENTE INTACTOS en cada juego â€” confirma que es
     una escritura selectiva/dispersa, nunca un sobreescrito total del dummy
     sobre los targets.
  5. Para los campos multi-select de lista de ids, NO simplemente asigna
     EditingGame.<Ids> â€” llama un helper local consolidateIds(selectionList,
     originalIds): toma los ids totalmente seleccionados de la lista, mas â€”
     para items que quedaron en el estado tri-estado
     indeterminado/mixto (Selected == null) â€” vuelve a agregar los ids de esa
     lista que ya estaban presentes en la lista ORIGINAL DE ESE JUEGO
     ESPECIFICO (originalIds = game.GenreIds etc., evaluado por juego adentro
     del loop). Este es el mecanismo real de "no arruinar el estado por-juego
     de campos que no tocaste" para campos multi-select: los items
     explicitamente marcados aplican a todos los juegos, los explicitamente
     desmarcados se quitan de todos, y los items mixtos/indeterminados se
     preservan individualmente por juego en vez de aplanarse a algun valor
     comun.
  6. Resultado: la escritura de edicion multiple es OPT-IN A NIVEL DE CAMPO
     (via el latch Use*Changes, que en multi-edit realmente significa "el
     usuario interactuo con este control en algun momento") combinado, para
     campos de lista, con PRESERVACION POR JUEGO de selecciones
     indeterminadas no tocadas. Enfaticamente NO es "sobreescribir todo desde
     el dummy incluyendo campos en blanco/mixtos."

--------------------------------------------------------------------------------
28.22 BACKUP / RESTORE DE BIBLIOTECA

Confirmado presente â€” una feature completa de backup/restore, pero NO hay una
feature separada de "exportar biblioteca a archivo" / "importar biblioteca
desde archivo" distinta de este mecanismo de backup, y no se encontro ningun
export de lista de juegos en CSV/JSON en ningun lado de Playnite/ ni
Playnite.DesktopApp/.

Archivo: Playnite/Backup.cs.
  enum BackupDataItem { Settings, Library, LibraryFiles, Extensions, Themes,
    ExtensionsData }
  class BackupOptions { DataDir; LibraryDir; OutputFile/OutputDir;
    BackupItems (List<BackupDataItem>); ClosedWhenDone; CancelIfGameRunning;
    RotatingBackups; }
  class BackupRestoreOptions { BackupFile; DataDir; LibraryDir; RestoreItems;
    ClosedWhenDone; CancelIfGameRunning;
    RestoreLibrarySettingsPath (permite redirigir la DB a otra ruta al
    restaurar); }

  Backup.BackupData(BackupOptions, CancellationToken): escribe un ZipArchive
  plano. Siempre incluye ambos archivos de config (config.json/config
  fullscreen) y cada archivo directo bajo el directorio de biblioteca (ahi
  vive el/los archivo(s) LiteDB) bajo una raiz de entrada library/;
  condicionalmente incluye libraryfiles/ (carpeta de cache de media),
  extension/, extensiondata/, themes/<Desktop|Fullscreen>/<dir> (los temas
  default se excluyen explicitamente del backup) segun BackupItems. Despues de
  escribir, hace LIMPIEZA DE BACKUPS ROTATIVOS: matchea por regex archivos
  PlayniteBackup-yyyy-MM-dd-HH-mm-ss.zip en el directorio de salida, se queda
  solo con los RotatingBackups + 1 mas nuevos.

  Backup.RestoreBackup(BackupRestoreOptions): extrae selectivamente segun
  RestoreItems; para Library BORRA TODOS LOS ARCHIVOS EXISTENTES EN LibraryDir
  PRIMERO, despues extrae solo entradas de nivel superior bajo library/
  (guard contra restaurar archivos anidados en subcarpetas que no
  corresponden); tiene un caso especial de reescritura a nivel-string de
  DatabasePath en el JSON de config restaurado si RestoreLibrarySettingsPath
  esta seteado (evita deserializacion JSON completa a proposito "porque no
  sabemos de que version del modelo de settings viene el backup").

  Backup.GetRestoreSelections(backupFile) inspecciona un zip y devuelve que
  BackupDataItem estan realmente presentes (para manejar la UI de opciones de
  restore).

  Backup.GetAutoBackupOptions(PlayniteSettings, dataDir, libraryDir) arma
  BackupOptions desde settings del usuario (AutoBackupIncludeExtensions/
  ExtensionsData/Themes/LibFiles, RotatingBackups).

  Disparador de auto-backup: PlayniteSettings.ShouldDataBackupOnStartup() â€”
  condicionado por AutoBackupEnabled y AutoBackupFrequency (OnceADay:
  Now.Date > LastAutoBackup.Date; OnceAWeek: (Now - LastAutoBackup).TotalDays
  > 6; cualquier otro valor lo deshabilita). Invocado desde el constructor de
  PlayniteApplication â€” escribe un JSON BackupActionFile y setea CmdLine.Backup
  para que LA APP SE REINICIE A SI MISMA HACIA UN FLUJO DEDICADO DE
  BACKUP-Y-RELANZAMIENTO manejado en Application_Startup, corrido a traves de
  un wrapper Dialogs.ActivateGlobalProgress, no inline en el hilo de UI.

  Los defaults para usuario nuevo se siembran en
  DesktopApplication.ProcessStartupWizard(): AutoBackupEnabled = true,
  LastAutoBackup = Now.AddDays(1) (pospone deliberadamente el primer backup un
  dia para no interrumpir la experiencia de primer uso), RotatingBackups = 3,
  AutoBackupDir = <ConfigRoot>/Backup, AutoBackupFrequency = OnceADay, todos
  los flags Include* en false por default.

--------------------------------------------------------------------------------
28.23 WIZARD DE PRIMER ARRANQUE

Confirmado presente. Archivos:
Playnite.DesktopApp/ViewModels/FirstTimeStartupViewModel.cs,
Playnite.DesktopApp/Windows/FirstTimeStartupWindow.xaml.cs.

Disparador â€” DesktopApplication.ProcessStartupWizard(), llamado desde
Startup() justo despues de InstantiateApp(). firstStartup es true salvo que
AppSettings.DatabasePath ya este seteado, o ya exista una DB en la ruta
default calculada (se recupera de una instalacion previa sin volver a correr
el wizard). Si es primer arranque: setea AppSettings.DatabasePath al default,
guarda settings, ABRE LA BASE DE DATOS (Database.SetDatabasePath +
Database.OpenDatabase()) ANTES de mostrar el wizard, despues construye y abre
FirstTimeStartupViewModel via FirstTimeStartupWindowFactory.

Paginas del wizard (FirstTimeStartupViewModel.Pages): Intro (0) ->
ProviderSelect (1) -> ProviderConfig (2) -> Finish (3), manejado por
NavigateNext():
  1. Intro -> ProviderSelect: descarga la lista de extensiones recomendadas
     del backend (backendClient.GetDefaultExtensions()), puebla
     RecommendeLibrariesList (lista de checkboxes de plugins de biblioteca
     conocidos por nombre/id). Si falla la descarga, salta directo a Finish
     con un dialogo de error.
  2. ProviderSelect -> ProviderConfig: por cada biblioteca marcada mas cada
     entrada en recommendedExtensions.Generic, descarga y encola el paquete
     del addon para instalar (via ExtensionInstaller.QueuePackageInstall),
     despues corre ExtensionInstaller.InstallExtensionQueue() +
     extensions.LoadPlugins(null, false, null) para cargarlos DE VERDAD EN
     PROCESO, despues por cada LibraryPlugin ya cargado junta un
     PluginSettingsItem { Name, View = lib.GetSettingsView(true), Settings =
     lib.GetSettings(true) } â€” o sea reusa la UI de settings de primer
     arranque propia de cada plugin. Si no se selecciono ninguno, salta a
     Finish.
  3. ProviderConfig: itera los selectedPlugins juntados uno a la vez
     (SetPluginConfiguration â€” llama plugin.Settings.BeginEdit() y bindea el
     UserControl del plugin), validando via Settings.VerifySettings(out
     errors) y EndEdit() antes de avanzar a la pagina de config del siguiente
     plugin o finalmente a Finish.
  4. Finish: FinishCommand/CloseCommand -> CloseView(result), que setea
     Settings.DisabledPlugins al COMPLEMENTO de lo que el usuario marco en
     LibraryPlugins (las bibliotecas no marcadas durante el wizard quedan
     deshabilitadas), dispone cada instancia de plugin, y cierra.

De vuelta en ProcessStartupWizard, despues de que el wizard cierra: aplica
wizardModel.Settings.DisabledPlugins a AppSettings, despues incondicionalmente
siembra los defaults de auto-backup descriptos en S28.22, setea
FirstTimeWizardComplete = true, guarda settings.

O SEA: el trabajo del wizard es angosto: elegir que addons de fuente de
biblioteca del catalogo integrado instalar, dejar que cada uno configure su
propio login/settings, y deshabilitar el resto â€” NO toca eleccion de tema, ni
settings generales de la app, ni importa ningun juego el mismo (eso pasa
despues en OpenMainViewAsync, que para isFirstStart == true llama
MainModel.UpdateLibrary(false, true, false) y despues
MainModel.DownloadMetadata(...)).

--------------------------------------------------------------------------------
28.24 HALLAZGOS SORPRENDENTES â€” TERCERA PASADA

1. LibraryGetGamesArgs no tiene callback de progreso â€” solo CancelToken.
   Cualquier UI de "escaneando juego X de Y" durante una actualizacion de
   biblioteca la maneja enteramente el wrapper de progreso global del caller
   alrededor de toda la llamada a GetGames(), no eventos de progreso
   empujados por el plugin. Si Bridge quiere reporte de progreso incremental
   para su primera fuente, necesita su propio mecanismo â€” el contrato real de
   Playnite no ofrece uno a nivel de interfaz de plugin.
2. Casi todo el contrato de LibraryPlugin es opcional (virtual con defaults
   triviales) salvo Name y el Id abstracto heredado. Bridge no necesita forzar
   una interfaz rica â€” un plugin que devuelve GetGames() => [] es un
   LibraryPlugin legal (aunque inutil).
3. Los flags Use*Changes de edicion multiple son LATCHES DE TOQUE DE UI en
   modo multi-edit, no diffs de valor. En modo edicion simple si comparan de
   verdad contra el Game original; en modo multi-edit, simplemente abrir un
   dropdown y reconfirmar la MISMA seleccion igual deja el flag en true para
   siempre durante esa sesion de dialogo â€” no hay chequeo de "el valor
   efectivo realmente cambio" una vez que IsSingleGameEdit == false. Importa
   para una reescritura: no asumir que "los campos sin cambios se
   auto-detectan" para edicion masiva â€” el producto real depende de flags de
   sucio disparados por binding por cada interaccion de control, y
   deliberadamente nunca los des-latchea en multi-edit.
4. La escritura de campos multi-select preserva selecciones indeterminadas NO
   TOCADAS por-juego via consolidateIds, en vez de hacer un simple "asignar
   la lista de ids seleccionados del dummy a cada juego." Esta reconciliacion
   por-juego (marcado -> aplica a todos, desmarcado -> se quita de todos,
   indeterminado -> mantiene lo que ese juego especifico ya tenia) es un
   algoritmo bastante distinto de un sobreescrito ingenuo y es facil de
   sub-construir en una reescritura.
5. La deteccion de instancia unica desempata por PID, no por reloj de pared:
   si el mutex con nombre por alguna razon no esta tomado (ej. proceso
   obsoleto) pero existen varios procesos crudos, el desempate es
   simplemente "sobrevive el de PID mas alto" â€” una heuristica que se ve
   genuinamente fragil, mantenida presumiblemente porque es barata y la
   ventana de la race es minuscula.
6. La aplicacion de tema es RECARGA DESTRUCTIVA DE TODO, no merge
   incremental â€” ApplyTheme quita y reconstruye TODA la cadena de
   diccionarios de recursos del tema default intercalada con los overrides
   del tema especificamente para satisfacer la semantica de orden de carga
   de StaticResource de WPF (comentario explicito de fix de regresion
   referenciando un issue). Una implementacion ingenua de "solo mergear los
   diccionarios del tema encima" (que es lo que muestran la mayoria de los
   tutoriales de temas de WPF) esta marcada en el codigo fuente mismo como
   rota.
7. El auto-backup al arrancar esta implementado como UN REINICIO PROPIO HACIA
   UN MODO DE CLI DEDICADO (CmdLine.Backup seteado -> la app se relanza a si
   misma con ese flag -> Application_Startup corta directo a modo
   solo-backup -> se reinicia de nuevo a modo normal cuando termina), en vez
   de hacer el backup inline durante el arranque normal. O sea "backup" es
   arquitectonicamente una corrida headless separada del mismo ejecutable,
   no una llamada a subrutina.
8. Se encontro un archivo suelto Documentacion_Reescritura_Playnite.txt (y
   copias cacheadas bajo .vs/CopilotSnapshots/...) sentado adentro del
   arbol fuente de Playnite (read-only para esta investigacion), con notas en
   espaÃ±ol que referencian HasCustomizedGameImport â€” aparentemente
   documentacion sobrante en progreso de este mismo esfuerzo de extraccion
   para Bridge, ya presente en el checkout antes de esta sesion. Se marca
   solo porque es un artefacto raro de encontrar adentro de lo que se supone
   deberia ser codigo fuente prÃ­stino de Playnite upstream â€” vale la pena que
   confirmes si eso es algo tuyo de una sesion anterior o si conviene
   excluirlo/limpiarlo de ese checkout; NO debe tratarse como comportamiento
   autoritativo de Playnite ya que evidentemente es contenido de borrador del
   proyecto de reescritura, no codigo upstream.

--------------------------------------------------------------------------------
28.25 CIERRE DE LA INVESTIGACION DE CODIGO FUENTE (28.1-28.24)

Con esta tercera pasada se cerraron los huecos identificados: bootstrap real
de la app (sin contenedor de DI â€” composicion manual por constructor, dato
importante para el diseÃ±o de Bridge.App), sistema de temas (deferido a Fase 7
pero con la forma real entendida), el contrato completo de LibraryPlugin (util
para modelar la primera fuente de Bridge sin necesidad de un sistema de
plugins completo), la mitad de escritura de edicion multiple, backup/restore
de biblioteca, y el wizard de primer arranque.

Esta seccion (28.1 a 28.25) es ahora la referencia tecnica completa de
comportamiento de Playnite que respalda el plan de fases de PLAN.md. Sigue
siendo REFERENCIA, no diseÃ±o de Bridge â€” adaptar esto a las clases y flujos
concretos de Bridge.Core/Storage/Import/Metadata/Emulation/App, decidiendo
deliberadamente en cada punto que replicar tal cual y que mejorar (ej.
tracking por polling vs Process.Exited nativo; sin InstallController generico
vs agregar uno propio; edicion multiple con latch de UI vs diff de valores),
es el siguiente paso de diseÃ±o, todavia no hecho.

--------------------------------------------------------------------------------
28.26 IGDB Y STEAM â€” VERIFICADO CONTRA EL REPO DE EXTENSIONES OFICIAL
(agregado 2026-08-05, tercera sesion, fuente:
D:\Proyectos\PlayniteExtensions-master\PlayniteExtensions-master, el repo
real de extensiones de Playnite â€” a diferencia de S28.1-28.25 que analizaron
el nucleo, esto analiza dos addons oficiales concretos porque Bridge los
necesita ahora: IGDBMetadata y SteamLibrary)

IGDB â€” Playnite NO le pega directo a IGDB desde el cliente:
  Archivo: source/Metadata/IGDBMetadata/IgdbClient.cs (completo, 119 lineas)
  plugin.cfg: { "BackendEndpoint": "https://api2.playnite.link/api/" }
  IgdbClient manda POST/GET planos a ese backend propio de Playnite:
    POST igdb/search    (SearchGames)
    POST igdb/metadata  (GetMetadata)
    GET  igdb/game/{id} (GetGame)
  NINGUN header de Authorization, NINGUN Client-ID, CERO OAuth en este
  archivo â€” confirmado por grep, no hay ni una linea de client_secret/token/
  api.igdb.com en toda la carpeta IGDBMetadata. El servidor api2.playnite.link
  (del autor de Playnite) es el que tiene las credenciales reales de IGDB y
  hace de proxy â€” el usuario final nunca ve ni carga un Client ID/Secret.
  CONFIRMA lo que ya se le explico al usuario en esta sesion (S no numerada,
  respuesta sobre "como hace Playnite para IGDB sin pedirme credenciales").
  Decision de Bridge (ADR-10, ARCHITECTURE.md) de pedirle credenciales al
  usuario sigue siendo valida â€” es la alternativa sin servidor propio, mas
  simple para un mantenedor solo, con el costo real y ya documentado de que
  el usuario tiene que registrarse en Twitch una vez.

STEAM â€” deteccion de juegos instalados, 100% archivos locales, sin red:
  Archivo principal: source/Libraries/SteamLibrary/Services/SteamLocalService.cs
  Archivo de ubicacion: source/Libraries/SteamLibrary/Steam.cs

  1. Steam.InstallationPath (Steam.cs linea ~34) lee el registro de Windows:
     Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")
     (valor "SteamPath" ahi adentro, tipicamente)
  2. GetLibraryFolders() (SteamLocalService.cs linea 341): arranca con
     {Steam.InstallationPath}, y ADEMAS parsea
     {InstallationPath}/steamapps/libraryfolders.vdf (formato VDF de Valve,
     NO json â€” se parsea con la clase KeyValue del paquete NuGet SteamKit2)
     para encontrar carpetas de biblioteca adicionales que el usuario haya
     agregado en otros discos.
  3. GetInstalledGamesFromFolder(path) (linea 129): por cada carpeta de
     biblioteca, escanea {carpeta}/steamapps/ buscando archivos
     appmanifest*.acf (tambien formato VDF).
  4. GetInstalledGameFromFile(path) (linea 72): parsea cada .acf con el mismo
     KeyValue parser. Campos que lee: StateFlags (debe tener el flag
     FullyInstalled o se descarta â€” hay un enum AppStateFlags completo con
     16+ flags, linea 377), name (o UserConfig.name si name esta vacio),
     appID (el Steam AppID, se convierte a GameID), installDir (se resuelve
     como {carpetaAppmanifest}/common/{installDir}, si no existe prueba
     {carpetaAppmanifest}/music/{installDir} para soundtracks).
  5. Descarta el AppID 228980 explicitamente (es "Steamworks Common
     Redistributables", no un juego real).
  6. Devuelve GameMetadata con: GameId = AppID como string, Name (limpiado
     con RemoveTrademarks()), InstallDirectory, IsInstalled = true,
     Platforms = pc_windows.
  7. Tambien maneja mods (GoldSrc/Source engine, HL1/HL2) por separado, no
     relevante para el MVP de Bridge.

  CONCLUSION PARA BRIDGE: para implementar deteccion de Steam instalado,
  el flujo real es Registro de Windows -> parsear libraryfolders.vdf (VDF,
  no JSON) -> por cada carpeta, escanear appmanifest*.acf (VDF) -> filtrar
  por StateFlags.FullyInstalled -> extraer appID/name/installDir. Bridge
  necesitaria: (a) un parser de VDF (SteamKit2 tiene uno gratis via NuGet,
  la clase KeyValue especificamente, evaluar si conviene traer todo
  SteamKit2 solo por eso o portar un parser VDF minimo), (b) el mismo mapeo
  a GameMetadata.ExternalId=AppID, Name, InstallDirectory, IsInstalled=true.
  Epic no se investigo en esta pasada (no estaba en el repo de extensiones
  revisado, es otro repo/addon) â€” queda pendiente si se necesita.

  ICONO 32x32 DEL CLIENTE STEAM (hallazgo 2026-08-07, usado por Bridge):
  el icono cuadrado que Playnite muestra en la lista de la biblioteca es el
  "clienticon" de Steam, un archivo de 40 caracteres hex (hash) que el
  CLIENTE Steam guarda localmente en {InstallationPath}/appcache/librarycache/
  {appid}/{40hex}.jpg â€” una imagen real de 32x32, junto a header.jpg
  (460x215) y library_*.jpg. La API web store.steampowered.com/api/appdetails
  YA NO devuelve el campo clienticon (verificado contra la API real con
  Dota 2 570, HL2 220, CS2 730, CS 1.6 10, GMod 4000 y Destiny 2 1085660 â€”
  el campo sale vacio en todas). Por eso Bridge no copia la via de SteamKit2
  (PICSGetProductInfo) que usa el addon real para el hash, sino que lee el
  archivo local que Steam ya descargo (verificado: 628 apps con icono 32x32
  real en el cache de esta maquina, no placeholders), con fallback a la URL
  de header.jpg que provee SteamMetadataProvider. Implementado en
  Bridge.Import/Steam/SteamLocalIconResolver.cs; MainViewModel.ApplySteamLocalIcon
  lo aplica al cargar y tras cada descarga de metadata.

28.27 EXTENSIONES DE BIBLIOTECAS â€” EL PIPELINE REAL COMPLETO DE STEAM,
      EL BACKEND DE IGDB Y COMO IMPORTAN LAS OTRAS 9 LIBRERIAS
(agregado 2026-08-06, cuarta sesion, fuente: el mismo repo
D:\Proyectos\PlayniteExtensions-master\PlayniteExtensions-master â€” esto
amplia 28.26, que solo cubrio los juegos instalados de Steam y el proxy de
IGDB. Aqui se documenta el agregador completo de Steam, el flujo real del
backend de IGDB y el metodo de deteccion/import de cada libreria del repo).

A. STEAM â€” EL AGREGADOR (SteamServiceAggregator.GetGamesAsync)
  Archivo: source/Libraries/SteamLibrary/Services/SteamServiceAggregator.cs
  (392 lineas) + SteamLibrary.cs (GetGames -> crea el agregador con los
  servicios). El import real NO es un solo flujo: es la fusion de varias
  fuentes por GameId (el AppID de Steam como string).

  Orden del agregador (GetGamesAsync, linea 37):
  1. settings.ImportInstalledGames (default true): importa instalados 100%
     local via SteamLocalService.GetInstalledGames() (lo de 28.26). Marca
     installedGameIds.
  2. settings.ConnectAccount (default false): via online:
     a. Si IsPrivateAccount: GetOwnedGamesApiKey(apiKey) +
        GetClientLastPlayedTimesApiKey -> LastPlayTimeSync = now.
     b. Si no: SteamStoreService.GetAccessTokenAsync() (scrapea
        store.steampowered.com/dynamicstore/userdata/ con WebView para
        sacar rgOwnedApps y un token), luego GetOwnedGamesWeb. FALLOBACK:
        si GetClientAppList falla, usa GetSteamStoreGamesAsync (userdata
        rgOwnedApps -> POST al backend de Playnite steam/appinfo para
        nombre/tipo localizado, filtra type=="game").
     c. ImportFamilySharedGames (default true): FamilyGroupsService.
     d. GetClientLastPlayedTimesWeb para playtimes.
  3. AdditionalAccounts: cuentas extra con su propia API key; se salta las
     que ya estan en el grupo familiar (familySharingUserIds).
  4. IgnoreOtherInstalled: elimina instalados que NO aparecen en la
     biblioteca online de cuentas bajo control del usuario.
  5. GetGamesFromExtraIds: IDs manuales ("Nombre: url.../app/240" o
     "240;Nombre").
  6. Filtro final (linea 220): nombre no vacio y
     (IsInstalled || settings.ImportUninstalledGames).
  7. Source default "Steam" para los instalados (linea 222-225).
  8. UpdateExistingGames (linea 268): en la DB, solo sincroniza SourceId e
     InstallSize si el juego ya existe (con BufferedUpdate), sin tocar
     campos editados por el usuario.

  Endpoints web reales del import (todos con retry en 429, 5 intentos):
    api.steampowered.com/IPlayerService/GetOwnedGames/v1/  -> owned + playtime
        params: key|access_token, steamid, include_appinfo=true,
        include_played_free_games=true, include_free_sub, language.
    api.steampowered.com/IPlayerService/ClientGetLastPlayedTimes/v1/ -> playtime
        params: key|access_token, min_last_played (incremental).
    api.steampowered.com/IClientCommService/GetClientAppList/v1/ -> juegos
        de la sesion del cliente con bytes_required (=> InstallSize).
    api.steampowered.com/IFamilyGroupsService/GetFamilyGroupForUser/v1/
        y GetSharedLibraryApps/v1/ -> Family Sharing (Source "Steam Family
        Sharing").
    store.steampowered.com/dynamicstore/userdata/ (fallback, WebView).
  NOTA: localconfig.vdf NO se usa en el import actual â€” es codigo heredado
  muerto (existe GetGamesLastActivity pero nunca se invoca). El playtime
  viene SIEMPRE de la Web API, no de archivos locales.

  Playtime/LastActivity (PlayerService.cs:36-72):
    Playtime = playtime_forever * 60  (minutos -> segundos)
    LastActivity = GetLastPlayedDateTime(rtime_last_played) (unix -> DateTime;
    unix 0 -> null). NO existe import de PlayCount.
  Juegos no instalados: se crean igual con InstallDirectory=null,
  IsInstalled=false, y entran solo si ImportUninstalledGames.
  PlayAction: NO se serializa GameAction; el launch es dinamico
  (SteamPlayController.Play -> "steam://rungameid/{GameId}" o
  "steam://launch/{GameId}/Dialog") + ProcessMonitor.WatchDirectoryProcesses
  del InstallDirectory para detectar el proceso real y medir la sesion.

  Settings que importan (SteamLibrarySettingsViewModel.cs):
    ImportInstalledGames (true), ConnectAccount (false),
    ImportUninstalledGames (false, requiere ConnectAccount),
    IsPrivateAccount (API key vs web token), IncludeFreeSubGames,
    ImportFamilySharedGames (true), IgnoreOtherInstalled,
    AdditionalAccounts (AccountId+RuntimeApiKey+ImportPlayTime),
    ExtraIDsToImport, LastPlayTimeSync. La API key se cifra en keys.dat
    (Encryption AES-256 con password = SID de Windows).

B. IGDB â€” EL FLUJO REAL (backend + extension). Ampliacion de 28.26.
  28.26 confirmo que el addon es un proxy del backend api2.playnite.link.
  El flujo que Bridge debe replicar si llama a IGDB directo (ADR-10):
  - OAuth: POST id.twitch.tv/oauth2/token con client_id+client_secret,
    grant_type=client_credentials. Se re-autentica cuando una peticion
    devuelve 401/403 (no hay refresh por TTL). Token cacheado en disco.
  - Rate limit del backend: 4 requests/segundo, timeout 50s. Errores:
    401/403 -> reauth + 1 reintento; 429 -> delay 500ms + 1 reintento;
    500 -> delay 2s + 1 reintento.
  - Busqueda (backend, MongoDB textScore): umbral 0.6 (0.9 en
    alternative_names), penaliza ports (game_type==11, -0.01), merge y
    DistinctBy. Matching automatico TryMatchGame (normalizar nombre:
    ordena "X, The", quita [...](...){} y marcas, numeros->romanos,
    prefijo "The", and<->&, sin apostrofes, sin ":" "-", sin subtitulo;
    con aÃ±o usa el aÃ±o; multi-match: librerias -> el mas nuevo, manual ->
    el mas viejo). Lookup exacto por ExternalGame para Steam/GOG/Epic/Itch
    (sin heuristica).
  - Mapeo campo a campo (IgdbLazyMetadataProvider.cs), lo que Bridge NO
    tiene aun:
    Developers/Publishers = involved_companies_expanded filtrando
        developer / publisher -> company_expanded.name.
    Genres = genres_expanded.name (tienen id pero el addon usa name;
        sin localizacion propia, queda en ingles del backend).
    Features = game_modes_expanded TitleCase (+ "VR" si
        player_perspectives == "Virtual Reality").
    CriticScore = aggregated_rating; CommunityScore = rating.
    AgeRating = age_ratings org 1(ESRB)/2(PEGI) -> "ESRB {rating}".
    Series = collections_expanded.
    Description = summary con \n -> \n<br>.
    Links = websites_expanded.
    NO expone Platform, Tags ni InstallSize.
  - Imagenes (ImageSizes.cs): reescritura de URL de IGDB con regex
    /t_[^/]+ -> t_{size}. Icon = thumb_2x (180x180); Cover = original o
    1080p si height>1080; Background = 1080p/original (seleccion
    First/Random/Select segun ImageSelectionPriority). TamaÃ±os disponibles:
    cover_small 90x128, screenshot_med 569x320, cover_big 264x374,
    logo_med 284x160, screenshot_big 889x500, screenshot_huge 1280x720,
    thumb 90x90, micro 35x35, 720p, 1080p, original.
  - Settings del addon: UseScreenshotsIfNecessary, ImageSelectionPriority,
    UseCoverAsIcon. (El Client ID/Secret del usuario no existe en Playnite
    porque el backend los tiene.)

C. LAS OTRAS 9 LIBRERIAS â€” METODO DE DETECCION/IMPORT (resumen por
   libreria, todas en source/Libraries/). Patron comun: GetGames() =
   1) importa instalados (local) si el setting, 2) si ConnectAccount obtiene
   la biblioteca de la cuenta y, si ImportUninstalledGames=false, filtra a
   solo instalados, 3) merge por GameId, 4) notificacion de error.
   GameMetadata siempre: Source=MetadataNameProperty(nombre), Platforms=
   {pc_windows}, Name=.RemoveTrademarks(), GameId=ID del ecosistema
   (NO global: appName Epic, numero GOG, ProductId Battle.net, PFN Xbox,
   {machine_name}_{human_name} Humble, id itchio, uplay_id, titleId).

  - Epic: instalados = %PROGRAMDATA%\Epic\UnrealEngineLauncher\
    LauncherInstalled.dat + EpicGamesLauncher\Data\Manifests\*.item (JSON,
    AppName), filtra DLC (MainGameAppName). Owned = API web OAuth
    (library/api/public/items, catalog bulk por namespace/catalogItemId,
    playtime items). GameId=AppName. Play=com.epicgames.launcher://apps/{id}.
    Requiere login para owned.
  - GOG: instalados = registry de desinstalacion (clave ^(\d+)_is1,
    Publisher GOG.com) + goggame-{id}.info (DLC y playTasks). Owned = API
    web con cookies de WebView (menu.gog.com/v1/account/basic,
    www.gog.com/u/{user}/games/stats, fallback getFilteredProducts). Play=
    Galaxy /launchViaAutostart. Galaxy NO se usa para detectar.
  - BattleNet: instalados = registry (--uid en UninstallString) + lista
    hardcodeada BattleNetGames.cs + fallback product.db protobuf (no SQLite)
    en c:\ProgramData\Battle.net\Agent\. Owned = account.battle.net/api/
    games-and-subs (cookies WebView). GameId=ProductId (WoW, D3...).
  - Xbox: NO es local. UWP Apps del sistema + API Xbox Live (OAuth
    Live+XSTS: login.live.com/oauth20_*, user.auth.xboxlive.com,
    xsts.auth.xboxlive.com; titlehub.xboxlive.com/titles/titlehistory,
    userstats.xboxlive.com/batch). GameId=PFN. Requiere cuenta incluso
    para instalados (la API dice que UWP es un juego).
  - Amazon: instalados = SQLite %LOCALAPPDATA%\Amazon Games\Data\Games\Sql\
    GameInstallInfo.sqlite (Installed=1). Owned = gaming.amazon.com/api/
    distribution/entitlements (PKCE + device registration con MachineGuid).
    Play=amazon-games://play/{id} o exe de fuel.json.
  - Uplay (Ubisoft Connect): instalados = registry
    HKLM\SOFTWARE\ubisoft\Launcher\Installs\{GameId}. Owned = cachÃ© LOCAL
    protobuf del cliente %LOCALAPPDATA%\Ubisoft Game Launcher\cache\
    configuration\configurations (YAML dentro). Sin web, sin login del
    plugin (pero requiere que el cliente tenga sesion). Play=uplay://launch.
  - Humble: owned = API web (humblebundle.com/api/v1/orders?gamekeys=...,
    /home/library con #user-home-json-data; Trove publico sin auth).
    Instalados = %APPDATA%\Humble App\config.json. Usa ImportGames
    customizado (no GetGames). GameId={machine_name}_{human_name}.
  - Itchio: butler (daemon itch) JSON-RPC 2.0 sobre TCP, sin HTTP directo.
    Instalados = Fetch.Caves (cave.game.id); owned = ProfileOwnedKeys +
    ProfileCollections/GameRecords. Requiere butler.db con sesion del
    cliente itch. Play=.itch.toml + butler.Launch.
  - Rockstar: 100% local registry (regex "...uninstall={titleId}" en
    UninstallString) + lista hardcodeada RockstarGames.cs. Sin owned, sin
    web. Play=Launcher.exe -launchTitleInFolder "{dir}".

  Clasificacion para Bridge (orden de dificultad si se quisieran soportar):
  locales puros: Rockstar, Uplay (owned via cache local del cliente).
  locales instalados + web owned: Epic, GOG, BattleNet, Amazon, Humble.
  requieren cuenta incluso para instalados: Xbox. itchio requiere butler.
  Dato clave: los tokens se cifran con Encryption.cs (AES-256, password =
  SID de Windows) en tokens.json/login.json/xsts.json/extras.dat, o se usan
  cookies de WebView (sesion fragil: GOG/Humble/BattleNet).

  IMPLICACIONES PARA BRIDGE:
  1. El import actual de Steam en Bridge (instalados, local) == el paso 1
     del agregador real (ImportInstalledGames). Lo que falta para parity:
     la capa online (owned + playtime via Web API) que requiere login del
     usuario; y el merge/sync de SourceId+InstallSize sin tocar campos
     editados (Bridge ya syncs IsInstalled/InstallDirectory).
  2. IGDB: Bridge mapea 5 campos; el addon real mapea 15+ y el matching
     automatico vive en el backend (heuristica TryMatchGame). Para un
     match automatico confiable hay que portar esa heuristica.
  3. El playcount no se importa en ninguna libreria (solo playtime y
     lastActivity) â€” coherente con el modelo actual de Bridge.
  4. Los GameId de cada ecosistema son su ID nativo; Bridge deberia
     conservar ese esquema como ExternalId por SourceId.
================================================================================

FIN DEL DOCUMENTO
