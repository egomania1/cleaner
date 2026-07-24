import { contextBridge, ipcRenderer } from "electron"

export interface Rule {
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

const api = {
  listDrives: (): Promise<DriveInfo[]> => ipcRenderer.invoke("drives:list"),
  scanDrive: (letter: string): Promise<Rule[]> => ipcRenderer.invoke("drive:scan", letter),
  getOverview: (letter: string): Promise<OverviewBucket[]> => ipcRenderer.invoke("drive:overview", letter),
  cleanItems: (keys: string[], letter: string): Promise<number> =>
    ipcRenderer.invoke("drive:clean", keys, letter),
  confirm: (message: string, detail?: string): Promise<boolean> =>
    ipcRenderer.invoke("dialog:confirm", message, detail),
}

contextBridge.exposeInMainWorld("api", api)

export type Api = typeof api
