import { useEffect, useMemo, useState } from "react"
import { LiquidButton } from "@/components/ui/liquid-glass-button"
import { ItemCard } from "@/components/item-card"
import { OverviewChart } from "@/components/overview-chart"

function humanSize(bytes: number): string {
  if (bytes == null || Number.isNaN(bytes)) return "0 o"
  let size = bytes
  for (const unit of ["o", "Ko", "Mo", "Go", "To"]) {
    if (size < 1024) return `${size.toFixed(2)} ${unit}`
    size /= 1024
  }
  return `${size.toFixed(2)} Po`
}

type View = "list" | "overview"

export default function App() {
  const [drives, setDrives] = useState<DriveInfo[]>([])
  const [letter, setLetter] = useState<string>("")
  const [rules, setRules] = useState<Rule[]>([])
  const [checked, setChecked] = useState<Record<string, boolean>>({})
  const [view, setView] = useState<View>("list")
  const [overview, setOverview] = useState<OverviewBucket[]>([])
  const [scanning, setScanning] = useState(false)
  const [overviewLoading, setOverviewLoading] = useState(false)
  const [cleaning, setCleaning] = useState(false)
  const [status, setStatus] = useState("Pret.")

  useEffect(() => {
    window.api.listDrives().then((list) => {
      setDrives(list)
      if (list.length) setLetter(list[0].letter)
    })
  }, [])

  const currentDrive = useMemo(() => drives.find((d) => d.letter === letter), [drives, letter])

  async function onScan() {
    if (!letter) return
    setView("list")
    setScanning(true)
    setStatus(`Analyse du disque ${letter}: en cours...`)
    setRules([])
    const result = await window.api.scanDrive(letter)
    setRules(result)
    setChecked(Object.fromEntries(result.map((r) => [r.key, r.risk === "SUR"])))
    setStatus(
      result.length
        ? "Analyse terminee. Coche les elements a nettoyer puis clique sur Nettoyer."
        : "Rien a nettoyer sur ce disque."
    )
    setScanning(false)
  }

  async function onOverview() {
    if (!letter) return
    setView("overview")
    setOverviewLoading(true)
    setStatus(`Analyse du disque ${letter}: en cours (vue d'ensemble)...`)
    const buckets = await window.api.getOverview(letter)
    setOverview(buckets)
    setStatus(buckets.length ? "Vue d'ensemble terminee." : "Rien a afficher sur ce disque.")
    setOverviewLoading(false)
  }

  function onSelectSafe() {
    setChecked(Object.fromEntries(rules.map((r) => [r.key, r.risk === "SUR"])))
  }

  async function onClean() {
    const selected = rules.filter((r) => checked[r.key])
    if (!selected.length) return
    const total = selected.reduce((sum, r) => sum + r.size, 0)
    const ok = await window.api.confirm(
      `Supprimer ${selected.length} element(s) pour liberer environ ${humanSize(total)} ?`,
      "Les elements en cache/temp se reconstruisent automatiquement."
    )
    if (!ok) return

    setCleaning(true)
    setStatus("Nettoyage en cours...")
    const freed = await window.api.cleanItems(
      selected.map((r) => r.key),
      letter
    )
    setStatus(`Nettoyage termine. Environ ${humanSize(freed)} liberes.`)
    setCleaning(false)

    const list = await window.api.listDrives()
    setDrives(list)
    onScan()
  }

  const total = rules.reduce((sum, r) => sum + r.size, 0)
  const usageFrac = currentDrive && currentDrive.total ? currentDrive.used / currentDrive.total : 0

  return (
    <div className="flex h-screen flex-col bg-background text-foreground">
      <header className="border-b border-white/10 bg-white/5 px-5 py-4 backdrop-blur-2xl">
        <div className="mb-3 flex items-center justify-between">
          <h1 className="text-xl font-bold">Clean</h1>
          {currentDrive && (
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <div className="h-2 w-44 overflow-hidden rounded-full bg-muted">
                <div className="h-full bg-primary" style={{ width: `${usageFrac * 100}%` }} />
              </div>
              <span>
                {humanSize(currentDrive.free)} libres sur {humanSize(currentDrive.total)}
              </span>
            </div>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <select
            value={letter}
            onChange={(e) => setLetter(e.target.value)}
            className="h-9 rounded-md border border-white/15 bg-white/5 px-3 text-sm backdrop-blur"
          >
            {drives.map((d) => (
              <option key={d.letter} value={d.letter}>
                {d.letter}:   {humanSize(d.free)} libre / {humanSize(d.total)}
              </option>
            ))}
          </select>

          <LiquidButton size="sm" variant="default" disabled={scanning} onClick={onScan}>
            Scanner
          </LiquidButton>

          <LiquidButton size="sm" variant="outline" disabled={overviewLoading} onClick={onOverview}>
            Vue d'ensemble
          </LiquidButton>

          <LiquidButton size="sm" variant="outline" disabled={!rules.length} onClick={onSelectSafe}>
            Selectionner le sur
          </LiquidButton>

          <LiquidButton
            size="sm"
            variant="destructive"
            disabled={!rules.length || cleaning}
            onClick={onClean}
            className="ml-auto"
          >
            Nettoyer la selection
          </LiquidButton>
        </div>
      </header>

      <main className="flex-1 overflow-y-auto px-5 py-4">
        {view === "list" ? (
          rules.length ? (
            <div className="flex flex-col gap-2">
              {rules.map((r) => (
                <ItemCard
                  key={r.key}
                  rule={r}
                  checked={!!checked[r.key]}
                  onToggle={() => setChecked((c) => ({ ...c, [r.key]: !c[r.key] }))}
                  onHover={() => setStatus(`${r.description}${r.path ? `  (${r.path})` : ""}`)}
                />
              ))}
            </div>
          ) : (
            <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
              {scanning ? "Analyse..." : "Choisis un disque puis clique sur Scanner."}
            </div>
          )
        ) : (
          <div className="flex flex-col gap-3">
            <div className="rounded-lg border border-white/10 bg-white/5 p-2 backdrop-blur-xl">
              <OverviewChart buckets={overview} />
            </div>
            <div className="flex flex-col gap-1">
              {overview.map((b) => (
                <div
                  key={b.label}
                  className="flex items-center justify-between rounded-md border border-white/10 bg-white/5 px-3 py-1.5 text-sm backdrop-blur-xl"
                >
                  <span className="truncate">{b.label}</span>
                  <span className="font-bold">{humanSize(b.size)}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </main>

      <footer className="flex items-center justify-between border-t border-white/10 bg-white/5 px-5 py-2 text-xs text-muted-foreground backdrop-blur-2xl">
        <span>{status}</span>
        <span className="font-bold text-foreground">
          {rules.length ? `${rules.length} elements — ${humanSize(total)} au total` : ""}
        </span>
      </footer>
    </div>
  )
}
