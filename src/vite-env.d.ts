/// <reference types="vite/client" />

interface Rule {
  key: string
  label: string
  risk: "SUR" | "A VERIFIER"
  description: string
  path: string | null
  lockedProcess?: string
  isRecycleBin?: boolean
  locked?: boolean
  size: number
}

interface DriveInfo {
  letter: string
  total: number
  free: number
  used: number
}

interface OverviewBucket {
  label: string
  size: number
}

interface Window {
  api: {
    listDrives: () => Promise<DriveInfo[]>
    scanDrive: (letter: string) => Promise<Rule[]>
    getOverview: (letter: string) => Promise<OverviewBucket[]>
    cleanItems: (keys: string[], letter: string) => Promise<number>
    confirm: (message: string, detail?: string) => Promise<boolean>
  }
}
