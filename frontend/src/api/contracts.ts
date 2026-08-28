export type AssetType = 'Character' | 'Weapon' | 'Environment' | 'Prop'

export interface CreateAssetRequest {
  name: string
  assetType: AssetType
}

export interface CreateAssetResponse {
  assetId: string
  name: string
  assetType: string
}

export interface GenerationRequest {
  assetId: string
  prompt: string
}

export interface GenerationResponse {
  workflowExecutionId: string
  artifactId: string
  assetId: string
  artifactName: string
  artifactType: string
  mediaType: string
  contentUrl: string
}

export interface GenerationInput {
  workflowExecutionId: string
  prompt: string
}

export interface ArtifactSummary {
  artifactId: string
  assetId: string
  artifactName: string
  artifactType: string
  createdAt: string
  contentUrl: string
  generationInput: GenerationInput | null
  sourceArtifactIds: string[]
}

export interface ArtifactTransformationResponse {
  workflowExecutionId: string
  artifactId: string
  assetId: string
  artifactName: string
  artifactType: string
  mediaType: string
  contentUrl: string
  sourceArtifactIds: string[]
}

export interface ApiErrorResponse {
  code: string
  message: string
  retryAfterSeconds?: number | null
}
