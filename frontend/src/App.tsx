import { useCallback, useEffect, useRef, useState } from 'react'
import type { AssetType, GenerationResponse } from './api/contracts'
import { createAsset, generateArtifact, LunarApiError } from './api/lunarApi'
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

  const assetIdRef = useRef<string | null>(null)
  const lastAssetNameRef = useRef<string>('')
  const lastAssetTypeRef = useRef<AssetType>('Environment')
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

  const handleAssetNameChange = (value: string) => {
    setAssetName(value)
    if (assetIdRef.current !== null) {
      assetIdRef.current = null
    }
  }

  const handleAssetTypeChange = (value: AssetType) => {
    setAssetType(value)
    if (assetIdRef.current !== null) {
      assetIdRef.current = null
    }
  }

  const handleGenerate = async () => {
    if (!assetName.trim() || !prompt.trim()) {
      return
    }

    setErrorMessage('')
    setGeneration(null)

    try {
      if (
        assetIdRef.current === null ||
        lastAssetNameRef.current !== assetName ||
        lastAssetTypeRef.current !== assetType
      ) {
        setUiState('creating')
        const asset = await createAsset({ name: assetName, assetType })
        assetIdRef.current = asset.assetId
        lastAssetNameRef.current = assetName
        lastAssetTypeRef.current = assetType
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
    if (!generation) return

    try {
      const response = await fetch(generation.contentUrl)
      if (!response.ok) {
        setErrorMessage('Could not download the generated file.')
        return
      }

      const blob = await response.blob()
      const objectUrl = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = objectUrl
      anchor.download = generation.artifactName
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
    assetIdRef.current = null
    lastAssetNameRef.current = ''
    lastAssetTypeRef.current = 'Environment'
  }

  const isBusy = uiState === 'creating' || uiState === 'generating'
  const canGenerate = assetName.trim() !== '' && prompt.trim() !== '' && !isBusy

  return (
    <div className="lunar-app">
      <header className="lunar-header">
        <h1>Lunar Asset Studio</h1>
      </header>

      <main className="lunar-main">
        <section className="lunar-form-section">
          <div className="lunar-field">
            <label htmlFor="asset-name">Asset name</label>
            <input
              id="asset-name"
              type="text"
              value={assetName}
              onChange={(e) => handleAssetNameChange(e.target.value)}
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
              onChange={(e) => handleAssetTypeChange(e.target.value as AssetType)}
              disabled={isBusy}
            >
              {assetTypeOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

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
            {uiState === 'completed' && (
              <button type="button" className="lunar-button-secondary" onClick={handleReset}>
                New asset
              </button>
            )}
          </div>
        </section>

        {uiState === 'generating' && (
          <section className="lunar-status-section">
            <div className="lunar-spinner" aria-hidden="true" />
            <span className="lunar-status-text">
              Generating…{elapsedSeconds > 0 ? ` ${elapsedSeconds}s` : ''}
            </span>
          </section>
        )}

        {uiState === 'failed' && (
          <section className="lunar-error-section">
            <p className="lunar-error-message">{errorMessage}</p>
            <button type="button" className="lunar-button-secondary" onClick={handleGenerate}>
              Try again
            </button>
          </section>
        )}

        {uiState === 'completed' && generation && (
          <section className="lunar-result-section">
            <div className="lunar-preview">
              <img
                src={generation.contentUrl}
                alt={generation.artifactName}
                className="lunar-preview-image"
              />
            </div>
            <div className="lunar-result-meta">
              <span className="lunar-artifact-name">{generation.artifactName}</span>
              <button type="button" className="lunar-button-secondary" onClick={handleDownload}>
                Download
              </button>
            </div>
          </section>
        )}
      </main>
    </div>
  )
}
