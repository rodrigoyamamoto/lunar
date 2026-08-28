/**
 * Lunar Asset Studio — Foreground Isolation Worker
 *
 * Narrow provider-edge adapter that receives raw image bytes from the
 * Lunar .NET backend, applies Cloudflare Images foreground segmentation
 * (segment=foreground), and returns transparent PNG bytes.
 *
 * The Worker does NOT know about Lunar Assets, Artifacts, WorkflowExecutions,
 * or repositories. It is a pure transformation adapter.
 *
 * Authentication: Bearer token shared with the Lunar backend
 * (LUNAR_FOREGROUND_ISOLATION_TOKEN), configured via:
 *   npx wrangler secret put LUNAR_FOREGROUND_ISOLATION_TOKEN
 *
 * The token is never declared in wrangler.jsonc vars. It is a Worker
 * secret bound at runtime.
 */

export interface ImagesBinding {
  input(stream: ReadableStream): ImageHandle;
}

export interface ImageHandle {
  transform(options: Record<string, unknown>): ImageHandle;
  output(options: { format: string }): ImageResponse;
}

export interface ImageResponse {
  response(): Response;
}

export interface Env {
  IMAGES: ImagesBinding;
  LUNAR_FOREGROUND_ISOLATION_TOKEN: string;
}

const MAX_INPUT_BYTES = 20 * 1024 * 1024; // 20 MB (Cloudflare Images binding limit)

/**
 * Request handler exported for executable unit testing.
 * Tests import this function and pass a fake Env/IMAGES binding.
 */
export async function handleRequest(
  request: Request,
  env: Env,
): Promise<Response> {
  if (request.method !== "POST") {
    return jsonError(405, "method_not_allowed", "Only POST is accepted.");
  }

  // Guard against missing Worker secret. If the deployer forgot
  // `npx wrangler secret put LUNAR_FOREGROUND_ISOLATION_TOKEN`,
  // the binding will be undefined or empty. This is a service
  // misconfiguration, not an invalid caller credential.
  const configuredToken = env.LUNAR_FOREGROUND_ISOLATION_TOKEN;
  if (!configuredToken) {
    return jsonError(
      503,
      "service_not_configured",
      "Foreground isolation service is not configured.");
  }

  const authHeader = request.headers.get("Authorization");
  if (!authHeader || !authHeader.startsWith("Bearer ")) {
    return jsonError(401, "missing_authorization", "Authorization header is required.");
  }

  const token = authHeader.slice(7);
  if (!constantTimeEquals(token, configuredToken)) {
    return jsonError(401, "invalid_authorization", "Invalid service credential.");
  }

  const contentType = request.headers.get("Content-Type");
  if (!contentType || !contentType.startsWith("image/")) {
    return jsonError(400, "invalid_content_type", "Content-Type must be an image media type.");
  }

  const body = request.body;
  if (!body) {
    return jsonError(400, "empty_body", "Request body is required.");
  }

  const contentLength = request.headers.get("Content-Length");
  if (contentLength && parseInt(contentLength, 10) > MAX_INPUT_BYTES) {
    return jsonError(
      413,
      "payload_too_large",
      `Request body exceeds the ${MAX_INPUT_BYTES} byte limit.`,
    );
  }

  let providerResponse: Response;

  try {
    providerResponse = await env.IMAGES
      .input(body)
      .transform({ segment: "foreground" })
      .output({ format: "image/png" })
      .response();
  } catch {
    // The binding threw an exception rather than returning an error
    // Response. Do not return raw provider/runtime exception messages
    // across the service boundary.
    return jsonError(502, "provider_error", "Foreground isolation provider failed.");
  }

  // Preserve the provider's HTTP failure semantics. Do not force a
  // failed binding response into a 200 image/png. Cloudflare Images
  // returns error responses (e.g. 429 for quota exhaustion, 5xx for
  // internal errors) with documented error codes.
  if (!providerResponse.ok) {
    const status = providerResponse.status;
    // Map to the same status class so the .NET client can classify
    // the failure correctly. Do not forward the raw provider body.
    return jsonError(
      status,
      "provider_error",
      "Foreground isolation provider failed.");
  }

  return new Response(providerResponse.body, {
    headers: {
      "Content-Type": "image/png",
      "Cache-Control": "no-store",
    },
  });
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    return handleRequest(request, env);
  },
};

function jsonError(status: number, code: string, message: string): Response {
  return new Response(JSON.stringify({ code, message }), {
    status,
    headers: {
      "Content-Type": "application/json",
      "Cache-Control": "no-store",
    },
  });
}

function constantTimeEquals(a: string, b: string): boolean {
  if (a.length !== b.length) {
    return false;
  }

  let result = 0;
  for (let i = 0; i < a.length; i++) {
    result |= a.charCodeAt(i) ^ b.charCodeAt(i);
  }

  return result === 0;
}
