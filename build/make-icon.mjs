import sharp from "sharp"
import pngToIco from "png-to-ico"
import fs from "node:fs/promises"
import path from "node:path"
import { fileURLToPath } from "node:url"

const dir = path.dirname(fileURLToPath(import.meta.url))
const sizes = [16, 24, 32, 48, 64, 128, 256]
const src = path.join(dir, "icon-source.png")

const buffers = await Promise.all(
  sizes.map((size) => sharp(src).resize(size, size).png().toBuffer())
)

const ico = await pngToIco(buffers)
await fs.writeFile(path.join(dir, "icon.ico"), ico)
await sharp(src).resize(512, 512).png().toFile(path.join(dir, "icon.png"))
console.log("icon.ico + icon.png written")
