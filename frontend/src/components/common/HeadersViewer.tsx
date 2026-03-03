interface Props {
  headersJson: string
}

/** Parses a JSON-serialized header dictionary and renders it as a table. */
export default function HeadersViewer({ headersJson }: Props) {
  let entries: [string, string][] = []
  try {
    const parsed = JSON.parse(headersJson) as Record<string, string | string[]>
    entries = Object.entries(parsed).map(([k, v]) => [
      k,
      Array.isArray(v) ? v.join(', ') : v,
    ])
  } catch {
    return <pre className="headers-raw monospace">{headersJson}</pre>
  }

  if (entries.length === 0) {
    return <p className="empty-note">No headers</p>
  }

  return (
    <table className="headers-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Value</th>
        </tr>
      </thead>
      <tbody>
        {entries.map(([name, value]) => (
          <tr key={name}>
            <td className="header-name monospace">{name}</td>
            <td className="header-value monospace">{value}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
