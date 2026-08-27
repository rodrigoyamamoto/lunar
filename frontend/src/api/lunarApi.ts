import type {
  ApiErrorResponse,
  ArtifactSummary,
  CreateAssetRequest,
  CreateAssetResponse,
  GenerationRequest,
  GenerationResponse,
} from './contracts'

export class LunarApiError {
  readonly code: string
  readonly message: string
  readonly retryAfterSeconds?: number | null

  constructor(code: string, message: string, retryAfterSeconds?: number | null) {
    this.code = code
    this.message = message
    this.retryAfterSeconds = retryAfterSeconds
  }
}

async function parseErrorResponse(response: Response): Promise<LunarApiError> {
  try {
    const body = (await response.json()) as ApiErrorResponse
    return new LunarApiError(body.code, body.message, body.retryAfterSeconds)
  } catch {
    return new LunarApiError('network_error', 'Something went wrong while contacting Lunar.')
  }
}

export async function createAsset(request: CreateAssetRequest): Promise<CreateAssetResponse> {
  const response = await fetch('/api/assets', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw await parseErrorResponse(response)
  }

  return (await response.json()) as CreateAssetResponse
}

export async function generateArtifact(request: GenerationRequest): Promise<GenerationResponse> {
  const response = await fetch('/api/generations', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw await parseErrorResponse(response)
  }

  return (await response.json()) as GenerationResponse
}

export async function listAssetArtifacts(assetId: string): Promise<ArtifactSummary[]> {
  const response = await fetch(`/api/assets/${assetId}/artifacts`)

  if (!response.ok) {
    throw await parseErrorResponse(response)
  }

  return (await response.json()) as ArtifactSummary[]
}
