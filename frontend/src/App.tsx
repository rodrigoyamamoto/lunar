import { useCallback, useEffect, useRef, useState } from 'react'
import type { ArtifactSummary, AssetType, GenerationResponse } from './api/contracts'
import {
  createAsset,
  generateArtifact,
  listAssetArtifacts,
  LunarApiError,
} from './api/lunarApi'
import { mapErrorToMessage } from './api/errorMapping'
import './App.css'

type UiState = 'idle' | 'creating' | 'generating' | 'completed' | 'failed'

const assetTypeOptions: { value: AssetType; label: string }[] = [
  { value: 'Character', label: 'Character' },
  { value: 'Weapon', label: 'Weapon' },
  { value: 'Environment', label: 'Environment' },
  { value: 'Prop', label: 'Prop' },
]

export default function App() {
  const [assetName, setAssetName] = useState('')
  const [assetType, setAssetType] = useState<AssetType>('Environment')
  const [prompt, setPrompt] = useState('')
  const [uiState, setUiState] = useState<UiState>('idle')
  const [errorMessage, setErrorMessage] = useState('')
  const [generation, setGeneration] = useState<GenerationResponse | null>(null)
  const [elapsedSeconds, setElapsedSeconds] = useState(0)
  const [artifacts, setArtifacts] = useState<ArtifactSummary[]>([])
  const [selectedArtifact, setSelectedArtifact] = useState<ArtifactSummary | null>(null)
  const [galleryError, setGalleryError] = useState('')
  const [assetId, setAssetId] = useState<string | null>(null)

  const assetIdRef = useRef<string | null>(null)
  const elapsedTimerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const clearElapsedTimer = useCallback(() => {
    if (elapsedTimerRef.current !== null) {
      clearInterval(elapsedTimerRef.current)
      elapsedTimerRef.current = null
    }
  }, [])

  const startElapsedTimer = useCallback(() => {
    clearElapsedTimer()
    setElapsedSeconds(0)
    elapsedTimerRef.current = setInterval(() => {
      setElapsedSeconds((prev) => prev + 1)
    }, 1000)
  }, [clearElapsedTimer])

  useEffect(() => {
    return () => {
      clearElapsedTimer()
    }
  }, [clearElapsedTimer])

  const refreshGallery = useCallback(async (assetId: string) => {
    try {
      const list = await listAssetArtifacts(assetId)
      setArtifacts(list)
      setGalleryError('')

      if (list.length > 0) {
        setSelectedArtifact(list[0])
      }
    } catch (error) {
      if (error instanceof LunarApiError) {
        setGalleryError(mapErrorToMessage(error))
      } else {
        setGalleryError('Could not load generated outputs.')
      }
    }
  }, [])

  const handleGenerate = async () => {
    if (!assetIdRef.current && !assetName.trim()) {
      return
    }

    if (!prompt.trim()) {
      return
    }

    setErrorMessage('')
    setGeneration(null)

    try {
      if (assetIdRef.current === null) {
        setUiState('creating')
        const asset = await createAsset({ name: assetName, assetType })
        assetIdRef.current = asset.assetId
        setAssetId(asset.assetId)
      }

      setUiState('generating')
      startElapsedTimer()

      const result = await generateArtifact({
        assetId: assetIdRef.current!,
        prompt,
      })

      clearElapsedTimer()
      setGeneration(result)
      setUiState('completed')

      await refreshGallery(assetIdRef.current!)
    } catch (error) {
      clearElapsedTimer()
      setUiState('failed')
      if (error instanceof LunarApiError) {
        setErrorMessage(mapErrorToMessage(error))
      } else {
        setErrorMessage('Something went wrong while contacting Lunar.')
      }
    }
  }

  const handleDownload = async () => {
    const target = selectedArtifact
    if (!target) return

    try {
      const response = await fetch(target.contentUrl)
      if (!response.ok) {
        setErrorMessage('Could not download the generated file.')
        return
      }

      const blob = await response.blob()
      const objectUrl = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = objectUrl
      anchor.download = target.artifactName
      document.body.appendChild(anchor)
      anchor.click()
      document.body.removeChild(anchor)
      URL.revokeObjectURL(objectUrl)
    } catch {
      setErrorMessage('Could not download the generated file.')
    }
  }

  const handleReset = () => {
    setAssetName('')
    setAssetType('Environment')
    setPrompt('')
    setUiState('idle')
    setErrorMessage('')
    setGeneration(null)
    setElapsedSeconds(0)
    setArtifacts([])
    setSelectedArtifact(null)
    setGalleryError('')
    assetIdRef.current = null
    setAssetId(null)
  }

  const handleSelectArtifact = (artifact: ArtifactSummary) => {
    setSelectedArtifact(artifact)
  }

  const isBusy = uiState === 'creating' || uiState === 'generating'
  const hasAsset = assetId !== null
  const canGenerate =
    (!hasAsset ? assetName.trim() !== '' : true) &&
    prompt.trim() !== '' &&
    !isBusy
  const previewTarget = selectedArtifact
  const previewName = previewTarget?.artifactName ?? generation?.artifactName ?? ''
  const previewUrl = previewTarget?.contentUrl ?? generation?.contentUrl ?? ''

  return (
    <div className="lunar-app">
      <header className="lunar-header">
        <h1>Lunar Asset Studio</h1>
      </header>

      <main className="lunar-main">
        <div className="lunar-workspace">
          <section className="lunar-sidebar">
            <div className="lunar-asset-identity">
              <h2>{hasAsset ? assetName : 'New Asset'}</h2>
              {hasAsset && <span className="lunar-asset-type-badge">{assetType}</span>}
            </div>

            {hasAsset ? (
              <div className="lunar-asset-readonly">
                <div className="lunar-field">
                  <label htmlFor="asset-name">Asset name</label>
                  <input
                    id="asset-name"
                    type="text"
                    value={assetName}
                    readOnly
                    aria-readonly="true"
                    autoComplete="off"
                  />
                </div>

                <div className="lunar-field">
                  <label htmlFor="asset-type">Asset type</label>
                  <input
                    id="asset-type"
                    type="text"
                    value={assetType}
                    readOnly
                    aria-readonly="true"
                  />
                </div>
              </div>
            ) : (
              <>
                <div className="lunar-field">
                  <label htmlFor="asset-name">Asset name</label>
                  <input
                    id="asset-name"
                    type="text"
                    value={assetName}
                    onChange={(e) => setAssetName(e.target.value)}
                    placeholder="Ruined Gothic Watchtower"
                    disabled={isBusy}
                    autoComplete="off"
                  />
                </div>

                <div className="lunar-field">
                  <label htmlFor="asset-type">Asset type</label>
                  <select
                    id="asset-type"
                    value={assetType}
                    onChange={(e) => setAssetType(e.target.value as AssetType)}
                    disabled={isBusy}
                  >
                    {assetTypeOptions.map((opt) => (
                      <option key={opt.value} value={opt.value}>
                        {opt.label}
                      </option>
                    ))}
                  </select>
                </div>
              </>
            )}

            <div className="lunar-field">
              <label htmlFor="prompt">Describe what you want</label>
              <textarea
                id="prompt"
                value={prompt}
                onChange={(e) => setPrompt(e.target.value)}
                placeholder="A ruined gothic watchtower under a blood-red eclipse, wet stone, ominous atmosphere..."
                rows={4}
                disabled={isBusy}
              />
            </div>

            <div className="lunar-actions">
              <button
                type="button"
                className="lunar-button-primary"
                onClick={handleGenerate}
                disabled={!canGenerate}
              >
                {uiState === 'creating' ? 'Preparing…' : 'Generate'}
              </button>
              {hasAsset && (
                <button
                  type="button"
                  className="lunar-button-secondary"
                  onClick={handleReset}
                  disabled={isBusy}
                >
                  New asset
                </button>
              )}
            </div>

            {uiState === 'generating' && (
              <div className="lunar-status-section">
                <div className="lunar-spinner" aria-hidden="true" />
                <span className="lunar-status-text">
                  Generating…{elapsedSeconds > 0 ? ` ${elapsedSeconds}s` : ''}
                </span>
              </div>
            )}

            {uiState === 'failed' && (
              <div className="lunar-error-section">
                <p className="lunar-error-message">{errorMessage}</p>
                <button
                  type="button"
                  className="lunar-button-secondary"
                  onClick={handleGenerate}
                >
                  Try again
                </button>
              </div>
            )}
          </section>

          <section className="lunar-preview-section" aria-label="Selected output">
            {previewUrl ? (
              <>
                <div className="lunar-preview">
                  <img
                    src={previewUrl}
                    alt={previewName}
                    className="lunar-preview-image"
                  />
                </div>
                <div className="lunar-result-meta">
                  <span className="lunar-artifact-name">{previewName}</span>
                  <button
                    type="button"
                    className="lunar-button-secondary"
                    onClick={handleDownload}
                    disabled={!previewTarget}
                  >
                    Download
                  </button>
                </div>
              </>
            ) : (
              <div className="lunar-preview-placeholder">
                <p>Generated outputs will appear here.</p>
              </div>
            )}
          </section>
        </div>

        {galleryError && (
          <section className="lunar-gallery-error">
            <p className="lunar-error-message">{galleryError}</p>
          </section>
        )}

        {artifacts.length > 0 && (
          <section className="lunar-gallery-section" aria-label="Generated outputs">
            <h3 className="lunar-gallery-title">Generated outputs</h3>
            <div className="lunar-gallery">
              {artifacts.map((artifact) => (
                <button
                  key={artifact.artifactId}
                  type="button"
                  className={
                    'lunar-gallery-item' +
                    (selectedArtifact?.artifactId === artifact.artifactId
                      ? ' lunar-gallery-item-selected'
                      : '')
                  }
                  onClick={() => handleSelectArtifact(artifact)}
                  aria-pressed={selectedArtifact?.artifactId === artifact.artifactId}
                  aria-label={`Select ${artifact.artifactName}`}
                >
                  <img
                    src={artifact.contentUrl}
                    alt={artifact.artifactName}
                    className="lunar-gallery-thumbnail"
                  />
                  <span className="lunar-gallery-item-name">{artifact.artifactName}</span>
                </button>
              ))}
            </div>
          </section>
        )}
      </main>
    </div>
  )
}
