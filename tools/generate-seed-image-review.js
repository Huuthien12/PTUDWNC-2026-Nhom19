const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const sharp = require("../frontend/node_modules/sharp");

const root = path.resolve(__dirname, "..");
const output = path.join(root, "seed-upgrade-image-review");
const laterRoot = path.join(root, "CulinaryBlog_Batch03_05_60_Anh_DuThao");
const priorityReplacementNumbers = new Set([48, 49, 56, 58, 67, 73, 76, 80, 81, 84, 88, 89, 99, 100]);
const correctedCandidateNumbers = new Set([49, 56, 58, 67, 76, 88, 89, 99, 100]);
const csv = value => `"${String(value ?? "").replaceAll('"', '""')}"`;
const hash = file => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const slugify = value => value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase()
  .replace(/đ/g, "d").replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "");

function readJson(file) { return JSON.parse(fs.readFileSync(file, "utf8")); }
function findReplacement(number) {
  const prefix = String(number).padStart(3, "0");
  const roots = [
    path.join(root, "seed-assets", "recipe-images", "replacements-13"),
    path.join(laterRoot, "seed-assets", "recipe-images", `batch${String(Math.ceil((number - 40) / 20) + 2).padStart(2, "0")}`),
  ];
  for (const dir of roots) {
    if (!fs.existsSync(dir)) continue;
    const found = fs.readdirSync(dir).find(name => name.startsWith(`${prefix}-`) && name.includes("-replacement") && name.endsWith(".webp"));
    if (found) return path.join(dir, found);
  }
  return null;
}

function sourceFor(number, laterManifest, firstManifest) {
  if (number <= 6) return path.join(root, "seed-assets", "recipe-images", "batch01", firstManifest[number - 1].file);
  if (number <= 20) {
    const dir = path.join(root, "seed-assets", "recipe-images", "batch01");
    return path.join(dir, fs.readdirSync(dir).find(name => name.startsWith(`${String(number).padStart(2, "0")}-`)));
  }
  if (number <= 40) {
    const dir = path.join(root, "seed-assets", "recipe-images", "batch02");
    return path.join(dir, fs.readdirSync(dir).find(name => name.startsWith(`${number}-`)));
  }
  const record = laterManifest.images.find(item => item.recipeNumber === number);
  if (record?.replacementCandidate?.relativePath) return path.join(laterRoot, record.replacementCandidate.relativePath);
  if (priorityReplacementNumbers.has(number)) return findReplacement(number);
  return record && path.join(laterRoot, record.relativePath);
}

function magic(file) {
  const data = fs.readFileSync(file);
  if (data.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))) return "image/png";
  if (data.subarray(0, 3).toString("ascii") === "\xff\xd8\xff") return "image/jpeg";
  if (data.subarray(0, 4).toString("ascii") === "RIFF" && data.subarray(8, 12).toString("ascii") === "WEBP") return "image/webp";
  return "unknown";
}

function wrap(text, max = 31) {
  const words = text.split(" "); const lines = []; let line = "";
  for (const word of words) {
    const next = line ? `${line} ${word}` : word;
    if (next.length > max && line) { lines.push(line); line = word; } else line = next;
  }
  if (line) lines.push(line);
  return lines.slice(0, 3);
}
function escape(text) { return text.replace(/[&<>"']/g, c => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", '"':"&quot;", "'":"&apos;" })[c]); }
async function contactSheet(records, target) {
  const width = 240, photoHeight = 180, labelHeight = 56, columns = 5;
  if (!records.length) {
    const svg = `<svg width="${width * columns}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg"><rect width="100%" height="100%" fill="#fff7ed"/><text x="12" y="32" font-family="Arial, sans-serif" font-size="16" font-weight="700" fill="#431407">No source-quality-risk candidates selected.</text></svg>`;
    await sharp(Buffer.from(svg)).jpeg({ quality: 88 }).toFile(target);
    return;
  }
  const tiles = await Promise.all(records.map(async record => {
    const photo = await sharp(record.source).resize(width, photoHeight, { fit: "cover" }).jpeg().toBuffer();
    const lines = [`${record.recipeNumber}. ${record.recipeTitle}`, ...wrap(record.imageFile, 34).slice(0, 2)];
    const svg = `<svg width="${width}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg"><rect width="100%" height="100%" fill="#fff7ed"/>${lines.map((line, index) => `<text x="7" y="${16 + index * 17}" font-family="Arial, sans-serif" font-size="${index === 0 ? 12 : 10}" font-weight="${index === 0 ? 700 : 400}" fill="#431407">${escape(line)}</text>`).join("")}</svg>`;
    return sharp({ create: { width, height: photoHeight + labelHeight, channels: 3, background: "white" } })
      .composite([{ input: photo, top: 0, left: 0 }, { input: Buffer.from(svg), top: photoHeight, left: 0 }]).jpeg().toBuffer();
  }));
  const tileHeight = photoHeight + labelHeight;
  await sharp({ create: { width: width * columns, height: tileHeight * Math.ceil(tiles.length / columns), channels: 3, background: "white" } })
    .composite(tiles.map((input, index) => ({ input, left: (index % columns) * width, top: Math.floor(index / columns) * tileHeight })))
    .jpeg({ quality: 88 }).toFile(target);
}

(async () => {
  const snapshot = readJson(path.join(root, "seed-upgrade-production-20261112-132622", "postapply-snapshot", "full-snapshot.json"));
  const firstManifest = readJson(path.join(root, "seed-assets", "recipe-images", "batch01", "manifest.json"));
  const laterManifest = readJson(path.join(laterRoot, "manifest_all_60.json"));
  const snapshots = new Map(snapshot.Recipes.map(item => [Number(item.Aggregate.Slug.replace("lab02-recipe-", "")), item.Aggregate]));
  if (snapshots.size !== 100) throw new Error(`Expected 100 production seed aggregates, found ${snapshots.size}.`);
  fs.mkdirSync(output, { recursive: true });
  const records = [];
  for (let recipeNumber = 1; recipeNumber <= 100; recipeNumber++) {
    const aggregate = snapshots.get(recipeNumber);
    const source = sourceFor(recipeNumber, laterManifest, firstManifest);
    if (!source || !fs.existsSync(source)) throw new Error(`Missing candidate for recipe ${recipeNumber}.`);
    let metadata; let decodeError = null;
    try { metadata = await sharp(source).metadata(); } catch (error) { decodeError = error.message; }
    const mime = magic(source); const size = fs.statSync(source).size;
    const isWebp = mime === "image/webp" && metadata?.format === "webp";
    const manifestRecord = laterManifest.images.find(item => item.recipeNumber === recipeNumber);
    const selectedReplacement = /-replacement(?:-v\d+)?\./.test(path.basename(source));
    const actualHash = hash(source);
    const expectedManifestHash = recipeNumber >= 41
      ? (selectedReplacement ? manifestRecord?.replacementCandidate?.sha256 : manifestRecord?.sha256)
      : null;
    const technicalIssues = [
      ...(decodeError ? [`decode failed: ${decodeError}`] : []),
      ...(!isWebp ? [`expected WebP, detected ${mime}/${metadata?.format ?? "unknown"}`] : []),
      ...(size > 5 * 1024 * 1024 ? ["larger than 5 MiB"] : []),
      ...(!metadata?.width || !metadata?.height ? ["missing decoded dimensions"] : []),
      ...(recipeNumber >= 41 && !expectedManifestHash ? ["selected image has no manifest SHA-256"] : []),
      ...(expectedManifestHash && String(expectedManifestHash).toLowerCase() !== actualHash
        ? ["selected image SHA-256 does not match manifest"] : []),
    ];
    const expectedSlug = slugify(aggregate.Title);
    const filenameNormalized = slugify(path.basename(source, path.extname(source)).replace(/^0?\d+-/, "").replace(/-replacement(?:-v\d+)?$/, ""));
    const mappingIssue = expectedSlug !== filenameNormalized
      ? `filename/title normalization mismatch: expected ${expectedSlug}, got ${filenameNormalized}` : null;
    const sourceCrop = manifestRecord?.sourceCropResolution ?? null;
    const sourceQualityWarning = recipeNumber >= 41 && !selectedReplacement && sourceCrop
      ? `provisional source crop ${sourceCrop.width}x${sourceCrop.height}; effective-resolution quality needs human review` : null;
    records.push({
      recipeNumber, recipeId: aggregate.Id, slug: aggregate.Slug, recipeTitle: aggregate.Title,
      imageFile: path.basename(source), source: path.relative(root, source).replaceAll("\\", "/"),
      imageSHA256: actualHash, dimension: metadata?.width && metadata?.height ? `${metadata.width}x${metadata.height}` : "",
      bytes: size, mime, decodedFormat: metadata?.format ?? "", expectedSlug, filenameNormalized,
      candidateStatus: technicalIssues.length ? "TECHNICAL_INVALID" : mappingIssue ? "MAPPING_REVIEW_REQUIRED" : sourceQualityWarning ? "SOURCE_QUALITY_REVIEW_REQUIRED" : selectedReplacement ? "REPLACEMENT_CANDIDATE" : "TECHNICAL_VALID_CANDIDATE",
      visualReviewStatus: "REVIEW_REQUIRED",
      approvedForImport: false, importStatus: "PENDING_ADMIN_APPROVAL",
      technicalIssues, mappingIssue, sourceCropResolution: sourceCrop, sourceQualityWarning,
      manifestVisualHint: manifestRecord?.visualMatchStatus ?? null,
    });
  }
  const duplicateFiles = records.filter((record, index) => records.findIndex(other => other.imageSHA256 === record.imageSHA256) !== index);
  const preflight = {
    generatedAtUtc: new Date().toISOString(),
    sourceSnapshot: "seed-upgrade-production-20261112-132622/postapply-snapshot/full-snapshot.json",
    candidateCount: records.length,
    technicalValidCount: records.filter(record => record.technicalIssues.length === 0).length,
    technicalInvalidCount: records.filter(record => record.technicalIssues.length > 0).length,
    sourceQualityReviewRequiredCount: records.filter(record => record.sourceQualityWarning).length,
    visualReviewRequiredCount: records.filter(record => record.visualReviewStatus === "REVIEW_REQUIRED").length,
    visualRejectedCandidateNumbers: [],
    replacementCandidateNumbers: records.filter(record => record.candidateStatus === "REPLACEMENT_CANDIDATE").map(record => record.recipeNumber),
    errors: records.filter(record => record.technicalIssues.length).map(record => ({ recipeNumber: record.recipeNumber, imageFile: record.imageFile, issues: record.technicalIssues })),
    duplicateHashCandidates: duplicateFiles.map(record => ({ recipeNumber: record.recipeNumber, imageFile: record.imageFile, imageSHA256: record.imageSHA256 })),
    filenameTitleMismatches: records.filter(record => record.mappingIssue).map(record => ({ recipeNumber: record.recipeNumber, imageFile: record.imageFile, recipeTitle: record.recipeTitle, issue: record.mappingIssue })),
    sourceQualityWarnings: records.filter(record => record.sourceQualityWarning).map(record => ({ recipeNumber: record.recipeNumber, imageFile: record.imageFile, warning: record.sourceQualityWarning })),
    records,
  };
  const reviewHeaders = ["RecipeNumber", "RecipeId", "RecipeTitle", "ImageFile", "ImageSHA256", "Dimension", "CandidateStatus", "VisualReviewStatus", "ApprovedForImport", "ImportStatus", "Source", "Bytes", "Mime", "TechnicalIssues", "MappingIssue", "SourceQualityWarning"];
  const reviewRows = records.map(record => [record.recipeNumber, record.recipeId, record.recipeTitle, record.imageFile, record.imageSHA256, record.dimension, record.candidateStatus, record.visualReviewStatus, record.approvedForImport, record.importStatus, record.source, record.bytes, record.mime, record.technicalIssues.join("; "), record.mappingIssue ?? "", record.sourceQualityWarning ?? ""]);
  fs.writeFileSync(path.join(output, "image-approval-review.csv"), [reviewHeaders, ...reviewRows].map(row => row.map(csv).join(",")).join("\n") + "\n");
  fs.writeFileSync(path.join(output, "image-approval-template.json"), JSON.stringify({ schemaVersion: 1, generatedFrom: "image-approval-review.csv", approvals: records.map(record => ({ recipeNumber: record.recipeNumber, recipeId: record.recipeId, slug: record.slug, imageFile: record.imageFile, imageSHA256: record.imageSHA256, reviewApproved: false, autoImportAllowed: false, approveImport: false })) }, null, 2) + "\n");
  fs.writeFileSync(path.join(output, "image-preflight-report.json"), JSON.stringify(preflight, null, 2) + "\n");
  const collageReview = laterManifest.images.filter(item => item.sourceCropResolution && item.visualMatchStatus === "match").map(item => {
    const selected = records.find(record => record.recipeNumber === item.recipeNumber);
    const replaced = selected.imageFile.includes("-replacement.");
    return {
      recipeNumber: item.recipeNumber, recipeTitle: item.recipeTitle, originalFile: item.imageFile,
      sourceCrop: `${item.sourceCropResolution.width}x${item.sourceCropResolution.height}`,
      selectedFile: selected.imageFile,
      qualityStatus: replaced ? "ACCEPTABLE_CANDIDATE" : "NEEDS_REPLACEMENT",
      reason: replaced ? "Independent replacement candidate selected; remains pending administrator visual approval." : "Original is a provisional low-resolution collage crop; do not upscale or import as a production representative image.",
    };
  });
  const collageHeaders = ["RecipeNumber", "RecipeTitle", "OriginalFile", "SourceCrop", "SelectedFile", "QualityStatus", "Reason"];
  fs.writeFileSync(path.join(output, "collage-quality-review.csv"), [collageHeaders, ...collageReview.map(item => [item.recipeNumber, item.recipeTitle, item.originalFile, item.sourceCrop, item.selectedFile, item.qualityStatus, item.reason])].map(row => row.map(csv).join(",")).join("\n") + "\n");
  await contactSheet(records, path.join(output, "contact-sheet-100.jpg"));
  await contactSheet(records.filter(record => priorityReplacementNumbers.has(record.recipeNumber)), path.join(output, "contact-sheet-replacements-14.jpg"));
  await contactSheet(records.filter(record => correctedCandidateNumbers.has(record.recipeNumber)), path.join(output, "contact-sheet-corrected-candidates-09.jpg"));
  await contactSheet(records.filter(record => record.sourceQualityWarning), path.join(output, "contact-sheet-quality-risk.jpg"));
  console.log(JSON.stringify({ candidates: records.length, technicalValid: preflight.technicalValidCount, technicalInvalid: preflight.technicalInvalidCount, reviewRequired: records.length, output }, null, 2));
})().catch(error => { console.error(error.message); process.exitCode = 1; });
