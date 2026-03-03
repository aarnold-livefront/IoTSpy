import type { ReactNode } from 'react'
import '../../styles/layout.css'

interface Props {
  header: ReactNode
  children: ReactNode
}

export default function AppShell({ header, children }: Props) {
  return (
    <div className="app-shell">
      {header}
      <main className="app-main">{children}</main>
    </div>
  )
}
