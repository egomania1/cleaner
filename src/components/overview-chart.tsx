import { useEffect, useRef } from "react"

function humanSize(bytes: number): string {
  if (bytes == null || Number.isNaN(bytes)) return "0 o"
  let size = bytes
  for (const unit of ["o", "Ko", "Mo", "Go", "To"]) {
    if (size < 1024) return `${size.toFixed(2)} ${unit}`
    size /= 1024
  }
  return `${size.toFixed(2)} Po`
}

const SEQ_LIGHT_LOW = "#cde2fb"
const SEQ_LIGHT_HIGH = "#0d366b"
const SEQ_DARK_LOW = "#104281"
const SEQ_DARK_HIGH = "#9ec5f4"

function hexToRgb(hex: string): [number, number, number] {
  const h = hex.replace("#", "")
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)]
}

function rgbToHex([r, g, b]: number[]): string {
  const c = (v: number) => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, "0")
  return `#${c(r)}${c(g)}${c(b)}`
}

function lerpColor(hex1: string, hex2: string, t: number): string {
  t = Math.max(0, Math.min(1, t))
  const a = hexToRgb(hex1)
  const b = hexToRgb(hex2)
  return rgbToHex(a.map((v, i) => v + (b[i] - v) * t))
}

function sequentialColor(t: number, dark: boolean): string {
  return dark ? lerpColor(SEQ_DARK_LOW, SEQ_DARK_HIGH, t) : lerpColor(SEQ_LIGHT_LOW, SEQ_LIGHT_HIGH, t)
}

function polarToXy(cx: number, cy: number, radius: number, bearingDeg: number): [number, number] {
  const rad = (bearingDeg * Math.PI) / 180
  return [cx + radius * Math.sin(rad), cy - radius * Math.cos(rad)]
}

const CHART_CHROME = {
  dark: { surface: "#1a1a19", primary: "#ffffff", muted: "#898781", grid: "#2c2c2a" },
  light: { surface: "#fcfcfb", primary: "#0b0b0b", muted: "#898781", grid: "#e1e0d9" },
}

export function OverviewChart({ buckets }: { buckets: OverviewBucket[] }) {
  const canvasRef = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const draw = () => {
      const parent = canvas.parentElement
      const w = Math.max(parent?.clientWidth ?? 640, 640)
      const h = Math.max(canvas.clientHeight || 380, 380)
      canvas.width = w
      canvas.height = h

      const ctx = canvas.getContext("2d")
      if (!ctx) return

      const dark = document.documentElement.classList.contains("dark")
      const chrome = dark ? CHART_CHROME.dark : CHART_CHROME.light

      ctx.clearRect(0, 0, w, h)

      if (!buckets.length) {
        ctx.fillStyle = chrome.muted
        ctx.font = "12px Segoe UI"
        ctx.textAlign = "center"
        ctx.fillText("Clique sur \"Vue d'ensemble\" pour analyser le disque.", w / 2, h / 2)
        return
      }

      const cx = w / 2
      const cy = h / 2 + 6
      const rMax = Math.max(60, Math.min(w, h) / 2 - 90)
      const rMin = rMax * 0.16
      const maxSize = Math.max(...buckets.map((b) => b.size))
      const n = buckets.length
      const stepDeg = 360 / n
      const gapDeg = Math.min(3, stepDeg * 0.12)

      ctx.strokeStyle = chrome.grid
      ctx.lineWidth = 1
      for (const frac of [0.33, 0.66, 1.0]) {
        ctx.beginPath()
        ctx.arc(cx, cy, rMax * frac, 0, Math.PI * 2)
        ctx.stroke()
      }

      buckets.forEach((b, i) => {
        const start = i * stepDeg
        const end = start + stepDeg
        const t = maxSize ? b.size / maxSize : 0
        const radius = rMin + (rMax - rMin) * Math.sqrt(t)
        const color = sequentialColor(t, dark)

        const segStart = start + gapDeg / 2
        const segEnd = end - gapDeg / 2
        ctx.beginPath()
        ctx.moveTo(cx, cy)
        for (let k = 0; k <= 8; k++) {
          const bearing = segStart + ((segEnd - segStart) * k) / 8
          const [x, y] = polarToXy(cx, cy, radius, bearing)
          ctx.lineTo(x, y)
        }
        ctx.closePath()
        ctx.fillStyle = color
        ctx.fill()

        const [sx, sy] = polarToXy(cx, cy, rMax, start)
        ctx.beginPath()
        ctx.moveTo(cx, cy)
        ctx.lineTo(sx, sy)
        ctx.strokeStyle = chrome.grid
        ctx.stroke()

        const mid = start + stepDeg / 2
        const [lx, ly] = polarToXy(cx, cy, rMax + 24, mid)
        let align: CanvasTextAlign = "center"
        if (mid < 8 || mid > 352 || Math.abs(mid - 180) < 8) align = "center"
        else if (mid < 180) align = "left"
        else align = "right"

        let label = b.label
        if (label.length > 22) label = label.slice(0, 21) + "…"

        ctx.fillStyle = chrome.primary
        ctx.font = "10px Segoe UI"
        ctx.textAlign = align
        ctx.fillText(label, lx, ly)
        ctx.fillText(humanSize(b.size), lx, ly + 12)
      })

      const legX0 = 20
      const legY0 = h - 34
      const legW = 140
      const legH = 10
      const steps = 24
      for (let k = 0; k < steps; k++) {
        ctx.fillStyle = sequentialColor(k / steps, dark)
        ctx.fillRect(legX0 + (legW * k) / steps, legY0, legW / steps + 1, legH)
      }
      ctx.fillStyle = chrome.muted
      ctx.font = "9px Segoe UI"
      ctx.textAlign = "left"
      ctx.fillText("Petit", legX0, legY0 - 10)
      ctx.textAlign = "right"
      ctx.fillText("Grand", legX0 + legW, legY0 - 10)
    }

    draw()
    const observer = new ResizeObserver(draw)
    if (canvas.parentElement) observer.observe(canvas.parentElement)
    return () => observer.disconnect()
  }, [buckets])

  return <canvas ref={canvasRef} className="h-[380px] w-full rounded-lg" />
}
