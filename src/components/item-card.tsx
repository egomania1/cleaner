import { cn } from "@/lib/utils"

function humanSize(bytes: number): string {
  if (bytes == null || Number.isNaN(bytes)) return "0 o"
  let size = bytes
  for (const unit of ["o", "Ko", "Mo", "Go", "To"]) {
    if (size < 1024) return `${size.toFixed(2)} ${unit}`
    size /= 1024
  }
  return `${size.toFixed(2)} Po`
}

const RISK_BADGE: Record<Rule["risk"], string> = {
  SUR: "bg-emerald-100 text-emerald-800 dark:bg-emerald-500/15 dark:text-emerald-400",
  "A VERIFIER": "bg-amber-100 text-amber-800 dark:bg-amber-500/15 dark:text-amber-400",
}

export function ItemCard({
  rule,
  checked,
  onToggle,
  onHover,
}: {
  rule: Rule
  checked: boolean
  onToggle: () => void
  onHover: () => void
}) {
  return (
    <div
      className="flex items-center gap-3 rounded-lg border border-white/10 bg-white/5 px-4 py-3 backdrop-blur-xl transition-colors cursor-pointer hover:bg-white/10"
      onMouseEnter={onHover}
      onClick={onToggle}
    >
      <input
        type="checkbox"
        checked={checked}
        onChange={onToggle}
        onClick={(e) => e.stopPropagation()}
        className="h-5 w-5 accent-primary shrink-0"
      />
      <div className="min-w-0 flex-1">
        <div className="text-sm font-semibold truncate">{rule.label}</div>
        <div className="text-xs text-muted-foreground truncate">
          {rule.locked && rule.lockedProcess ? (
            <span>Ferme {rule.lockedProcess} pour un nettoyage complet — {rule.description}</span>
          ) : (
            rule.description
          )}
        </div>
      </div>
      <span className={cn("rounded-full px-2.5 py-1 text-xs font-bold shrink-0", RISK_BADGE[rule.risk])}>
        {rule.risk}
      </span>
      <span className="w-24 shrink-0 text-right text-sm font-bold">{humanSize(rule.size)}</span>
    </div>
  )
}
