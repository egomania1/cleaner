import packager from "electron-packager"
import path from "node:path"
import { fileURLToPath } from "node:url"

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)))

await packager({
  dir: root,
  name: "Clean",
  platform: "win32",
  arch: "x64",
  out: path.join(root, "release"),
  icon: path.join(root, "build", "icon.ico"),
  overwrite: true,
  ignore: [
    /^\/electron($|\/)/,
    /^\/src($|\/)/,
    /^\/scripts($|\/)/,
    /^\/(\.gitignore|tsconfig.*|vite\.config\.ts|components\.json)$/,
  ],
})

console.log("Packaged into release/Clean-win32-x64")
