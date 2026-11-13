const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const review = path.join(root, "seed-upgrade-image-review");
const read = name => JSON.parse(fs.readFileSync(path.join(review, name), "utf8"));
const sha256 = file => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const v2 = new Set([49, 56, 58, 67, 76, 88, 89, 99, 100]);
const template = read("image-approval-template.json");
const preflight = read("image-preflight-report.json");
const visualApproval = read("image-approval-v2-09-admin-approved.json");
const records = new Map(preflight.records.map(record => [record.recipeNumber, record]));
const visual = new Map(visualApproval.approvals.map(item => [item.recipeNumber, item]));

if (template.approvals.length !== 100 || records.size !== 100 || visual.size !== 100)
  throw new Error("Expected exactly 100 source records.");
const approvals = template.approvals.map(approval => {
  const record = records.get(approval.recipeNumber);
  if (!record || record.recipeId !== approval.recipeId || record.slug !== approval.slug || record.imageSHA256 !== approval.imageSHA256 ||
      record.technicalIssues.length || record.decodedFormat !== "webp")
    throw new Error(`Preflight binding failed for recipe ${approval.recipeNumber}.`);
  const source = path.join(root, record.source);
  if (!fs.existsSync(source) || sha256(source) !== approval.imageSHA256)
    throw new Error(`File hash failed for recipe ${approval.recipeNumber}.`);
  if (v2.has(approval.recipeNumber)) {
    const approvedV2 = visual.get(approval.recipeNumber);
    if (!approvedV2?.reviewApproved || approvedV2.imageFile !== record.imageFile ||
        approvedV2.imageSHA256 !== approval.imageSHA256 || !/-replacement-v2\.webp$/i.test(record.imageFile))
      throw new Error(`The v2 binding failed for recipe ${approval.recipeNumber}.`);
  }
  return { ...approval, reviewApproved: true, autoImportAllowed: true, approveImport: true };
});
const numbers = approvals.map(item => item.recipeNumber).sort((a, b) => a - b);
if (numbers.length !== 100 || numbers.some((number, index) => number !== index + 1)) throw new Error("Approval coverage is not 1–100.");
const output = {
  schemaVersion: 1,
  artifactType: "production-import-authorized",
  authorizedRecipeNumbers: numbers,
  v2ReplacementRecipeNumbers: [...v2].sort((a, b) => a - b),
  generatedFrom: ["image-preflight-report.json", "image-approval-template.json", "image-approval-v2-09-admin-approved.json"],
  approvals,
};
const destination = path.join(review, "image-approval-production-100-authorized.json");
fs.writeFileSync(destination, JSON.stringify(output, null, 2) + "\n");
console.log(JSON.stringify({ destination, authorized: approvals.length, v2Bound: v2.size }));
