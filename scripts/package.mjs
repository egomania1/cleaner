import packager from "electron-packager"
import path from "node:path"
import { fileURLToPath } from "node:url"

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)))

// Allowlist: only the compiled output and package.json ever ship.
// Everything else (source, node_modules, docs, configs, local shortcuts)
// stays out of the package entirely.
const KEEP = /^[\\/](dist|dist-electron|package\.json)($|[\\/])/

await packager({
  dir: root,
  name: "Clean",
  platform: "win32",
  arch: "x64",
  out: path.join(root, "release"),
  icon: path.join(root, "build", "icon.ico"),
  asar: true,
  overwrite: true,
  ignore: (file) => file !== "" && !KEEP.test(file),
})

console.log("Packaged into release/Clean-win32-x64")
