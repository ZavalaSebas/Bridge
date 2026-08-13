/**
 * IGDB API Proxy Worker
 *
 * Proxy hacia la API de IGDB (api.igdb.com) que oculta las credenciales
 * de Twitch/IGDB usando Worker Secrets.
 *
 * Endpoints:
 *   POST /metadata — busca un juego por nombre y año, devuelve metadata completa
 *   POST /auth     — obtiene un token OAuth de Twitch (opcional, para depuración)
 */

// ─── Tipos ───────────────────────────────────────────────────────────────────

interface Env {
  TWITCH_CLIENT_ID: string;
  TWITCH_CLIENT_SECRET: string;
}

interface MetadataRequest {
  name: string;
  releaseYear?: number;
}

// ─── Constantes ───────────────────────────────────────────────────────────────

const TWITCH_TOKEN_URL = "https://id.twitch.tv/oauth2/token";
const IGDB_API_URL = "https://api.igdb.com/v4";

// Cachea el token de Twitch en memoria (por instancia de Worker)
let cachedToken: { value: string; expiresAt: number } | null = null;

// ─── Helpers ─────────────────────────────────────────────────────────────────

/**
 * Obtiene un app access token de Twitch usando client_credentials.
 * Usa caché en memoria para evitar pedir un token en cada request.
 */
async function getTwitchToken(env: Env): Promise<string> {
  // Si tenemos un token válido en caché, lo reusamos
  if (cachedToken && Date.now() < cachedToken.expiresAt) {
    return cachedToken.value;
  }

  const res = await fetch(TWITCH_TOKEN_URL, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      client_id: env.TWITCH_CLIENT_ID,
      client_secret: env.TWITCH_CLIENT_SECRET,
      grant_type: "client_credentials",
    }),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Twitch token request failed (${res.status}): ${text}`);
  }

  const data = (await res.json()) as {
    access_token: string;
    expires_in: number;
  };

  // Guarda el token con un margen de 60s antes de la expiración real
  cachedToken = {
    value: data.access_token,
    expiresAt: Date.now() + (data.expires_in - 60) * 1000,
  };

  return data.access_token;
}

/**
 * Hace una petición POST a un endpoint de IGDB con el body en texto plano
 * (IGDB usa el formato Apicalypse).
 */
async function igdbRequest(
  endpoint: string,
  body: string,
  env: Env,
): Promise<unknown> {
  const token = await getTwitchToken(env);

  const res = await fetch(`${IGDB_API_URL}/${endpoint}`, {
    method: "POST",
    headers: {
      "Client-ID": env.TWITCH_CLIENT_ID,
      Authorization: `Bearer ${token}`,
      "Content-Type": "text/plain",
    },
    body,
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`IGDB request to /${endpoint} failed (${res.status}): ${text}`);
  }

  return res.json();
}

/**
 * Construye la query de Apicalypse para buscar un juego por nombre (y opcionalmente año).
 * Pide todos los campos de metadata que el endpoint /metadata necesita.
 */
function buildGameQuery(name: string, releaseYear?: number): string {
  // Escapa comillas dobles en el nombre para la búsqueda
  const safeName = name.replace(/"/g, '\\"');

  // Filtro base: busca por nombre aproximado
  let whereClause = `name ~ "${safeName}"`;

  // Si se especifica el año, filtra por el timestamp de first_release_date
  if (releaseYear) {
    const start = Math.floor(Date.UTC(releaseYear, 0, 1) / 1000); // 1 ene 00:00 UTC
    const end = Math.floor(Date.UTC(releaseYear + 1, 0, 1) / 1000); // 1 ene del año siguiente
    whereClause += ` & first_release_date >= ${start} & first_release_date < ${end}`;
  }

  return [
    "fields id,name,summary,first_release_date,cover.image_id,cover.url,",
    "artworks.image_id,artworks.url,genres.name,involved_companies.company.name,",
    "involved_companies.publisher,involved_companies.developer,",
    "rating,rating_count,aggregated_rating,aggregated_rating_count,",
    "total_rating,total_rating_count,websites.url,websites.type;",
    `where ${whereClause};`,
    "limit 1;",
  ].join("");
}

// ─── Handlers de endpoints ──────────────────────────────────────────────────

/**
 * POST /metadata
 * Body: { "name": "Genshin Impact", "releaseYear": 2020 }
 */
async function handleMetadata(request: Request, env: Env): Promise<Response> {
  let body: MetadataRequest;
  try {
    body = (await request.json()) as MetadataRequest;
  } catch {
    return Response.json(
      { error: "Body JSON inválido. Se espera { name: string, releaseYear?: number }." },
      { status: 400 },
    );
  }

  if (!body.name || typeof body.name !== "string") {
    return Response.json(
      { error: "El campo 'name' es obligatorio y debe ser string." },
      { status: 400 },
    );
  }

  try {
    const query = buildGameQuery(body.name, body.releaseYear);
    const result = await igdbRequest("games", query, env);

    const games = result as unknown[];
    if (!Array.isArray(games) || games.length === 0) {
      return Response.json(
        {
          error: "Juego no encontrado.",
          query: { name: body.name, releaseYear: body.releaseYear },
        },
        { status: 404 },
      );
    }

    return Response.json(games[0], { status: 200 });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Error desconocido";
    return Response.json({ error: message }, { status: 502 });
  }
}

/**
 * POST /auth
 * Devuelve un token OAuth de Twitch (client_credentials).
 * Útil para depuración o si tu app de C# quiere manejar el token directamente.
 */
async function handleAuth(env: Env): Promise<Response> {
  try {
    const token = await getTwitchToken(env);
    return Response.json(
      {
        access_token: token,
        token_type: "bearer",
        expires_in: cachedToken
          ? Math.floor((cachedToken.expiresAt - Date.now()) / 1000)
          : 0,
      },
      { status: 200 },
    );
  } catch (err) {
    const message = err instanceof Error ? err.message : "Error desconocido";
    return Response.json({ error: message }, { status: 502 });
  }
}

// ─── Router principal ────────────────────────────────────────────────────────

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    const { pathname } = url;
    const method = request.method.toUpperCase();

    // CORS headers para que tu app de C# pueda consumir el Worker sin problemas
    const corsHeaders = {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type",
    };

    // Preflight CORS
    if (method === "OPTIONS") {
      return new Response(null, { status: 204, headers: corsHeaders });
    }

    // Validar que los secrets estén configurados
    if (!env.TWITCH_CLIENT_ID || !env.TWITCH_CLIENT_SECRET) {
      return Response.json(
        {
          error:
            "Faltan secrets. Configura TWITCH_CLIENT_ID y TWITCH_CLIENT_SECRET con `wrangler secret put`.",
        },
        { status: 500 },
      );
    }

    // ── Routing ──
    if (pathname === "/metadata" && method === "POST") {
      const res = await handleMetadata(request, env);
      // Agrega CORS a la respuesta
      const headers = new Headers(res.headers);
      for (const [k, v] of Object.entries(corsHeaders)) headers.set(k, v);
      return new Response(res.body, { status: res.status, headers });
    }

    if (pathname === "/auth" && method === "POST") {
      const res = await handleAuth(env);
      const headers = new Headers(res.headers);
      for (const [k, v] of Object.entries(corsHeaders)) headers.set(k, v);
      return new Response(res.body, { status: res.status, headers });
    }

    // 404 para cualquier otra ruta
    return Response.json(
      { error: "Ruta no encontrada. Endpoints disponibles: POST /metadata, POST /auth" },
      { status: 404 },
    );
  },
} satisfies ExportedHandler<Env>;
