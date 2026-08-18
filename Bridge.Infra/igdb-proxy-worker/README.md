# Bridge IGDB Proxy Worker

Un Cloudflare Worker que actúa como proxy hacia la API de IGDB
(api.igdb.com). Oculta las credenciales de Twitch/IGDB usando Worker Secrets:
el Client Secret nunca está en el código ni en el cliente de escritorio.

## Arquitectura

```
Bridge.exe ──► https://bridge-igdb.<cuenta>.workers.dev/metadata ──► api.igdb.com
                  │
                  └── credenciales de Twitch como Worker Secrets (solo aquí)
```

Bridge (la app de escritorio) hace una petición HTTP POST a este Worker; el
Worker obtiene un app token de Twitch con `grant_type=client_credentials`,
llama a IGDB con la metadata y devuelve el resultado. Las credenciales viven
solo en el Worker (Worker Secrets), nunca en el cliente de escritorio.

## Endpoints

### `POST /metadata`
Recibe el nombre (y opcionalmente año) del juego:
```json
{ "name": "Genshin Impact", "releaseYear": 2020 }
```
Devuelve la metadata de IGDB en su formato crudo:
`id, name, summary, first_release_date, cover.url, artworks[].url,
screenshots[].url, genres[].name, involved_companies[].company.name, rating,
aggregated_rating, websites[].url, websites[].type`

Los `screenshots[].url` son las capturas reales (16:9) del juego — Bridge las
muestra como galería en el detalle para juegos no-Steam (Epic, manuales).

### Estrategia de búsqueda (dos queries en orden)

El endpoint `metadata` intenta dos búsquedas de IGDB y devuelve la primera que
tenga resultados:

1. **Coincidencia literal** — `where name ~ "..."` (case-insensitive, contiene
   el texto). Mantiene los resultados exactos: "Genshin Impact" devuelve el
   juego base y no un DLC/spin-off cuyo nombre empieza igual.
2. **Búsqueda fuzzy** — `search "..."` (endpoint de texto libre de IGDB), solo
   si la literal no dio nada. Tokeniza el nombre, ignora acentos y guiones, y
   devuelve el mejor match por relevancia — lo que necesitan los títulos de
   ROMs ("Pokemon - Emerald Version" → "Pokémon Emerald Version"), que con el
   match literal fallaban.

Ambas queries piden los mismos campos; la lista de queries la construye
`buildGameQueries` y el handler usa la primera con resultados.

### `POST /auth` (solo diagnóstico)
Obtiene un token OAuth de Twitch. **No lo expongas en producción sin
protegerlo** — se usa solo para depurar.

## Configuración y deploy

Requisitos: cuenta de Cloudflare y credenciales de Twitch en
https://dev.twitch.tv/console/apps (flujo `client_credentials` — **no** necesita
OAuth redirect URIs).

```bash
npm install
npx wrangler login            # una sola vez, abre el navegador
npx wrangler secret put TWITCH_CLIENT_ID
npx wrangler secret put TWITCH_CLIENT_SECRET
npx wrangler deploy
```

Los secrets se guardan cifrados en Cloudflare, nunca en este repo. El archivo
`.gitignore` excluye `node_modules/`, `.dev.vars`, `.env` y `.wrangler/`.

## Desarrollo local

Crea `.dev.vars` (no lo subas a git) con los secrets y ejecuta `npx wrangler dev`.

## Fallback en Bridge

`Bridge.Metadata/BridgeIgdbProvider.cs` consume este Worker como primer
provider de IGDB. Si el Worker no responde, Bridge cae al proxy público de
En la app, el orden de proveedores es: Worker propio → proxy publico legacy (`PlayniteIgdbProvider`) → IGDB del usuario (`IgdbMetadataProvider`).
