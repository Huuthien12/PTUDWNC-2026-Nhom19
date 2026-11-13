param(
  [string]$Database = 'CulinaryBlogDb',
  [string]$BundleDirectory = 'seed-upgrade-production-20261112-132622'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$bundle = Join-Path $root $BundleDirectory
$report = Get-Content (Join-Path $bundle 'seed-upgrade-report/report.json') -Raw | ConvertFrom-Json
$snapshot = Get-Content (Join-Path $bundle 'snapshot/full-snapshot.json') -Raw | ConvertFrom-Json
$approvals = Get-Content (Join-Path $bundle 'approvals.json') -Raw | ConvertFrom-Json
$snapshotBySlug = @{}; $snapshot.Recipes | ForEach-Object { $snapshotBySlug[$_.Aggregate.Slug] = $_ }
$approvalByNumber = @{}; $approvals | ForEach-Object { $approvalByNumber[[int]$_.RecipeNumber] = $_ }

$sql = 'SELECT c."Id", c."Name", c."Slug", count(r."Id") FROM "Categories" c LEFT JOIN "Recipes" r ON r."CategoryId" = c."Id" GROUP BY c."Id", c."Name", c."Slug" ORDER BY c."Name";'
$categories = @(docker compose exec -T postgres psql -U postgres -d $Database -At -F '|' -c $sql | ForEach-Object {
  $parts = $_ -split '\|'; [pscustomobject]@{ Id=$parts[0]; Name=$parts[1]; Slug=$parts[2]; RecipeCount=[int]$parts[3] }
})

function Recommendation([string]$target, [object[]]$allCategories) {
  $name = switch ($target) {
    'Món Việt' { 'Món Việt'; break }
    'Món Âu' { 'Món Âu'; break }
    'Tráng Miệng' { 'Món tráng miệng'; break }
    'Hải Sản' { 'Món Hải Sản'; break }
    default { $target }
  }
  if ($target -in @('Bánh Ngọt', 'Đồ Uống')) { return [pscustomobject]@{ Action='CREATE_EXACT_CATEGORY_PENDING_APPROVAL'; Category=$null } }
  $match = @($allCategories | Where-Object Name -eq $name)
  if ($match.Count -eq 1) { return [pscustomobject]@{ Action='MAP_EXISTING_PENDING_APPROVAL'; Category=$match[0] } }
  return [pscustomobject]@{ Action='CATEGORY_REVIEW_REQUIRED'; Category=$null }
}

$review = foreach ($row in $report | Sort-Object Number) {
  $aggregate = $snapshotBySlug[$row.Slug].Aggregate
  $approval = $approvalByNumber[[int]$row.Number]
  $mapping = Recommendation $row.TargetCategory $categories
  $requiredAction = switch ($row.Status) {
    'ALREADY_UPGRADED' { 'KEEP_CONTENT_AND_CATEGORY' }
    'CONTENT_UPGRADED_CATEGORY_REMAP_REQUIRED' { 'CATEGORY_REMAP_ONLY_PENDING_APPROVAL' }
    default { 'REPLACEMENT_PENDING_APPROVAL' }
  }
  [pscustomobject]@{
    RecipeNumber=$row.Number; RecipeId=$aggregate.Id; Slug=$row.Slug
    CurrentTitle=$row.CurrentTitle; TargetTitle=$row.TargetTitle
    CurrentCategoryId=$aggregate.CategoryId; CurrentCategory=$row.CurrentCategory; TargetCategory=$row.TargetCategory
    CurrentIngredientCount=$row.IngredientCount; TargetIngredientCount=$row.TargetIngredientCount
    CurrentStepCount=$row.StepCount; TargetStepCount=$row.TargetStepCount
    AggregateSnapshotHash=$snapshotBySlug[$row.Slug].SnapshotHash; TargetCatalogHash=$approval.TargetCatalogHash
    HasLinkedImages=$row.HasImages; AnalyzerStatus=$row.Status; AnalyzerReason=$row.Reason
    RequiredProductionAction=$requiredAction
    RecommendedCategoryAction=$mapping.Action; RecommendedTargetCategoryId=$mapping.Category.Id; RecommendedTargetCategoryName=$mapping.Category.Name
    ApproveReplacement=$approval.ApproveReplacement; ApproveCategoryRemap=$approval.ApproveCategoryRemap; ApprovalTargetCategoryId=$approval.TargetCategoryId
  }
}
$review | Export-Csv (Join-Path $bundle 'production-approval-review.csv') -NoTypeInformation -Encoding utf8
$review | Where-Object { $_.AnalyzerStatus -in @('ALREADY_UPGRADED', 'CONTENT_UPGRADED_CATEGORY_REMAP_REQUIRED') } |
  Export-Csv (Join-Path $bundle 'already-upgraded-review.csv') -NoTypeInformation -Encoding utf8
$review | Group-Object RequiredProductionAction | ForEach-Object { [pscustomobject]@{ Action=$_.Name; RecipeCount=$_.Count } } |
  ConvertTo-Json | Set-Content (Join-Path $bundle 'production-action-summary.json') -Encoding utf8

$categoryReview = foreach ($target in ($review.TargetCategory | Sort-Object -Unique)) {
  $mapping = Recommendation $target $categories
  [pscustomobject]@{
    TargetCategory=$target; RecipeCount=@($review | Where-Object TargetCategory -eq $target).Count
    RecommendedAction=$mapping.Action; RecommendedTargetCategoryId=$mapping.Category.Id; RecommendedTargetCategoryName=$mapping.Category.Name
    ApprovedForProduction='NO'
  }
}
$categoryReview | Export-Csv (Join-Path $bundle 'production-category-mapping-review.csv') -NoTypeInformation -Encoding utf8
$markdown = @('# Production category-mapping review','','All production approvals remain false. Recommendations inherit fixture-tested mappings but are not production approvals.','', '| Target category | Recipes | Recommended action | Target category ID | Target category | Approved for production |','|---|---:|---|---|---|---|')
$categoryReview | ForEach-Object { $markdown += "| $($_.TargetCategory) | $($_.RecipeCount) | $($_.RecommendedAction) | $($_.RecommendedTargetCategoryId) | $($_.RecommendedTargetCategoryName) | NO |" }
Set-Content (Join-Path $bundle 'production-category-mapping-review.md') $markdown -Encoding utf8

Write-Output "reviewRows=$(@($review).Count) alreadyUpgradedRows=$(@($review | Where-Object { $_.AnalyzerStatus -in @('ALREADY_UPGRADED', 'CONTENT_UPGRADED_CATEGORY_REMAP_REQUIRED') }).Count) approvalsEnabled=$(@($review | Where-Object { $_.ApproveReplacement -or $_.ApproveCategoryRemap }).Count)"
