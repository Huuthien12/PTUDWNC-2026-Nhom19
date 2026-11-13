const crypto = require("crypto");
const { execFileSync } = require("child_process");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const reviewDir = path.join(root, "seed-upgrade-image-review");
const read = file => JSON.parse(fs.readFileSync(file, "utf8"));
const sha256 = file => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const stableUploadId = (recipeId, imageHash) => {
  const bytes = crypto.createHash("sha256").update(`seed-image-import:${recipeId}:${imageHash}`).digest().subarray(0, 16);
  bytes[6] = (bytes[6] & 0x0f) | 0x50; // UUID v5-shaped stable identifier.
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  // Match Guid.ToString("N") in the .NET importer, including Guid's mixed-endian first fields.
  return Buffer.concat([Buffer.from(bytes.subarray(0, 4)).reverse(), Buffer.from(bytes.subarray(4, 6)).reverse(),
    Buffer.from(bytes.subarray(6, 8)).reverse(), bytes.subarray(8)]).toString("hex");
};
const liveRecipes = () => {
  const sql = "SELECT \"Id\", \"Slug\" FROM \"Recipes\" WHERE \"Slug\" ~ '^lab02-recipe-([1-9]|[1-9][0-9]|100)$' ORDER BY \"Slug\";";
  const output = execFileSync("docker", ["compose", "exec", "-T", "postgres", "sh", "-c",
    "psql -v ON_ERROR_STOP=1 -U \"$POSTGRES_USER\" -d CulinaryBlogDb -At -F '|'"], { input: sql, encoding: "utf8" });
  const rows = output.trim().split(/\r?\n/).filter(Boolean).map(line => {
    const [recipeId, slug] = line.split("|"); return { recipeId, slug };
  });
  if (rows.length !== 100 || new Set(rows.map(row => row.slug)).size !== 100) throw new Error("Production identity query did not return exactly 100 seed recipes.");
  return new Map(rows.map(row => [row.slug, row.recipeId]));
};

// This command deliberately has no MinIO or EF dependency: dry-run validates the immutable handoff only.
// shortcut: actual import is intentionally deferred until an administrator signs individual approvals.
try {
  const preflight = read(path.join(reviewDir, "image-preflight-report.json"));
  const template = read(path.resolve(process.argv[2] || path.join(reviewDir, "image-approval-template.json")));
  if (preflight.candidateCount !== 100 || template.approvals.length !== 100) throw new Error("Expected exactly 100 review and approval records.");
  const records = new Map(preflight.records.map(record => [record.recipeNumber, record]));
  const production = liveRecipes();
  const seen = new Set(); const planned = []; const rejected = [];
  for (const approval of template.approvals) {
    const record = records.get(approval.recipeNumber);
    if (!record || seen.has(approval.recipeNumber)) throw new Error(`Invalid or duplicate approval record ${approval.recipeNumber}.`);
    seen.add(approval.recipeNumber);
    if (approval.recipeId !== record.recipeId || approval.slug !== record.slug || approval.imageSHA256 !== record.imageSHA256)
      throw new Error(`Approval binding mismatch for recipe ${approval.recipeNumber}.`);
    if (production.get(approval.slug) !== approval.recipeId)
      throw new Error(`Production RecipeId/slug mismatch for recipe ${approval.recipeNumber}.`);
    if (!approval.reviewApproved) {
      rejected.push({ recipeNumber: approval.recipeNumber, reason: "PENDING_VISUAL_APPROVAL" }); continue;
    }
    const image = path.join(root, record.source);
    if (record.technicalIssues.length || !fs.existsSync(image) || sha256(image) !== approval.imageSHA256)
      throw new Error(`Approved image validation failed for recipe ${approval.recipeNumber}.`);
    const uploadId = stableUploadId(approval.recipeId, approval.imageSHA256);
    const executionAuthorized = approval.autoImportAllowed && approval.approveImport;
    planned.push({ recipeNumber: approval.recipeNumber, recipeId: approval.recipeId, source: record.source, uploadId, executionAuthorized,
      // Matches the existing IFileStorageService convention, so streaming/cleanup can parse the key.
      objectKey: `recipes/${approval.recipeId}/${uploadId}-${approval.imageSHA256}.webp` });
    if (!executionAuthorized) rejected.push({ recipeNumber: approval.recipeNumber, reason: "PENDING_IMPORT_AUTHORIZATION" });
  }
  if (seen.size !== 100) throw new Error("Approval file is incomplete.");
  const report = { mode: "dry-run", databaseName: "CulinaryBlogDb", productionIdentityValidated: true,
    writesAttempted: false, minioTouched: false, databaseWritesAttempted: false,
    plannedImports: planned, skipped: rejected, summary: { visuallyApproved: planned.length,
      importAuthorized: planned.filter(item => item.executionAuthorized).length, pending: rejected.length } };
  fs.writeFileSync(path.join(reviewDir, "image-import-dry-run-report.json"), JSON.stringify(report, null, 2) + "\n");
  console.log(JSON.stringify(report.summary));
} catch (error) { console.error(error.message); process.exitCode = 1; }
