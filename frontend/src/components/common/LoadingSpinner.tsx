import '../../styles/layout.css'

interface Props {
  fullPage?: boolean
}

export default function LoadingSpinner({ fullPage }: Props) {
  return (
    <div className={`loading-spinner${fullPage ? ' loading-spinner--full-page' : ''}`}>
      <div className="spinner" />
    </div>
  )
}
