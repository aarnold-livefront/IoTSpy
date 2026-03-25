import { lazy, Suspense, useState } from 'react'
import { Route, Routes } from 'react-router-dom'
import { useAuthInit } from './hooks/useAuth'
import LoadingSpinner from './components/common/LoadingSpinner'
import OnboardingWizard from './components/onboarding/OnboardingWizard'

const SetupPage = lazy(() => import('./pages/SetupPage'))
const LoginPage = lazy(() => import('./pages/LoginPage'))
const DashboardPage = lazy(() => import('./pages/DashboardPage'))

function AppRoutes() {
  const { status } = useAuthInit()
  const [showOnboarding, setShowOnboarding] = useState(
    () => !localStorage.getItem('iotspy-onboarding-complete')
  )

  if (status === 'unknown') {
    return <LoadingSpinner fullPage />
  }

  return (
    <>
      <Routes>
        <Route path="/setup" element={<SetupPage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/*" element={<DashboardPage />} />
      </Routes>
      {status === 'authenticated' && showOnboarding && (
        <OnboardingWizard onComplete={() => setShowOnboarding(false)} />
      )}
    </>
  )
}

export default function App() {
  return (
    <Suspense fallback={<LoadingSpinner fullPage />}>
      <AppRoutes />
    </Suspense>
  )
}
