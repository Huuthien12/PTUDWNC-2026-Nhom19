# Lab 6 UI Inventory

> **Nguồn kiểm kê:** `main` tại `1fece1044215522fb0f3b4eb4190c572d5a71cd2`.
> Tài liệu này ghi nhận source đã có trên `main`; không coi công việc ở branch/PR chưa merge là hoàn thành.

## Quy ước trạng thái

- **Functional:** source có luồng dữ liệu hoặc mutation thực tế.
- **Static presentation:** chỉ là UI minh hoạ, không có API contract phía sau.
- **Fallback:** loading/error/not-found boundary của App Router.
- Checklist `[x]` nghĩa là source đã được xác nhận trên `main`; `[ ]` vẫn cần browser/integration demo hoặc gate cuối.

## UI route inventory

| Route | UI/Page | Access | Data | Status | Owner/Scope | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `/` | Homepage | Public | Health endpoint và `getCategories()` | Functional | Thuận planned; đã merge vào main | Hiển thị category đầu, loading/error/empty state cho danh mục. |
| `/login` | Login page | Public | `LoginForm`, local/Google auth services | Functional | Uyên form scope; đã merge | Đọc `next`/`reason` từ query string. |
| `/register` | Register page | Public | `RegisterForm` | Functional | Uyên form scope; đã merge | Đăng ký và thiết lập session theo auth service. |
| `/profile` | Profile page | Authenticated | `ProfileForm`, `GET/PATCH /api/v1/auth/me` | Functional | Thiện profile/integration scope; đã merge | Chỉ cho chỉnh sửa trường profile được service hỗ trợ. |
| `/categories` | Category list | Public | `getCategories()` | Functional | Thuận category/public scope; đã merge | Có loading, retry/error và empty state. |
| `/categories/[slug]` | Category detail and recipe list | Public; backend quyết định visibility | `getCategoryBySlug(slug, page, 12)` | Functional | Thuận category/public scope; đã merge | Có pagination; `notFound()` khi API trả 404. |
| `/recipes` | Recipe list/search | Public; backend quyết định visibility | `RecipeListClient`, recipe/search services | Functional | Thái recipe UI scope; đã merge | Đọc `q` và `page`; có search/pagination states. |
| `/recipes/[slug]` | Recipe detail | Public; owner/admin controls khi session cho phép | `getRecipeBySlug(slug)`, lifecycle/ingredient controls | Functional | Thái recipe UI scope; đã merge | Có loading, missing, error/retry và detail ingredients/steps/images. |
| `/recipes/create` | Create recipe | Author or Admin | `CreateRecipeForm` | Functional | Thái recipe form scope; đã merge | `RequireAuth` bảo vệ client-side; backend vẫn là authority. |
| `/recipes/[slug]/edit` | Edit recipe | Author or Admin | `EditRecipeForm` | Functional | Thái recipe form/child editor scope; đã merge | Form quản lý RowVersion và editor child theo implementation hiện tại. |
| `/recipes/dashboard` | Author recipe dashboard | Author or Admin | `AuthorRecipeDashboard` / recipe services | Functional | Thiện dashboard scope; đã merge | Load recipe detail, lifecycle actions, delete, 422 reload. |
| `/admin/categories` | Category admin | Admin | `CategoryManager`, category service | Functional | Team category/admin scope; đã merge | Được bảo vệ bởi admin layout; có loading/error/retry. |
| `/admin/users` | Admin users presentation | Admin | `StaticUserList` local sample rows | Static presentation | Thiện admin-static scope; đã merge | Chưa có Admin user-list API. |
| `/unauthorized` | Unauthorized page | Public fallback route | Không có API | Functional static page | Shared/error scope; đã merge | Có thể truy cập trực tiếp; `RequireAuth` hiện hiển thị alert inline khi sai role. |
| `error.tsx` | Root error fallback | App Router boundary | Không có API | Fallback | Shared/error scope; đã merge | Thông báo an toàn, retry hoặc về homepage; không render server error detail. |
| `not-found.tsx` | Root 404 fallback | App Router boundary | Không có API | Fallback | Shared/error scope; đã merge | Link về homepage. |
| `loading.tsx` | Root loading fallback | App Router boundary | Không có API | Fallback | Shared/public scope; đã merge | `role="status"`. |
| `recipes/error.tsx` | Recipe route error fallback | App Router boundary | Không có API | Fallback | Recipe UI scope; đã merge | Nút `reset()` để thử lại. |
| `recipes/loading.tsx` | Recipe route loading fallback | App Router boundary | Không có API | Fallback | Recipe UI scope; đã merge | `role="status"`. |
| `categories/loading.tsx` | Category loading fallback | App Router boundary | Không có API | Fallback | Category/public scope; đã merge | Route-level loading UI. |
| `categories/[slug]/loading.tsx` | Category detail loading fallback | App Router boundary | Không có API | Fallback | Category/public scope; đã merge | Route-level loading UI. |
| `categories/[slug]/not-found.tsx` | Category 404 fallback | App Router boundary | Không có API | Fallback | Category/public scope; đã merge | Reuses `ContentState`. |
| `admin/categories/loading.tsx` | Admin category loading fallback | App Router boundary | Không có API | Fallback | Category/admin scope; đã merge | Re-exports shared `CategoryLoading`. |

`app/api/recipe-cache/[...path]/route.ts` là API proxy technical route, không phải UI page; vì vậy không nằm trong bảng route UI.

## Component inventory

| Component | Responsibility | Used by | Status | Owner/Notes |
| --- | --- | --- | --- | --- |
| `Header` | Global navigation and search entry | Root layout | Functional | Thái header scope; merged current main. |
| `Footer` | Public navigation/footer links | Root layout | Functional | Thuận footer scope; merged current main. |
| `QueryProvider` | TanStack Query client and cache clear when session identity changes | Root layout | Functional | Thiện foundation scope; holds server-state only. |
| `RequireAuth` | Client-side session/role gate and login redirect | Protected pages/admin layout | Functional | Auth integration; backend authorization remains authoritative. |
| `AuthLifecycle` / `useSession` | Expiry redirect and external session subscription | Root layout/auth consumers | Functional | Thiện auth-state integration; uses `useSyncExternalStore`. |
| `LoginForm` / `RegisterForm` | Auth form UI and session establishment | Login/register pages | Functional | Uyên auth-form scope; merged current main. |
| `ProfileForm` | Load/update current profile, validation and user feedback | `/profile` | Functional | Thiện profile scope; merged current main. |
| `AuthControls` | Header auth controls and logout | `Header` | Functional | Current merged auth UI; role-aware UX only. |
| `AuthorRecipeDashboard` | Author list, lifecycle actions and delete | `/recipes/dashboard` | Functional | Thiện dashboard scope; merged current main. |
| `CreateRecipeForm` / `EditRecipeForm` | Recipe create/update form and mutation state | Recipe create/edit routes | Functional | Thái recipe-form scope; merged current main. |
| `IngredientEditor` | Ingredient CRUD/order interface | Recipe detail/edit integration | Functional | Uyên ingredient scope; merged current main. |
| `RecipeStepManager` | Step CRUD/order interface | Recipe edit integration | Functional | Thái step scope; merged current main. |
| `RecipeListClient` / `RecipeCard` / `RecipePagination` | Recipe list, filters/search UI and pagination | `/recipes`, homepage-related lists | Functional | Recipe/list scope; merged current main. |
| `CategoryList` / `CategoryCard` / `CategoryPagination` / `CategoryRecipeCard` | Category cards, category recipe list and pagination | Category routes | Functional | Thuận category/public scope; merged current main. |
| `SearchBar` / `SearchPagination` | Query input and pagination controls | Header/recipe list | Functional | Thuận search scope; merged current main. |
| `CategoryManager` / `CategoryForm` | Admin category mutation UI | `/admin/categories` | Functional | Category/admin scope; merged current main. |
| `StaticUserList` | Non-mutating user-management presentation | `/admin/users` | Static presentation | Thiện admin-static scope; intentionally has no API request. |
| `ContentState` / `RetryButton` | Reusable error, empty and retry views | Category/recipe/shared routes | Functional | Shared UI; merged current main. |
| `CategoryLoading` | Shared category loading state | Category route boundaries | Functional | Shared/public UI; merged current main. |
| `RecipeImage` / `CulinaryIllustration` | Safe recipe-image and decorative rendering | Recipe/category/home UI | Functional | Shared visual UI; merged current main. |

## Foundation inventory

### API client and error contract

- `apiClient` attaches the Bearer access token only for authenticated calls.
- A `401` performs refresh and retries the original request **at most once**; a repeated `401` expires the local session. `403`, network and business errors do not trigger refresh.
- `httpClient` parses bounded RFC 7807-shaped payloads into `ApiProblem`; the HTTP transport status is authoritative rather than a body-provided status.
- `429` exposes a parsed `Retry-After` value for delta-seconds or HTTP-date headers. The client does not automatically retry mutations.
- Network, timeout and caller abort are normalized as `ApiError` with status `0` and a distinct `kind` (`network`, `timeout`, or `aborted`).
- API calls may use the internal recipe-cache proxy route for cache policy; it remains technical infrastructure rather than a UI route.

### Auth and session state

- `auth-session` is the sole browser session store. `useSession` subscribes through `useSyncExternalStore`.
- `QueryProvider` owns TanStack Query server state, not auth/session state; it clears query cache when `sessionId` changes.
- The current contract persists the refresh token in `localStorage` as part of the serialized session and coordinates refresh/logout with Web Locks. This is a documented risk, not an HttpOnly-cookie implementation.
- Refresh, logout and expiry notify consumers through the existing session events; no parallel token store is documented here.

### TypeScript contracts

- Shared API types: `ApiProblem`, `ApiErrorKind`, and `PaginatedResult<T>` in `types/api.ts`.
- Auth/profile types: `AuthUser`, `AuthResponse`, `AuthSession`, `UserProfile`, and `UpdateProfileRequest`.
- Domain-facing UI types: category/detail pagination, recipe/detail/summary/nutrition/image, and search result/page contracts.
- `RecipeIngredient` and `RecipeStep` are concrete members of the current recipe types, including actual order/timer/RowVersion fields; no speculative additional child contract is claimed.

## Lab 6 ownership and merge state

| Planned owner | Planned focus | Current-main interpretation |
| --- | --- | --- |
| Uyên | Auth UI and form-related scope | Existing auth forms are merged; this inventory does not claim unmerged auth work as done. |
| Thái | Header, recipe UI, recipe form, steps and ingredients | Existing header/recipe/editor components are merged; later member work still requires integration review. |
| Thuận | Footer, category, search, homepage, shared/public UI | Existing footer/category/search/home/shared UI is merged; pending work must not be inferred from this documentation. |
| Thiện | API client, TS types, auth state, profile, dashboard, admin static UI, error states, integration and documentation | The documented foundation/profile/dashboard/admin-static/error work exists in current main. |

## Demo checklist

- [x] Homepage source and category/health states verified in current main.
- [x] Login and register UI source verified in current main.
- [x] Authenticated profile page and profile form source verified in current main.
- [x] Recipe list, detail, create and edit route source verified in current main.
- [x] Author dashboard and lifecycle/delete UI source verified in current main.
- [x] Category list/detail/admin category route source verified in current main.
- [x] Search UI source verified in current main.
- [x] Admin users static presentation source verified in current main.
- [x] Unauthorized, 404, root/recipe/category loading and error fallback source verified in current main.
- [x] Shared `ContentState` and retry UI source verified in current main.
- [ ] Responsive navigation browser demo verification.
- [ ] End-to-end loading/error/empty state browser verification against running services.
- [ ] No console-breaking error verification in a clean browser session.
- [ ] Final production build/CI gate verification for the integrated release.

## Known limitations

- **Admin users is presentation-only.** There is no backend Admin user-list API; `StaticUserList` uses local example rows and disabled controls.
- **`/unauthorized` is not used by `RequireAuth` for role mismatch.** The current guard renders an inline access-denied alert for that case.
- **Refresh token storage follows the present localStorage contract.** Moving to an HttpOnly cookie requires a coordinated backend/auth-contract change and is not claimed as implemented.
- **`frontend/frontend/` is a local untracked artifact.** It is not Lab 6 source and must not be committed.
- **H05 integration is pending where member deliverables are not yet merged/ready.** This inventory intentionally treats only current-main source as complete.

## H05 Integration Readiness

Thiện's completed foundation relevant to integration is documented here: shared API/error handling, TS contracts, external auth session state, profile, author dashboard, admin static presentation, route fallbacks, and this inventory.

Integration is still contingent on the relevant member PR/work being merged or explicitly ready. Resolve semantic overlap only after that point, especially in these conflict-sensitive files/areas:

- `frontend/src/app/layout.tsx`
- `frontend/src/app/globals.css`
- `frontend/src/components/Header.tsx` and `frontend/src/components/layout/footer.tsx`
- shared UI components
- auth forms and session consumers

The integration pass must preserve the single auth/session source of truth, existing API-error behavior, shared RowVersion/mutation coordination in recipe editors, and cache/revalidation behavior. No future branch is marked complete by this document before it is merged into `main`.
