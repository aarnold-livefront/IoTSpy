import '../../styles/layout.css'

interface Props {
  message: string
}

export default function ErrorBanner({ message }: Props) {
  return (
    <div className="error-banner" role="alert">
      <span className="error-banner__message">{message}</span>
    </div>
  )
}
