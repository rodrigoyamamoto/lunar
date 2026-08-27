import { LunarApiError } from '../api/lunarApi'

const errorMessages: Record<string, (error: LunarApiError) => string> = {
  rate_limited: (error) =>
    error.retryAfterSeconds
      ? `Generation is temporarily rate limited. Try again in ${error.retryAfterSeconds} seconds.`
      : 'Generation is temporarily rate limited. Please try again shortly.',
  quota_exhausted: () =>
    'Generation is temporarily unavailable because the generation service quota has been reached.',
  timed_out: () => 'Generation took too long. Please try again.',
  invalid_response: () => 'The generation service returned an invalid result. Please try again.',
  temporarily_unavailable: () => 'Generation is temporarily unavailable. Please try again shortly.',
  remote_outcome_unknown: () =>
    'The generation service returned an uncertain result. Please try again.',
  provider_authentication_failed: () =>
    'Generation is temporarily unavailable due to a service configuration issue.',
  provider_access_denied: () =>
    'Generation is temporarily unavailable due to a service access issue.',
  asset_not_found: () => 'The selected asset could not be found.',
  workflow_definition_not_found: () =>
    'The generation workflow is not properly configured.',
  workflow_step_not_found: () =>
    'The generation workflow is not properly configured.',
  artifact_content_persistence_failed: () =>
    'Generation completed but the result could not be stored. Please try again.',
  artifact_persistence_failed: () =>
    'Generation completed but the result could not be stored. Please try again.',
  workflow_execution_persistence_failed: () =>
    'Generation could not be started due to a persistence issue. Please try again.',
  asset_persistence_failed: () =>
    'The asset could not be created. Please try again.',
  invalid_asset_id: () => 'A valid asset must be selected before generating.',
  invalid_name: () => 'Asset name cannot be empty.',
  invalid_prompt: () => 'Prompt cannot be empty.',
  network_error: () => 'Something went wrong while contacting Lunar.',
}

export function mapErrorToMessage(error: LunarApiError): string {
  const mapper = errorMessages[error.code]
  if (mapper) {
    return mapper(error)
  }
  return error.message || 'Something went wrong while contacting Lunar.'
}
