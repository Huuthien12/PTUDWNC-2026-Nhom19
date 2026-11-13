param(
  [string]$Database = 'CulinaryBlogUpgradeTest',
  [string]$OutputDirectory = 'seed-upgrade-review'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force -Path $output | Out-Null
$report = Get-Content (Join-Path $root 'seed-upgrade-report-test-guarded/report.json') -Raw | ConvertFrom-Json
$snapshot = Get-Content (Join-Path $root 'seed-upgrade-phase2-snapshot/full-snapshot.json') -Raw | ConvertFrom-Json
$approvals = Get-Content (Join-Path $root 'seed-upgrade-phase2-approvals.json') -Raw | ConvertFrom-Json
$validation = Get-Content (Join-Path $root 'seed-upgrade-phase2-approval-validation.json') -Raw | ConvertFrom-Json
$sql = 'SELECT c."Id", c."Name", c."Slug", COUNT(r."Id") FROM "Categories" c LEFT JOIN "Recipes" r ON r."CategoryId"=c."Id" AND NOT r."IsDeleted" GROUP BY c."Id",c."Name",c."Slug" ORDER BY c."Name";'
$categoryRows = docker exec -u postgres ptudwnc-2026-nhom19-postgres-1 psql -d $Database -At -F '|' -c $sql
$categories = @($categoryRows | Where-Object { $_ -and $_ -match '\|' } | ForEach-Object { $p = $_ -split '\|'; [pscustomobject]@{ Id=$p[0]; Name=$p[1]; Slug=$p[2]; RecipeCount=[int]$p[3] } })
function Normalize([string]$Value) { (($Value.Normalize([Text.NormalizationForm]::FormD).ToCharArray() | Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark }) -join '').Replace(' ','').ToLowerInvariant() }
function Cell([object]$Value) { ([string]$Value).Replace('|','\|').Replace("`r",' ').Replace("`n",' ') }
function CandidateText([string]$Target) {
  $exact = @($categories | Where-Object { (Normalize $_.Name) -eq (Normalize $Target) })
  $semantic = switch ($Target) {
    'Bánh Ngọt' { @($categories | Where-Object Name -eq 'Món tráng miệng') }
    'Tráng Miệng' { @($categories | Where-Object Name -eq 'Món tráng miệng') }
    'Hải Sản' { @($categories | Where-Object Name -eq 'Món Hải Sản') }
    default { @() }
  }
  @($exact + $semantic | Sort-Object Id -Unique | ForEach-Object { "$($_.Id) [$($_.Name), recipes=$($_.RecipeCount)]" }) -join '; '
}
function Action([string]$Target) {
  switch ($Target) {
    'Món Việt' { return @('ADMIN_SELECT_ONE_EXISTING_EXACT_CATEGORY','Two exact normalized candidates exist; compare ownership and recipe counts, then approve exactly one ID.') }
    'Món Âu' { return @('ADMIN_SELECT_ONE_EXISTING_EXACT_CATEGORY','Two exact normalized candidates exist; compare ownership and recipe counts, then approve exactly one ID.') }
    'Bánh Ngọt' { return @('CREATE_EXACT_CATEGORY_OR_APPROVE_SEMANTIC_MAP','No exact category. Creating the catalog category preserves taxonomy; mapping to Món tráng miệng is an explicit semantic alternative.') }
    'Tráng Miệng' { return @('CREATE_EXACT_CATEGORY_OR_APPROVE_SEMANTIC_MAP','No exact category. Creating the catalog category preserves taxonomy; mapping to Món tráng miệng is an explicit semantic alternative.') }
    'Đồ Uống' { return @('CREATE_EXACT_CATEGORY','No exact or close existing category was found.') }
    'Hải Sản' { return @('CREATE_EXACT_CATEGORY_OR_APPROVE_SEMANTIC_MAP','No exact category. Mapping to Món Hải Sản is an explicit semantic alternative.') }
    default { return @('ADMIN_REVIEW','Unexpected category conflict.') }
  }
}

$snapshotBySlug = @{}; $snapshot.Recipes | ForEach-Object { $snapshotBySlug[$_.Aggregate.Slug] = $_ }
$approvalByNumber = @{}; $approvals | ForEach-Object { $approvalByNumber[$_.RecipeNumber] = $_ }
$conflictNumbers = @($validation | Where-Object Status -eq 'Conflict' | ForEach-Object RecipeNumber)
$categoryReview = foreach ($number in $conflictNumbers) {
  $row = $report | Where-Object Number -eq $number
  $aggregate = $snapshotBySlug[$row.Slug].Aggregate
  $current = $categories | Where-Object Id -eq $aggregate.CategoryId
  $action = Action $row.TargetCategory
  [pscustomobject]@{
    RecipeNumber=$number; RecipeTitle=$row.TargetTitle; TargetCategory=$row.TargetCategory
    CurrentCategoryId=$aggregate.CategoryId; CurrentCategoryName=$current.Name
    SimilarCategories=(CandidateText $row.TargetCategory)
    ConflictReason='Target category is missing or not unique after accent-insensitive normalization.'
    ValidOptions='Select one existing exact category; create exact catalog category; or explicitly approve a semantic existing-category mapping where listed.'
    RecommendedAction=$action[0]; RecommendationReason=$action[1]; ApprovedAction=''
  }
}
$categoryReview | Export-Csv (Join-Path $output 'category-mapping-review.csv') -NoTypeInformation -Encoding utf8
$categoryMarkdown = @('# Category mapping review','','Generated from the isolated PostgreSQL test database. `ApprovedAction` is intentionally blank.','', '| # | Recipe | Target | Current category | Similar existing categories | Recommended action | Reason | ApprovedAction |','|---:|---|---|---|---|---|---|---|')
$categoryReview | ForEach-Object { $categoryMarkdown += "| $($_.RecipeNumber) | $(Cell $_.RecipeTitle) | $(Cell $_.TargetCategory) | $(Cell "$($_.CurrentCategoryId) [$($_.CurrentCategoryName)]") | $(Cell $_.SimilarCategories) | $($_.RecommendedAction) | $(Cell $_.RecommendationReason) |  |" }
Set-Content (Join-Path $output 'category-mapping-review.md') $categoryMarkdown -Encoding utf8

$approvalReview = foreach ($row in $report | Sort-Object Number) {
  $aggregate = $snapshotBySlug[$row.Slug].Aggregate; $approval = $approvalByNumber[$row.Number]
  [pscustomobject]@{
    RecipeNumber=$row.Number; RecipeId=$aggregate.Id; Slug=$row.Slug; CurrentTitle=$row.CurrentTitle; TargetTitle=$row.TargetTitle
    CurrentCategoryId=$aggregate.CategoryId; CurrentCategory=$row.CurrentCategory; TargetCategory=$row.TargetCategory
    CurrentIngredientCount=$row.IngredientCount; TargetIngredientCount=$row.TargetIngredientCount; CurrentStepCount=$row.StepCount; TargetStepCount=$row.TargetStepCount
    SnapshotHash=$snapshotBySlug[$row.Slug].SnapshotHash; TargetCatalogHash=$approval.TargetCatalogHash; HasLinkedImages=$row.HasImages
    AnalyzerStatus=$row.Status; ApprovalStatus="ApproveReplacement=$($approval.ApproveReplacement); ApproveCategoryRemap=$($approval.ApproveCategoryRemap)"
    ReviewNote='Legacy Bogus seed lacks an immutable original aggregate hash; replacement requires explicit administrator approval bound to this snapshot and target hash.'
  }
}
$approvalReview | Export-Csv (Join-Path $output 'recipe-approval-review.csv') -NoTypeInformation -Encoding utf8
$approvalMarkdown = @('# Recipe approval review','','All 100 records are legacy seed records. Every approval remains false.','', '| # | Current → target title | Current → target category | Ingredients | Steps | Image | Snapshot hash | Target hash | Approval |','|---:|---|---|---:|---:|---|---|---|---|')
$approvalReview | ForEach-Object { $approvalMarkdown += "| $($_.RecipeNumber) | $(Cell "$($_.CurrentTitle) → $($_.TargetTitle)") | $(Cell "$($_.CurrentCategory) → $($_.TargetCategory)") | $($_.CurrentIngredientCount) → $($_.TargetIngredientCount) | $($_.CurrentStepCount) → $($_.TargetStepCount) | $($_.HasLinkedImages) | $($_.SnapshotHash) | $($_.TargetCatalogHash) | $(Cell $_.ApprovalStatus) |" }
Set-Content (Join-Path $output 'recipe-approval-review.md') $approvalMarkdown -Encoding utf8
