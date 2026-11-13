const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const sharp = require("../frontend/node_modules/sharp");

const root = path.resolve(__dirname, "..");
const output = path.join(root, "seed-upgrade-review");
const assetRoot = path.join(root, "CulinaryBlog_Batch03_05_60_Anh_DuThao");
const manifest = JSON.parse(fs.readFileSync(path.join(assetRoot, "manifest_all_60.json"), "utf8")).images;
const numbers = [48, 49, 56, 58, 67, 73, 76, 80, 81, 84, 88, 89, 99, 100];
const replacements = new Map(fs.readdirSync(path.join(assetRoot, "seed-assets", "recipe-images"), { recursive: true })
  .filter(file => file.endsWith("-replacement.webp"))
  .map(file => [Number.parseInt(path.basename(file), 10), path.join(assetRoot, "seed-assets", "recipe-images", file)]));
const sourceRoot = path.join(root, "seed-assets", "recipe-images", "replacements-13");
const sha256 = file => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const escape = value => value.replace(/[&<>"']/g, character => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", '"':"&quot;", "'":"&apos;" })[character]);

(async () => {
  fs.mkdirSync(output, { recursive: true });
  const rows = [];
  for (const number of numbers) {
    const record = manifest.find(item => item.recipeNumber === number);
    const file = replacements.get(number);
    if (!record || !file) throw new Error(`Missing standardized replacement for recipe ${number}.`);
    const metadata = await sharp(file).metadata();
    const source = number === 81
      ? path.join(path.dirname(file), "081-bun-cha-ha-noi-replacement.png")
      : fs.readdirSync(sourceRoot).map(name => path.join(sourceRoot, name)).find(item => Number.parseInt(path.basename(item), 10) === number);
    const sourceMetadata = source ? await sharp(source).metadata() : null;
    rows.push({ recipeNumber: number, recipeTitle: record.recipeTitle, targetSlug: record.targetSlug,
      standardizedFile: path.relative(root, file).replaceAll("\\", "/"), standardizedBytes: fs.statSync(file).size,
      standardizedSha256: sha256(file), width: metadata.width, height: metadata.height, format: metadata.format,
      sourceFile: source ? path.relative(root, source).replaceAll("\\", "/") : "",
      sourceBytes: source ? fs.statSync(source).size : null, sourceDecoded: Boolean(sourceMetadata?.width && sourceMetadata?.height),
      visualMatchStatus: "REVIEW_REQUIRED", decision: "PENDING_ADMIN_APPROVAL" });
  }
  fs.writeFileSync(path.join(output, "replacement-image-review.json"), JSON.stringify(rows, null, 2));
  const csv = [Object.keys(rows[0]).join(","), ...rows.map(row => Object.values(row).map(value => `"${String(value ?? "").replaceAll('"', '""')}"`).join(","))].join("\n");
  fs.writeFileSync(path.join(output, "replacement-image-review.csv"), csv);
  const markdown = ["# Replacement image review", "", "All replacements decoded successfully. Visual suitability remains `REVIEW_REQUIRED`; no manifest flag was changed.", "", "| # | Recipe | Standardized WebP | Bytes | SHA-256 | Dimensions | Source retained | Decision |", "|---:|---|---|---:|---|---|---|---|"];
  for (const row of rows) markdown.push(`| ${row.recipeNumber} | ${row.recipeTitle} | ${row.standardizedFile} | ${row.standardizedBytes} | ${row.standardizedSha256} | ${row.width}×${row.height} | ${row.sourceFile} | ${row.decision} |`);
  fs.writeFileSync(path.join(output, "replacement-image-review.md"), markdown.join("\n"));
  const width = 300, imageHeight = 225, labelHeight = 54, columns = 2;
  const tiles = await Promise.all(rows.map(async row => {
    const photo = await sharp(path.join(root, row.standardizedFile)).resize(width, imageHeight, { fit: "cover" }).jpeg().toBuffer();
    const label = `<svg width="${width}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg"><rect width="100%" height="100%" fill="#fff7ed"/><text x="8" y="20" font-family="Arial" font-size="15" font-weight="700">${row.recipeNumber}. ${escape(row.recipeTitle)}</text><text x="8" y="42" font-family="Arial" font-size="12">PENDING ADMIN APPROVAL</text></svg>`;
    return sharp({ create: { width, height: imageHeight + labelHeight, channels: 3, background: "white" } }).composite([{ input: photo }, { input: Buffer.from(label), top: imageHeight, left: 0 }]).jpeg().toBuffer();
  }));
  await sharp({ create: { width: width * columns, height: Math.ceil(tiles.length / columns) * (imageHeight + labelHeight), channels: 3, background: "white" } })
    .composite(tiles.map((input, index) => ({ input, left: (index % columns) * width, top: Math.floor(index / columns) * (imageHeight + labelHeight) })))
    .jpeg({ quality: 88 }).toFile(path.join(output, "replacement-review-sheet-14.jpg"));
})();
