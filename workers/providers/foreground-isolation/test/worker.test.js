import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { handleRequest } from "../src/index.ts";

const VALID_TOKEN = "test-service-token";
const PNG_BYTES = new Uint8Array([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01]);
const JPEG_BYTES = new Uint8Array([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46]);

function createEnv(options = {}) {
  return {
    IMAGES: options.imagesBinding ?? createFakeImagesBinding(),
    LUNAR_FOREGROUND_ISOLATION_TOKEN: options.token ?? VALID_TOKEN,
  };
}

function createFakeImagesBinding(options = {}) {
  return {
    input(stream) {
      if (options.throwOnInput) {
        throw options.throwOnInput;
      }
      return createFakeImageHandle(options);
    },
  };
}

function createFakeImageHandle(options = {}) {
  return {
    transform(opts) {
      options._capturedTransform = opts;
      return this;
    },
    output(opts) {
      options._capturedOutput = opts;
      return createFakeImageResponse(options);
    },
  };
}

function createFakeImageResponse() {
  return {
    response() {
      return new Response(PNG_BYTES, {
        headers: {
          "Content-Type": "image/png",
        },
      });
    },
  };
}

function createImageRequest(options = {}) {
  const method = options.method ?? "POST";
  const headers = new Headers();
  headers.set("Content-Type", "image/jpeg");
  headers.set("Authorization", `Bearer ${VALID_TOKEN}`);

  if (options.headers) {
    for (const [key, value] of Object.entries(options.headers)) {
      if (value === "") {
        headers.delete(key);
      } else {
        headers.set(key, value);
      }
    }
  }

  const body = options.body !== undefined ? options.body : new ReadableStream({
    start(controller) {
      controller.enqueue(JPEG_BYTES);
      controller.close();
    },
  });

  const init = {
    method,
    headers,
    body: method === "POST" ? body : undefined,
  };

  if (init.body != null) {
    init.duplex = "half";
  }

  return new Request("https://worker.example.com/", init);
}

async function readBody(response) {
  return await response.text();
}


describe("Foreground Isolation Worker — handleRequest", () => {
  it("non-POST returns 405 method_not_allowed", async () => {
    const env = createEnv();
    const request = createImageRequest({ method: "GET" });

    const response = await handleRequest(request, env);

    assert.equal(response.status, 405);
    const body = JSON.parse(await readBody(response));
    assert.equal(body.code, "method_not_allowed");
  });

  it("missing Authorization returns 401", async () => {
    const env = createEnv();
    const request = createImageRequest({
      headers: { Authorization: "" },
    });

    const response = await handleRequest(request, env);

    assert.equal(response.status, 401);
    const body = JSON.parse(await readBody(response));
    assert.equal(body.code, "missing_authorization");
  });

  it("wrong Bearer token returns 401", async () => {
    const env = createEnv();
    const request = createImageRequest({
      headers: { Authorization: "Bearer wrong-token" },
    });

    const response = await handleRequest(request, env);

    assert.equal(response.status, 401);
    const body = JSON.parse(await readBody(response));
    assert.equal(body.code, "invalid_authorization");
  });

  it("invalid Content-Type returns 400", async () => {
    const env = createEnv();
    const request = createImageRequest({
      headers: { "Content-Type": "text/plain" },
    });

    const response = await handleRequest(request, env);

    assert.equal(response.status, 400);
    const body = JSON.parse(await readBody(response));
    assert.equal(body.code, "invalid_content_type");
  });

  it("missing body returns 400 empty_body", async () => {
    const env = createEnv();
    const request = createImageRequest({ body: null });

    const response = await handleRequest(request, env);

    assert.equal(response.status, 400);
    const body = JSON.parse(await readBody(response));
    assert.equal(body.code, "empty_body");
  });

  it("Content-Length > 20MB returns 413 payload_too_large", async () => {
    const env = createEnv();
    const request = createImageRequest({
      headers: { "Content-Length": String(20 * 1024 * 1024 + 1) },
    });

    const response = await handleRequest(request, env);

    assert.equal(response.status, 413);
    const body = JSON.parse(await readBody(response));
    assert.equal(body.code, "payload_too_large");
  });

  it("valid request invokes IMAGES binding and returns 200 image/png", async () => {
    let inputCalled = false;
    let capturedTransform = null;
    let capturedOutput = null;

    const fakeBinding = {
      input(stream) {
        inputCalled = true;
        assert.ok(stream instanceof ReadableStream, "input should receive a ReadableStream (raw bytes, not Base64)");
        return {
          transform(opts) {
            capturedTransform = opts;
            return this;
          },
          output(opts) {
            capturedOutput = opts;
            return {
              response() {
                return new Response(PNG_BYTES, {
                  headers: { "Content-Type": "image/png" },
                });
              },
            };
          },
        };
      },
    };

    const env = createEnv({ imagesBinding: fakeBinding });
    const request = createImageRequest();

    const response = await handleRequest(request, env);

    assert.equal(response.status, 200);
    assert.equal(response.headers.get("Content-Type"), "image/png");
    assert.equal(response.headers.get("Cache-Control"), "no-store");
    assert.ok(inputCalled, "IMAGES.input should be called");
    assert.deepEqual(capturedTransform, { segment: "foreground" });
    assert.deepEqual(capturedOutput, { format: "image/png" });
  });

  it("raw source body reaches IMAGES.input without Base64 conversion", async () => {
    let capturedStream = null;

    const fakeBinding = {
      input(stream) {
        capturedStream = stream;
        return {
          transform() { return this; },
          output() {
            return {
              response() {
                return new Response(PNG_BYTES, {
                  headers: { "Content-Type": "image/png" },
                });
              },
            };
          },
        };
      },
    };

    const env = createEnv({ imagesBinding: fakeBinding });
    const request = createImageRequest();

    await handleRequest(request, env);

    assert.ok(capturedStream instanceof ReadableStream,
      "request.body (ReadableStream) should be passed directly to IMAGES.input");
  });

  it("provider/binding exception returns 502 provider_error without exception message", async () => {
    const internalMessage = "BindingInternalError: IMAGES_SECRET_DETAIL_12345";
    const fakeBinding = {
      input() {
        throw new Error(internalMessage);
      },
    };

    const env = createEnv({ imagesBinding: fakeBinding });
    const request = createImageRequest();

    const response = await handleRequest(request, env);

    assert.equal(response.status, 502);
    const bodyText = await readBody(response);
    const body = JSON.parse(bodyText);
    assert.equal(body.code, "provider_error");
    assert.ok(!bodyText.includes(internalMessage),
      "response must not contain the original exception message");
    assert.ok(!bodyText.includes("IMAGES_SECRET_DETAIL_12345"),
      "response must not contain binding internals");
  });

  it("service token is absent from all response bodies", async () => {
    const env = createEnv();
    const requests = [
      createImageRequest({ method: "GET" }),
      createImageRequest({ headers: { Authorization: "" } }),
      createImageRequest({ headers: { Authorization: "Bearer wrong" } }),
      createImageRequest({ headers: { "Content-Type": "text/plain" } }),
      createImageRequest({ body: null }),
    ];

    for (const req of requests) {
      const response = await handleRequest(req, env);
      const bodyText = await readBody(response);
      assert.ok(!bodyText.includes(VALID_TOKEN),
        `service token must not appear in response body: ${bodyText}`);
    }
  });

  it("binding 429 response is preserved as 429 provider_error without raw body", async () => {
    const rawProviderBody = '{"error":{"code":9422,"message":"transformation limit reached","cf-ray":"abc123"}}';
    const fakeBinding = {
      input() {
        return {
          transform() { return this; },
          output() {
            return {
              response() {
                return new Response(rawProviderBody, {
                  status: 429,
                  headers: { "Content-Type": "application/json" },
                });
              },
            };
          },
        };
      },
    };

    const env = createEnv({ imagesBinding: fakeBinding });
    const request = createImageRequest();

    const response = await handleRequest(request, env);

    assert.equal(response.status, 429);
    assert.equal(response.headers.get("Content-Type"), "application/json");
    const bodyText = await readBody(response);
    const body = JSON.parse(bodyText);
    assert.equal(body.code, "provider_error");
    // Raw provider body must not be forwarded
    assert.ok(!bodyText.includes("9422"),
      "response must not contain raw provider error code");
    assert.ok(!bodyText.includes("transformation limit"),
      "response must not contain raw provider error message");
    assert.ok(!bodyText.includes("cf-ray"),
      "response must not contain Cloudflare internal headers");
  });

  it("binding 503 response is preserved as 503 provider_error", async () => {
    const fakeBinding = {
      input() {
        return {
          transform() { return this; },
          output() {
            return {
              response() {
                return new Response("internal error", {
                  status: 503,
                  headers: { "Content-Type": "text/plain" },
                });
              },
            };
          },
        };
      },
    };

    const env = createEnv({ imagesBinding: fakeBinding });
    const request = createImageRequest();

    const response = await handleRequest(request, env);

    assert.equal(response.status, 503);
    assert.equal(response.headers.get("Content-Type"), "application/json");
    const body = JSON.parse(await readBody(response));
    assert.equal(body.code, "provider_error");
  });

  it("binding success preserves image/png bytes and 200 status", async () => {
    const fakeBinding = {
      input() {
        return {
          transform() { return this; },
          output() {
            return {
              response() {
                return new Response(PNG_BYTES, {
                  status: 200,
                  headers: { "Content-Type": "image/png" },
                });
              },
            };
          },
        };
      },
    };

    const env = createEnv({ imagesBinding: fakeBinding });
    const request = createImageRequest();

    const response = await handleRequest(request, env);

    assert.equal(response.status, 200);
    assert.equal(response.headers.get("Content-Type"), "image/png");
    assert.equal(response.headers.get("Cache-Control"), "no-store");
    const responseBytes = new Uint8Array(await response.arrayBuffer());
    assert.deepEqual(responseBytes, PNG_BYTES, "PNG bytes must be preserved");
  });

  it("binding failed response is not forced to image/png", async () => {
    const fakeBinding = {
      input() {
        return {
          transform() { return this; },
          output() {
            return {
              response() {
                return new Response("error", {
                  status: 500,
                  headers: { "Content-Type": "text/plain" },
                });
              },
            };
          },
        };
      },
    };

    const env = createEnv({ imagesBinding: fakeBinding });
    const request = createImageRequest();

    const response = await handleRequest(request, env);

    // Must NOT be 200 image/png — that would mask the provider failure
    assert.notEqual(response.status, 200);
    assert.notEqual(response.headers.get("Content-Type"), "image/png");
    assert.equal(response.headers.get("Content-Type"), "application/json");
  });

  it("missing Worker secret returns 503 service_not_configured", async () => {
    const env = createEnv({ token: "" });
    const request = createImageRequest();

    const response = await handleRequest(request, env);

    assert.equal(response.status, 503);
    const body = JSON.parse(await readBody(response));
    assert.equal(body.code, "service_not_configured");
    // Must not be treated as invalid caller auth
    assert.notEqual(body.code, "invalid_authorization");
    assert.notEqual(body.code, "missing_authorization");
  });

  it("undefined Worker secret returns 503 service_not_configured", async () => {
    const env = {
      IMAGES: createFakeImagesBinding(),
      LUNAR_FOREGROUND_ISOLATION_TOKEN: undefined,
    };
    const request = createImageRequest();

    const response = await handleRequest(request, env);

    assert.equal(response.status, 503);
    const body = JSON.parse(await readBody(response));
    assert.equal(body.code, "service_not_configured");
  });
});
