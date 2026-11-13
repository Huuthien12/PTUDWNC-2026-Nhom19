const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const output = path.join(root, "seed-upgrade-image-review", "image-streaming-100-smoke.json");
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

async function request(url) {
  for (;;) {
    const response = await fetch(url);
    if (response.status !== 429) return response;
    const retryAfter = Number(response.headers.get("retry-after") || 1);
    await delay(Math.max(1, retryAfter) * 1000);
  }
}

(async () => {
  const results = [];
  for (let number = 1; number <= 100; number++) {
    try {
      const detail = await request(`http://localhost:3000/api/v1/recipes/lab02-recipe-${number}`);
      if (!detail.ok) throw new Error(`detail HTTP ${detail.status}`);
      const recipe = await detail.json();
      const image = recipe.images?.find(item => item.isPrimary) ?? recipe.images?.[0];
      if (!image?.originalUrl) throw new Error("missing primary image");
      const stream = await request(`http://localhost:3000/api/v1/images?source=${encodeURIComponent(image.originalUrl)}`);
      if (!stream.ok) throw new Error(`stream HTTP ${stream.status}`);
      if (stream.headers.get("content-type") !== "image/webp") throw new Error(`unexpected ${stream.headers.get("content-type")}`);
      if ((await stream.arrayBuffer()).byteLength === 0) throw new Error("empty response body");
      results.push({ recipeNumber: number, status: "PASS" });
    } catch (error) {
      results.push({ recipeNumber: number, status: "FAIL", reason: error.message });
      break; // Preserve the first failure for diagnosis; do not hide it by continuing.
    }
    await delay(1300); // 2 requests/recipe stay within the configured general API rate limit.
  }
  const report = { checked: results.length, passed: results.filter(item => item.status === "PASS").length,
    failed: results.filter(item => item.status === "FAIL").length, results };
  fs.writeFileSync(output, JSON.stringify(report, null, 2) + "\n");
  console.log(JSON.stringify({ checked: report.checked, passed: report.passed, failed: report.failed, output }));
  if (report.failed) process.exitCode = 1;
})().catch(error => { console.error(error.message); process.exitCode = 1; });
