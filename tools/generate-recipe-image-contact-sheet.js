const fs = require("fs");
const path = require("path");
const sharp = require("../frontend/node_modules/sharp");

const root = path.resolve(__dirname, "..");
const batches = [1, 2, 3, 4, 5].flatMap(batch =>
  [...fs.readFileSync(path.join(root, "backend", "CulinaryBlog.Infrastructure", "Persistence", "Seed", `RealRecipeSeedBatch0${batch}.cs`), "utf8")
    .matchAll(/(?:Dish|D)\("([^"]+)"/g)].map(match => match[1]));
if (batches.length !== 100) throw new Error(`Expected 100 titles, found ${batches.length}.`);
const batch01 = path.join(root, "seed-assets", "recipe-images", "batch01");
const firstManifest = JSON.parse(fs.readFileSync(path.join(batch01, "manifest.json"), "utf8"));
const laterRoot = path.join(root, "CulinaryBlog_Batch03_05_60_Anh_DuThao");
const later = JSON.parse(fs.readFileSync(path.join(laterRoot, "manifest_all_60.json"), "utf8")).images;
const replacements = new Map(fs.readdirSync(path.join(laterRoot, "seed-assets", "recipe-images"), { recursive: true })
  .filter(file => file.endsWith("-replacement.webp"))
  .map(file => [Number.parseInt(path.basename(file), 10), path.join(laterRoot, "seed-assets", "recipe-images", file)]));
const escape = value => value.replace(/[&<>"']/g, character => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", '"':"&quot;", "'":"&apos;" })[character]);
const imageFor = number => {
  if (number <= 6) return path.join(batch01, firstManifest[number - 1].file);
  if (number <= 20) return fs.readdirSync(batch01).map(file => path.join(batch01, file)).find(file => path.basename(file).startsWith(`${String(number).padStart(2, "0")}-`));
  if (number <= 40) return fs.readdirSync(path.join(root, "seed-assets", "recipe-images", "batch02")).map(file => path.join(root, "seed-assets", "recipe-images", "batch02", file)).find(file => path.basename(file).startsWith(`${number}-`));
  if (replacements.has(number)) return replacements.get(number);
  const record = later.find(item => item.recipeNumber === number);
  return record && path.join(laterRoot, record.relativePath);
};

(async () => {
  const width = 180, imageHeight = 135, labelHeight = 45, columns = 5;
  const tiles = await Promise.all(batches.map(async (title, index) => {
    const number = index + 1, source = imageFor(number);
    if (!source || !fs.existsSync(source)) throw new Error(`Missing image for ${number}.`);
    const photo = await sharp(source).resize(width, imageHeight, { fit: "cover" }).jpeg().toBuffer();
    const label = `<svg width="${width}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg"><rect width="100%" height="100%" fill="#fff7ed"/><text x="8" y="17" font-family="Arial" font-size="12" font-weight="700">${number}. ${escape(title)}</text></svg>`;
    return sharp({ create: { width, height: imageHeight + labelHeight, channels: 3, background: "white" } })
      .composite([{ input: photo, top: 0, left: 0 }, { input: Buffer.from(label), top: imageHeight, left: 0 }]).jpeg().toBuffer();
  }));
  const rows = Math.ceil(tiles.length / columns);
  await sharp({ create: { width: width * columns, height: (imageHeight + labelHeight) * rows, channels: 3, background: "white" } })
    .composite(tiles.map((input, index) => ({ input, left: (index % columns) * width, top: Math.floor(index / columns) * (imageHeight + labelHeight) })))
    .jpeg({ quality: 88 }).toFile(path.join(root, "seed-assets", "recipe-images", "contact-sheet-100.jpg"));
})();
