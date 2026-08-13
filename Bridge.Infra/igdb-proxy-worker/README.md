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
llama a IGDB con la metadata y devuelve el resultado. Es el mismo diseño que
Playnite usa con su propio backend (api2.playnite.link), pero con
infraestructura propia de Bridge.

## Endpoints

### `POST /metadata`
Recibe el nombre (y opcionalmente año) del juego:
```json
{ "name": "Genshin Impact", "releaseYear": 2020 }
```
Devuelve la metadata de IGDB en su formato crudo:
`id, name, summary, first_release_date, cover.url, artworks[].url,
genres[].name, involved_companies[].company.name, rating,
aggregated_rating, websites[].url, websites[].type`

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
Playnite (`PlayniteIgdbProvider`) y luego al IGDB del usuario (`IgdbMetadataProvider`).
