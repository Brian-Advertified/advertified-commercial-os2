import {
  useCallback,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import {
  CampaignFlowContext,
  unboundCampaignFlow,
  type CampaignFlowRegistration,
  type CampaignFlowResolution,
} from './campaign-flow-state'

export function CampaignFlowProvider({ routeKey, children }: {
  routeKey: string
  children: ReactNode
}) {
  const nextRegistrationId = useRef(0)
  const [registration, setRegistration] = useState<CampaignFlowRegistration | null>(null)
  const register = useCallback((registeredRouteKey: string, resolution: CampaignFlowResolution) => {
    const id = ++nextRegistrationId.current
    setRegistration({ id, routeKey: registeredRouteKey, resolution })
    return () => setRegistration(current => current?.id === id ? null : current)
  }, [])
  const resolution = registration?.routeKey === routeKey
    ? registration.resolution
    : unboundCampaignFlow
  const value = useMemo(
    () => ({ routeKey, resolution, register }),
    [routeKey, resolution, register],
  )
  return <CampaignFlowContext.Provider value={value}>{children}</CampaignFlowContext.Provider>
}
