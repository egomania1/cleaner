import * as fs from "node:fs"
import * as os from "node:os"
import * as path from "node:path"
import { execFile } from "node:child_process"
import { promisify } from "node:util"

const execFileAsync = promisify(execFile)

export const RISK_SAFE = "SUR"
export const RISK_REVIEW = "A VERIFIER"

export interface Rule {
  key: string
  label: string
  risk: typeof RISK_SAFE | typeof RISK_REVIEW
  description: string
  path: string | null
  lockedProcess?: string
  isRecycleBin?: boolean
  size: number
}

export interface DriveInfo {
  letter: string
  total: number
  free: number
  used: number
}

export interface OverviewBucket {
  label: string
  size: number
}

export function humanSize(bytes: number): string {
  if (bytes == null || Number.isNaN(bytes)) return "0 o"
  let size = bytes
  for (const unit of ["o", "Ko", "Mo", "Go", "To"]) {
    if (size < 1024) return `${size.toFixed(2)} ${unit}`
    size /= 1024
  }
  return `${size.toFixed(2)} Po`
}

function isDir(p: string): boolean {
  try {
    return fs.statSync(p).isDirectory()
  } catch {
    return false
  }
}

export function folderSize(targetPath: string | null): number {
  if (!targetPath || !isDir(targetPath)) return 0
  let total = 0
  const stack: string[] = [targetPath]
  while (stack.length) {
    const dir = stack.pop()!
    let entries: fs.Dirent[]
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true })
    } catch {
      continue
    }
    for (const entry of entries) {
      const full = path.join(dir, entry.name)
      try {
        if (entry.isSymbolicLink()) continue
        if (entry.isDirectory()) {
          stack.push(full)
        } else {
          total += fs.statSync(full).size
        }
      } catch {
        // ignore unreadable entries, matches Python's best-effort walk
      }
    }
  }
  return total
}

export function clearFolderContents(targetPath: string): number {
  if (!isDir(targetPath)) return 0
  let freed = 0
  let entries: fs.Dirent[]
  try {
    entries = fs.readdirSync(targetPath, { withFileTypes: true })
  } catch {
    return 0
  }
  for (const entry of entries) {
    const full = path.join(targetPath, entry.name)
    try {
      const sizeBefore = entry.isDirectory() ? folderSize(full) : fs.statSync(full).size
      if (entry.isDirectory() && !entry.isSymbolicLink()) {
        fs.rmSync(full, { recursive: true, force: true })
      } else {
        fs.rmSync(full, { force: true })
      }
      freed += sizeBefore
    } catch {
      // best effort, same as Python version
    }
  }
  return freed
}

export async function emptyRecycleBin(driveLetter: string): Promise<void> {
  if (!/^[A-Za-z]$/.test(driveLetter)) return
  try {
    await execFileAsync("powershell.exe", [
      "-NoProfile",
      "-NonInteractive",
      "-Command",
      `Clear-RecycleBin -DriveLetter ${driveLetter.toUpperCase()} -Force -ErrorAction SilentlyContinue`,
    ])
  } catch {
    // best effort, matches SHEmptyRecycleBinW's silent failure mode
  }
}

export function getDrives(): DriveInfo[] {
  const drives: DriveInfo[] = []
  for (let code = 65; code <= 90; code++) {
    const letter = String.fromCharCode(code)
    const root = `${letter}:\\`
    if (!fs.existsSync(root)) continue
    try {
      // statfsSync gives block-based usage, available Node >= 18.15
      const stat = (fs as any).statfsSync(root)
      const total = stat.blocks * stat.bsize
      const free = stat.bfree * stat.bsize
      drives.push({ letter, total, free, used: total - free })
    } catch {
      // drive exists but usage unreadable (e.g. empty optical drive)
    }
  }
  return drives
}

let runningProcessNamesCache: { names: Set<string>; at: number } | null = null

async function getRunningProcessNames(): Promise<Set<string>> {
  const now = Date.now()
  if (runningProcessNamesCache && now - runningProcessNamesCache.at < 2000) {
    return runningProcessNamesCache.names
  }
  const names = new Set<string>()
  try {
    const { stdout } = await execFileAsync("tasklist.exe", ["/FO", "CSV", "/NH"])
    for (const line of stdout.split(/\r?\n/)) {
      const match = line.match(/^"([^"]+)"/)
      if (match) names.add(match[1].toLowerCase())
    }
  } catch {
    // ignore, treated as "nothing running"
  }
  runningProcessNamesCache = { names, at: now }
  return names
}

export async function processRunning(name: string): Promise<boolean> {
  const names = await getRunningProcessNames()
  return names.has(name.toLowerCase())
}

function findProfiles(base: string, patternDirname: string): string[] {
  const root = path.join(base, patternDirname)
  if (!isDir(root)) return []
  const profiles: string[] = []
  for (const name of fs.readdirSync(root)) {
    const p = path.join(root, name)
    if (isDir(p) && (name === "Default" || name.startsWith("Profile "))) {
      profiles.push(p)
    }
  }
  return profiles
}

function findLatestAppVersionDirs(base: string): string[] {
  if (!isDir(base)) return []
  const versions: [string, string][] = []
  for (const name of fs.readdirSync(base)) {
    if (name.startsWith("app-")) {
      const p = path.join(base, name)
      if (isDir(p)) versions.push([name, p])
    }
  }
  if (versions.length <= 1) return []
  const versionKey = (name: string) =>
    name
      .slice(4)
      .split(".")
      .map((part) => parseInt(part, 10) || 0)
  versions.sort((a, b) => {
    const ka = versionKey(a[0])
    const kb = versionKey(b[0])
    for (let i = 0; i < Math.max(ka.length, kb.length); i++) {
      const diff = (ka[i] ?? 0) - (kb[i] ?? 0)
      if (diff !== 0) return diff
    }
    return 0
  })
  return versions.slice(0, -1).map(([, p]) => p)
}

export function listTopLevelEntries(driveLetter: string): [string, number][] {
  const root = `${driveLetter}:\\`
  const entries: [string, number][] = []
  let scan: fs.Dirent[]
  try {
    scan = fs.readdirSync(root, { withFileTypes: true })
  } catch {
    return entries
  }
  for (const entry of scan) {
    const full = path.join(root, entry.name)
    let size = 0
    try {
      size = entry.isDirectory() && !entry.isSymbolicLink() ? folderSize(full) : fs.statSync(full).size
    } catch {
      continue
    }
    if (size > 0) entries.push([entry.name, size])
  }
  return entries
}

export function buildOverviewBuckets(entries: [string, number][], topN = 8): OverviewBucket[] {
  const sorted = [...entries].sort((a, b) => b[1] - a[1])
  const top = sorted.slice(0, topN)
  const rest = sorted.slice(topN)
  const buckets: OverviewBucket[] = top.map(([label, size]) => ({ label, size }))
  const restTotal = rest.reduce((sum, [, size]) => sum + size, 0)
  if (restTotal > 0) buckets.push({ label: `Autres (${rest.length} elements)`, size: restTotal })
  return buckets
}

async function buildRules(driveLetter: string): Promise<Rule[]> {
  const rules: Rule[] = []
  const systemDrive = (process.env.SystemDrive ?? "C:")[0].toUpperCase()
  const isSystemDrive = driveLetter.toUpperCase() === systemDrive

  rules.push({
    key: `recycle_${driveLetter}`,
    label: `Corbeille (disque ${driveLetter}:)`,
    risk: RISK_SAFE,
    description: "Vide la corbeille de ce disque.",
    path: null,
    isRecycleBin: true,
    size: 0,
  })

  if (!isSystemDrive) return rules

  const localAppdata = process.env.LOCALAPPDATA ?? ""
  const windir = process.env.WINDIR ?? "C:\\Windows"

  const realUserTemp = path.join(localAppdata, "Temp")
  rules.push({
    key: "user_temp",
    label: "Fichiers temporaires utilisateur",
    risk: RISK_SAFE,
    description: `Contenu de ${realUserTemp}`,
    path: realUserTemp,
    size: 0,
  })

  const envTemp = process.env.TEMP ?? ""
  if (envTemp && path.normalize(envTemp).toLowerCase() !== path.normalize(realUserTemp).toLowerCase()) {
    rules.push({
      key: "env_temp",
      label: "Dossier TEMP courant (variable d'environnement)",
      risk: RISK_SAFE,
      description: `Contenu de ${envTemp}`,
      path: envTemp,
      size: 0,
    })
  }

  rules.push({
    key: "win_temp",
    label: "Fichiers temporaires Windows",
    risk: RISK_SAFE,
    description: `Contenu de ${windir}\\Temp (necessite parfois les droits admin)`,
    path: path.join(windir, "Temp"),
    size: 0,
  })

  rules.push({
    key: "wer",
    label: "Rapports d'erreurs Windows (WER)",
    risk: RISK_SAFE,
    description: "Rapports de plantage collectes par Windows.",
    path: "C:\\ProgramData\\Microsoft\\Windows\\WER",
    size: 0,
  })

  const browserTargets: [string, string, string][] = [
    ["chrome", "Google Chrome", path.join(localAppdata, "Google", "Chrome", "User Data")],
    ["edge", "Microsoft Edge", path.join(localAppdata, "Microsoft", "Edge", "User Data")],
    ["brave", "Brave", path.join(localAppdata, "BraveSoftware", "Brave-Browser", "User Data")],
  ]
  for (const [key, name, base] of browserTargets) {
    const proc = key !== "brave" ? `${key}.exe` : "brave.exe"
    for (const profile of findProfiles(path.dirname(base), path.basename(base))) {
      const cacheDir = path.join(profile, "Cache")
      const codeCacheDir = path.join(profile, "Code Cache")
      const profileName = path.basename(profile)
      if (isDir(cacheDir)) {
        rules.push({
          key: `${key}_cache_${profileName}`,
          label: `Cache ${name} (${profileName})`,
          risk: RISK_SAFE,
          description: "Cache de pages web, se reconstruit automatiquement.",
          path: cacheDir,
          lockedProcess: proc,
          size: 0,
        })
      }
      if (isDir(codeCacheDir)) {
        rules.push({
          key: `${key}_codecache_${profileName}`,
          label: `Code Cache ${name} (${profileName})`,
          risk: RISK_SAFE,
          description: "Cache de scripts compiles, se reconstruit automatiquement.",
          path: codeCacheDir,
          lockedProcess: proc,
          size: 0,
        })
      }
    }
    const modelDir = path.join(base, "OptGuideOnDeviceModel")
    if (isDir(modelDir)) {
      rules.push({
        key: `${key}_aimodel`,
        label: `Modele IA embarque ${name}`,
        risk: RISK_SAFE,
        description: "Modele d'optimisation on-device, retelecharge si besoin.",
        path: modelDir,
        lockedProcess: proc,
        size: 0,
      })
    }
  }

  const firefoxBase = path.join(localAppdata, "Mozilla", "Firefox", "Profiles")
  if (isDir(firefoxBase)) {
    for (const name of fs.readdirSync(firefoxBase)) {
      const cacheDir = path.join(firefoxBase, name, "cache2")
      if (isDir(cacheDir)) {
        rules.push({
          key: `firefox_cache_${name}`,
          label: `Cache Firefox (${name})`,
          risk: RISK_SAFE,
          description: "Cache de pages web, se reconstruit automatiquement.",
          path: cacheDir,
          lockedProcess: "firefox.exe",
          size: 0,
        })
      }
    }
  }

  const gpuTargets: [string, string, string][] = [
    ["nvidia_dxcache", "Cache shaders NVIDIA (DirectX)", path.join(localAppdata, "NVIDIA", "DXCache")],
    ["nvidia_glcache", "Cache shaders NVIDIA (OpenGL)", path.join(localAppdata, "NVIDIA", "GLCache")],
    ["amd_dxcache", "Cache shaders AMD", path.join(localAppdata, "AMD", "DxCache")],
  ]
  for (const [key, label, p] of gpuTargets) {
    if (isDir(p)) {
      rules.push({
        key,
        label,
        risk: RISK_SAFE,
        description: "Cache de shaders compiles, se reconstruit automatiquement.",
        path: p,
        size: 0,
      })
    }
  }

  const appVersionTargets: [string, string, string, string][] = [
    ["discord", "Discord", path.join(localAppdata, "Discord"), "Discord.exe"],
    ["slack", "Slack", path.join(localAppdata, "slack"), "slack.exe"],
  ]
  for (const [key, name, base, proc] of appVersionTargets) {
    for (const od of findLatestAppVersionDirs(base)) {
      rules.push({
        key: `${key}_old_${path.basename(od)}`,
        label: `Ancienne version ${name} (${path.basename(od)})`,
        risk: RISK_SAFE,
        description: "Version precedente conservee apres mise a jour automatique.",
        path: od,
        lockedProcess: proc,
        size: 0,
      })
    }
  }

  const spotifyData = path.join(localAppdata, "Spotify", "Data")
  if (isDir(spotifyData)) {
    rules.push({
      key: "spotify_data",
      label: "Cache local Spotify",
      risk: RISK_REVIEW,
      description: "Morceaux mis en cache localement, seront re-telecharges en streaming.",
      path: spotifyData,
      lockedProcess: "Spotify.exe",
      size: 0,
    })
  }

  const devTargets: [string, string, string][] = [
    ["pip_cache", "Cache pip", path.join(localAppdata, "pip", "cache")],
    ["pnpm_cache", "Cache pnpm", path.join(localAppdata, "pnpm-cache")],
    ["npm_cache", "Cache npm", path.join(localAppdata, "npm-cache")],
  ]
  for (const [key, label, p] of devTargets) {
    if (isDir(p)) {
      rules.push({
        key,
        label,
        risk: RISK_SAFE,
        description: "Cache d'outil de developpement, se reconstruit automatiquement.",
        path: p,
        size: 0,
      })
    }
  }

  const playwrightDir = path.join(localAppdata, "ms-playwright")
  if (isDir(playwrightDir)) {
    rules.push({
      key: "playwright",
      label: "Navigateurs Playwright (tests automatises)",
      risk: RISK_REVIEW,
      description: "A garder si tu fais du developpement/tests avec Playwright.",
      path: playwrightDir,
      size: 0,
    })
  }

  const windowsOld = "C:\\Windows.old"
  if (isDir(windowsOld)) {
    rules.push({
      key: "windows_old",
      label: "Windows.old (ancienne installation Windows)",
      risk: RISK_REVIEW,
      description:
        "Permet de revenir a la version precedente de Windows. A supprimer seulement si tu es sur de ne pas revenir en arriere.",
      path: windowsOld,
      size: 0,
    })
  }

  return rules
}

export async function scanDrive(driveLetter: string): Promise<Rule[]> {
  const rules = await buildRules(driveLetter)
  for (const rule of rules) {
    if (rule.isRecycleBin) {
      rule.size = folderSize(path.join(`${driveLetter}:\\`, "$Recycle.Bin"))
    } else {
      rule.size = folderSize(rule.path)
    }
  }
  const withSize = rules.filter((r) => r.isRecycleBin || r.size > 0)
  withSize.sort((a, b) => b.size - a.size)
  // resolve locked-process flags for the UI without blocking the scan itself
  for (const rule of withSize) {
    if (rule.lockedProcess) {
      ;(rule as any).locked = await processRunning(rule.lockedProcess)
    }
  }
  return withSize
}

export async function cleanItems(
  items: { path: string | null; isRecycleBin?: boolean; size: number }[],
  driveLetter: string
): Promise<number> {
  let freedTotal = 0
  for (const item of items) {
    try {
      if (item.isRecycleBin) {
        await emptyRecycleBin(driveLetter)
        freedTotal += item.size
      } else if (item.path) {
        freedTotal += clearFolderContents(item.path)
      }
    } catch {
      // best effort, same as Python version
    }
  }
  return freedTotal
}

export function tempDirPlatformCheck() {
  return os.platform() === "win32"
}
