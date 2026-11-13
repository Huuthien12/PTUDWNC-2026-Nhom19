const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const reviewDir = path.join(root, "seed-upgrade-image-review");
const approved = new Set([49, 56, 58, 67, 76, 88, 89, 99, 100]);
const sha256 = file => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const template = JSON.parse(fs.readFileSync(path.join(reviewDir, "image-approval-template.json"), "utf8"));
const preflight = JSON.parse(fs.readFileSync(path.join(reviewDir, "image-preflight-report.json"), "utf8"));
const records = new Map(preflight.records.map(record => [record.recipeNumber, record]));

if (template.approvals.length !== 100 || records.size !== 100) throw new Error("Expected 100 approval and preflight records.");
const approvals = template.approvals.map(approval => {
  const record = records.get(approval.recipeNumber);
  if (!record || record.recipeId !== approval.recipeId || record.slug !== approval.slug || record.imageSHA256 !== approval.imageSHA256)
    throw new Error(`Immutable binding mismatch for recipe ${approval.recipeNumber}.`);
  if (!approved.has(approval.recipeNumber)) return approval;
  if (!/-replacement-v2\.webp$/i.test(record.imageFile)) throw new Error(`Approved recipe ${approval.recipeNumber} is not bound to a v2 replacement.`);
  const source = path.join(root, record.source);
  if (record.technicalIssues.length || !fs.existsSync(source) || sha256(source) !== approval.imageSHA256)
    throw new Error(`Approved recipe ${approval.recipeNumber} failed local validation.`);
  return { ...approval, reviewApproved: true, autoImportAllowed: false, approveImport: false };
});
if (approvals.filter(item => item.reviewApproved).length !== approved.size ||
    approvals.some(item => !approved.has(item.recipeNumber) && item.reviewApproved)) throw new Error("Approval scope is not exactly the administrator-approved v2 set.");

const output = {
  schemaVersion: 1,
  artifactType: "administrator-visual-approval-only",
  approvedRecipeNumbers: [...approved].sort((a, b) => a - b),
  generatedFrom: "image-approval-template.json + image-preflight-report.json",
  approvals,
};
const destination = path.join(reviewDir, "image-approval-v2-09-admin-approved.json");
fs.writeFileSync(destination, JSON.stringify(output, null, 2) + "\n");
console.log(JSON.stringify({ destination, visuallyApproved: approved.size, importAuthorized: 0 }));
