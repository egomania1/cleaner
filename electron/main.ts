import { app, BrowserWindow, ipcMain, dialog, Menu } from "electron"
import path from "node:path"
import { fileURLToPath } from "node:url"
import {
  getDrives,
  scanDrive,
  cleanItems,
  listTopLevelEntries,
  buildOverviewBuckets,
  type Rule,
} from "./cleaner"

const __dirname = path.dirname(fileURLToPath(import.meta.url))

process.env.APP_ROOT = path.join(__dirname, "..")
export const VITE_DEV_SERVER_URL = process.env["VITE_DEV_SERVER_URL"]
export const MAIN_DIST = path.join(process.env.APP_ROOT, "dist-electron")
export const RENDERER_DIST = path.join(process.env.APP_ROOT, "dist")

const ICON_PATH = path.join(process.env.APP_ROOT, "build", "icon.ico")

let win: BrowserWindow | null = null

// Authoritative snapshot of the last scan per drive, keyed by rule key.
// The renderer can only ever request deletion of paths this process itself
// discovered — it never gets to hand back an arbitrary filesystem path.
const lastScan = new Map<string, Map<string, Rule>>()

function isValidDriveLetter(value: unknown): value is string {
  return typeof value === "string" && /^[A-Za-z]$/.test(value)
}

Menu.setApplicationMenu(null)

function createWindow() {
  win = new BrowserWindow({
    title: "Clean",
    icon: ICON_PATH,
    width: 980,
    height: 780,
    minWidth: 820,
    minHeight: 600,
    resizable: true,
    backgroundMaterial: "acrylic",
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, "preload.mjs"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      webSecurity: true,
    },
  })

  win.setMenuBarVisibility(false)

  // Defense in depth: this app never needs to open new windows or navigate
  // away from its own bundled UI.
  win.webContents.setWindowOpenHandler(() => ({ action: "deny" }))
  win.webContents.on("will-navigate", (event, url) => {
    if (url !== win?.webContents.getURL()) event.preventDefault()
  })

  if (VITE_DEV_SERVER_URL) {
    win.loadURL(VITE_DEV_SERVER_URL)
  } else {
    win.loadFile(path.join(RENDERER_DIST, "index.html"))
  }
}

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit()
    win = null
  }
})

app.on("activate", () => {
  if (BrowserWindow.getAllWindows().length === 0) createWindow()
})

app.whenReady().then(() => {
  createWindow()

  ipcMain.handle("drives:list", () => getDrives())

  ipcMain.handle("drive:scan", async (_e, letter: unknown) => {
    if (!isValidDriveLetter(letter)) return []
    const rules = await scanDrive(letter)
    lastScan.set(letter.toUpperCase(), new Map(rules.map((r) => [r.key, r])))
    return rules
  })

  ipcMain.handle("drive:overview", (_e, letter: unknown) => {
    if (!isValidDriveLetter(letter)) return []
    const entries = listTopLevelEntries(letter)
    return buildOverviewBuckets(entries, 8)
  })

  ipcMain.handle("drive:clean", (_e, keys: unknown, letter: unknown) => {
    if (!isValidDriveLetter(letter) || !Array.isArray(keys)) return 0
    const cached = lastScan.get(letter.toUpperCase())
    if (!cached) return 0
    const items = keys
      .filter((k): k is string => typeof k === "string")
      .map((k) => cached.get(k))
      .filter((r): r is Rule => Boolean(r))
    return cleanItems(items, letter)
  })

  ipcMain.handle("dialog:confirm", async (_e, message: unknown, detail?: unknown) => {
    if (!win || typeof message !== "string") return false
    const result = await dialog.showMessageBox(win, {
      type: "question",
      buttons: ["Annuler", "Confirmer"],
      defaultId: 0,
      cancelId: 0,
      message,
      detail: typeof detail === "string" ? detail : undefined,
    })
    return result.response === 1
  })
})
