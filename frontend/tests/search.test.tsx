import assert from "node:assert/strict";
import { after, afterEach, test } from "node:test";
import type { ReactElement } from "react";
import { act, create, type ReactTestInstance, type ReactTestRenderer } from "react-test-renderer";
import { AppRouterContext } from "next/dist/shared/lib/app-router-context.shared-runtime";
import Home from "../src/app/page";
import Loading from "../src/app/loading";
import sitemap from "../src/app/sitemap";
import RecipesPage from "../src/app/recipes/page";
import RecipeListClient from "../src/components/recipes/RecipeListClient";
import SearchPagination from "../src/components/search/SearchPagination";
import RecipePagination from "../src/components/recipes/RecipePagination";
import RecipeImage from "../src/components/shared/RecipeImage";
import ContentState from "../src/components/shared/ContentState";
import { buildRecipeJsonLd, buildRecipeMetadata } from "../src/services/recipe-seo";
import { searchRecipes } from "../src/services/search-service";
import { clearRecipeRequests } from "../src/services/recipe-requests";
import type { RecipeSearchPage } from "../src/types/search";

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
const originalFetch = globalThis.fetch;
const originalSelf = Object.getOwnPropertyDescriptor(globalThis, "self");
let view: ReactTestRenderer | undefined;
const emptyPage: RecipeSearchPage = { items: [], totalCount: 0, page: 1, pageSize: 12, totalPages: 0 };
const router = {
  back() {}, forward() {}, refresh() {}, hmrRefresh() {}, push() {}, replace() {}, prefetch() {},
  bfcacheId: "test",
};

Object.defineProperty(globalThis, "self", { configurable: true, value: globalThis });

after(() => {
  if (originalSelf) Object.defineProperty(globalThis, "self", originalSelf);
  else Reflect.deleteProperty(globalThis, "self");
});

afterEach(async () => {
  if (view) await act(async () => { view!.unmount(); });
  view = undefined;
  globalThis.fetch = originalFetch;
  clearRecipeRequests();
});

function text(node: ReactTestInstance): string {
  return node.children.map((child) => typeof child === "string" ? child : text(child)).join("");
}

function withRouter(node: ReactElement) {
  return <AppRouterContext.Provider value={router}>{node}</AppRouterContext.Provider>;
}

async function render(node: ReactElement) {
  await act(async () => {
    view = create(withRouter(node));
    await new Promise<void>((resolve) => setImmediate(resolve));
  });
}

test("search query sends encoded q, page and pageSize", async () => {
  let requested = "";
  globalThis.fetch = async (url) => {
    requested = String(url);
    return Response.json(emptyPage);
  };
  await searchRecipes("phở & bò", 3, 6);
  const url = new URL(requested);
  assert.equal(url.pathname, "/api/v1/recipes/search");
  assert.equal(url.searchParams.get("q"), "phở & bò");
  assert.equal(url.searchParams.get("page"), "3");
  assert.equal(url.searchParams.get("pageSize"), "6");
});

test("recipes page ignores unsupported one-character queries and invalid pages", async () => {
  const page = await RecipesPage({ searchParams: Promise.resolve({ q: " x ", page: "0" }) });
  const list = page.props.children[1];
  assert.equal(list.props.query, "");
  assert.equal(list.props.page, 1);
});

test("recipes page parses supported filters and ignores empty cook time", async () => {
  const page = await RecipesPage({ searchParams: Promise.resolve({
    page: "3",
    categoryId: "11111111-1111-1111-1111-111111111111",
    difficulty: "2",
    maxCookTime: "30",
    minServings: "2",
    sort: "title-asc",
  }) });
  const list = page.props.children[1];
  assert.equal(list.props.page, 3);
  assert.deepEqual(list.props.filters, {
    categoryId: "11111111-1111-1111-1111-111111111111",
    difficulty: 2,
    maxCookTime: 30,
    minServings: 2,
    sortBy: "title",
    sortOrder: "asc",
  });

  const emptyFilterPage = await RecipesPage({ searchParams: Promise.resolve({ maxCookTime: "" }) });
  assert.equal(emptyFilterPage.props.children[1].props.filters.maxCookTime, undefined);
});

test("recipes page ignores repeated URL parameters and prototype property difficulty names", async () => {
  const page = await RecipesPage({ searchParams: Promise.resolve({
    q: ["pho", "bo"], maxCookTime: ["10", "20"], difficulty: "constructor",
  }) });
  const list = page.props.children[1];
  assert.equal(list.props.query, "");
  assert.equal(list.props.filters.maxCookTime, undefined);
  assert.equal(list.props.filters.difficulty, undefined);
});

test("recipe list does not refetch when only the filters object identity changes", async () => {
  let requests = 0;
  globalThis.fetch = async (url) => {
    if (String(url).includes("/categories")) return Response.json([]);
    requests++;
    return Response.json(emptyPage);
  };
  await render(<RecipeListClient page={1} filters={{ difficulty: 2 }} />);
  await act(async () => {
    view!.update(withRouter(<RecipeListClient page={1} filters={{ difficulty: 2 }} />));
    await new Promise<void>((resolve) => setImmediate(resolve));
  });
  assert.equal(requests, 1);
});

test("search pagination retains the query and exposes previous, page and next links", async () => {
  await render(<SearchPagination query="phở & bò" currentPage={3} totalPages={5} />);
  const hrefs = view!.root.findAll((node) => typeof node.props.href === "string")
    .map((node) => node.props.href as string);
  assert.ok(hrefs.includes("/recipes?q=ph%E1%BB%9F%20%26%20b%C3%B2&page=2"));
  assert.ok(hrefs.includes("/recipes?q=ph%E1%BB%9F%20%26%20b%C3%B2&page=3"));
  assert.ok(hrefs.includes("/recipes?q=ph%E1%BB%9F%20%26%20b%C3%B2&page=4"));
});

test("recipe pagination retains active filters when changing pages", async () => {
  await render(<RecipePagination currentPage={2} totalPages={4} filters={{
    categoryId: "11111111-1111-1111-1111-111111111111",
    difficulty: 2,
    maxCookTime: 30,
    minServings: 2,
    sortBy: "title",
    sortOrder: "asc",
  }} />);
  const hrefs = view!.root.findAll((node) => typeof node.props.href === "string")
    .map((node) => node.props.href as string);
  assert.ok(hrefs.some((href) => {
    const url = new URL(href, "http://localhost");
    return url.searchParams.get("page") === "1" &&
      url.searchParams.get("categoryId") === "11111111-1111-1111-1111-111111111111" &&
      url.searchParams.get("difficulty") === "2" &&
      url.searchParams.get("maxCookTime") === "30" &&
      url.searchParams.get("minServings") === "2" &&
      url.searchParams.get("sortBy") === "title" &&
      url.searchParams.get("sortOrder") === "asc";
  }));
});

test("ContentState asChild action renders one accessible link without a nested button", async () => {
  await render(<ContentState headingAs="h2" title="Không có dữ liệu" description="Quay lại danh sách."
    href="/recipes" action="Xem công thức" />);

  const link = view!.root.findByType("a");
  assert.equal(link.type, "a");
  assert.equal(link.props.href, "/recipes");
  assert.equal(view!.root.findByType("section").findAllByType("a").length, 1);
  assert.equal(view!.root.findAllByType("button").length, 0);
  assert.equal(view!.root.findByType("h2").children.join(""), "Không có dữ liệu");
});

test("recipe image uses responsive thumbnail and medium sources with next/image fallback", async () => {
  await render(<RecipeImage
    src="https://storage.test/original.jpg"
    mediumSrc="https://storage.test/medium.jpg"
    thumbnailSrc="https://storage.test/thumbnail.jpg"
    title="Canh chua"
  />);
  const sources = view!.root.findAllByType("source");
  assert.deepEqual(sources.map((source) => [source.props.media, source.props.srcSet]), [
    ["(max-width: 400px)", "https://storage.test/thumbnail.jpg"],
    [undefined, "https://storage.test/medium.jpg"],
  ]);
  assert.equal(view!.root.findByType("img").props.src, "https://storage.test/original.jpg");
});

test("recipe image removes only the failed variant and restores variants for a new image", async () => {
  const src = "https://storage.test/original.jpg";
  const mediumSrc = "https://storage.test/medium.jpg";
  const thumbnailSrc = "https://storage.test/thumbnail.jpg";
  await render(<RecipeImage src={src} mediumSrc={mediumSrc} thumbnailSrc={thumbnailSrc} title="Pho" />);
  await act(async () => {
    view!.root.findByType("img").props.onError({ currentTarget: { src, currentSrc: thumbnailSrc } });
  });
  assert.deepEqual(view!.root.findAllByType("source").map((source) => source.props.srcSet), [mediumSrc]);

  await act(async () => {
    view!.update(<RecipeImage src="https://storage.test/new-original.jpg"
      mediumSrc="https://storage.test/new-medium.jpg" thumbnailSrc="https://storage.test/new-thumbnail.jpg" title="Pho" />);
  });
  assert.equal(view!.root.findAllByType("source").length, 2);
});

test("medium failure keeps the thumbnail available after a viewport change", async () => {
  const src = "https://storage.test/original.jpg";
  const mediumSrc = "https://storage.test/medium.jpg";
  const thumbnailSrc = "https://storage.test/thumbnail.jpg";
  await render(<RecipeImage src={src} mediumSrc={mediumSrc} thumbnailSrc={thumbnailSrc} title="Pho" />);
  await act(async () => {
    view!.root.findByType("img").props.onError({ currentTarget: { src, currentSrc: mediumSrc } });
  });
  const remaining = view!.root.findAllByType("source");
  assert.equal(remaining.length, 1);
  assert.equal(remaining[0].props.srcSet, thumbnailSrc);
  assert.equal(remaining[0].props.media, "(max-width: 400px)");
  assert.equal(view!.root.findByType("img").props.src, src);
});

test("failed variants use the original and show a placeholder only if the original fails", async () => {
  const src = "https://storage.test/original.jpg";
  const mediumSrc = "https://storage.test/medium.jpg";
  const thumbnailSrc = "https://storage.test/thumbnail.jpg";
  await render(<RecipeImage src={src} mediumSrc={mediumSrc} thumbnailSrc={thumbnailSrc} title="Pho" />);
  for (const currentSrc of [thumbnailSrc, mediumSrc]) {
    await act(async () => {
      view!.root.findByType("img").props.onError({ currentTarget: { src, currentSrc } });
    });
  }
  assert.equal(view!.root.findAllByType("source").length, 0);
  assert.equal(view!.root.findByType("img").props.src, src);
  await act(async () => {
    view!.root.findByType("img").props.onError({ currentTarget: { src, currentSrc: src } });
  });
  assert.equal(view!.root.findAllByType("img").length, 0);
  assert.match(text(view!.root), /Góc bếp/);
});

test("recipe search shows loading then an empty state", async () => {
  let resolveFetch!: (response: Response) => void;
  globalThis.fetch = () => new Promise<Response>((resolve) => { resolveFetch = resolve; });
  act(() => { view = create(withRouter(<RecipeListClient page={1} query="phở" />)); });
  assert.match(text(view!.root), /Đang tải công thức/);
  await act(async () => {
    resolveFetch(Response.json(emptyPage));
    await new Promise<void>((resolve) => setImmediate(resolve));
  });
  assert.match(text(view!.root), /Không tìm thấy món ăn phù hợp/);
});

test("recipe search shows a safe error state when the API fails", async () => {
  globalThis.fetch = async () => new Response(null, { status: 503 });
  await render(<RecipeListClient page={1} query="phở" />);
  assert.match(text(view!.root), /Chưa thể tải công thức/);
  assert.doesNotMatch(text(view!.root), /503/);
});

test("a successful revisit clears the stale failure for the same request key", async () => {
  let pageOneRequests = 0;
  globalThis.fetch = async (url) => {
    const page = new URL(String(url)).searchParams.get("page");
    if (page === "1" && ++pageOneRequests === 1) return new Response(null, { status: 503 });
    return Response.json({
      items: [{ id: `recipe-${page}`, title: `Recipe page ${page}`, slug: `recipe-${page}`,
        description: "Loaded", prepTime: 10, cookTime: 20, servings: 2, difficulty: 1,
        thumbnailUrl: null, publishedAt: null, relevanceScore: 1 }],
      totalCount: 24, page: Number(page), pageSize: 12, totalPages: 2,
    });
  };

  await render(<RecipeListClient page={1} query="phở" />);
  assert.match(text(view!.root), /Chưa thể tải công thức/);
  await act(async () => {
    view!.update(withRouter(<RecipeListClient page={2} query="phở" />));
    await new Promise<void>((resolve) => setImmediate(resolve));
  });
  assert.match(text(view!.root), /Recipe page 2/);
  await act(async () => {
    view!.update(withRouter(<RecipeListClient page={1} query="phở" />));
    await new Promise<void>((resolve) => setImmediate(resolve));
  });

  assert.equal(pageOneRequests, 2);
  assert.match(text(view!.root), /Recipe page 1/);
  assert.doesNotMatch(text(view!.root), /Chưa thể tải công thức/);
});

for (const query of ["", "phở & bò"]) {
  test(`${query ? "search" : "recipe list"} retry requests the same page and renders recovered results`, async () => {
    const requested: string[] = [];
    let resolveRetry!: (response: Response) => void;
    clearRecipeRequests();
    globalThis.fetch = async (url) => {
      if (String(url).includes("/categories")) return Response.json([]);
      requested.push(String(url));
      if (requested.length === 1) return new Response(null, { status: 503 });
      return new Promise<Response>((resolve) => { resolveRetry = resolve; });
    };
    await render(<RecipeListClient page={2} query={query} />);
    assert.match(text(view!.root), /Chưa thể tải công thức/);
    const retry = view!.root.findAllByType("button").find((button) => button.props.className === "cb-button cb-button-secondary");
    assert.ok(retry);
    assert.equal(retry.props.type, "button");
    assert.equal(retry.parent!.type, "section");
    await act(async () => { retry.props.onClick(); });
    assert.equal(requested.length, 2);
    assert.equal(requested[0], requested[1]);
    const url = new URL(requested[1]);
    assert.equal(url.pathname, query ? "/api/v1/recipes/search" : "/api/v1/recipes");
    assert.equal(url.searchParams.get("page"), "2");
    assert.equal(url.searchParams.get("pageSize"), "12");
    assert.equal(url.searchParams.get("q"), query || null);
    assert.match(text(view!.root.findByProps({ role: "status" })), /Đang tải công thức/);
    await act(async () => {
      resolveRetry(Response.json({ items: [{ id: "recovered", title: "Recovered recipe", slug: "recovered",
        description: "Loaded after retry", prepTime: 10, cookTime: 20, servings: 2, difficulty: 1,
        thumbnailUrl: null, publishedAt: null, relevanceScore: 1 }],
      totalCount: 36, page: 2, pageSize: 12, totalPages: 3 }));
      await new Promise<void>((resolve) => setImmediate(resolve));
    });
    assert.equal(requested.length, 2);
    assert.match(text(view!.root), /Recovered recipe/);
    assert.doesNotMatch(text(view!.root), /Chưa thể tải công thức|Đang tải công thức/);
    const hrefs = view!.root.findAll((node) => typeof node.props.href === "string")
      .map((node) => node.props.href as string);
    assert.ok(hrefs.includes("/recipes/recovered"));
    assert.ok(hrefs.includes(query ? `/recipes?q=${encodeURIComponent(query)}&page=3` : "/recipes?page=3"));
  });
}

test("homepage category failure never invents category names or slugs", async () => {
  globalThis.fetch = async () => new Response(null, { status: 503 });
  await render(await Home());
  assert.match(text(view!.root), /Chưa thể tải danh mục/);
  for (const fake of ["Món Việt", "Bữa sáng", "Món chay", "Tráng miệng", "mon-viet", "bua-sang", "mon-chay", "trang-mieng"]) {
    assert.ok(!JSON.stringify(view!.toJSON()).includes(fake));
  }
});

test("homepage renders an explicit empty category state", async () => {
  globalThis.fetch = async (url) => String(url).includes("/categories")
    ? Response.json([])
    : Response.json(emptyPage);
  await render(await Home());
  assert.match(text(view!.root), /Chưa có danh mục để hiển thị/);
  assert.doesNotMatch(text(view!.root), /Món Việt/);
});

test("homepage requests and renders the six latest recipes in API order", async () => {
  let recipeRequest = "";
  globalThis.fetch = async (url) => {
    if (String(url).includes("/categories")) return Response.json([]);
    recipeRequest = String(url);
    return Response.json({
      items: ["Mới nhất", "Mới thứ hai"].map((title, index) => ({
        id: `latest-${index}`, slug: `latest-${index}`, title, description: "Món mới",
        prepTime: 10, cookTime: 20, servings: 2, difficulty: 1,
        thumbnailUrl: null, mediumUrl: null, publishedAt: null,
      })),
      totalCount: 2, page: 1, pageSize: 6, totalPages: 1,
    });
  };

  await render(<Home />);

  const url = new URL(recipeRequest);
  assert.equal(url.pathname, "/api/v1/recipes");
  assert.equal(url.searchParams.get("page"), "1");
  assert.equal(url.searchParams.get("pageSize"), "6");
  assert.equal(url.searchParams.get("sortBy"), "createdAt");
  assert.equal(url.searchParams.get("sortOrder"), "desc");
  assert.ok(text(view!.root).indexOf("Mới nhất") < text(view!.root).indexOf("Mới thứ hai"));
  assert.doesNotMatch(text(view!.root), /Trạng thái hệ thống/);
});

test("homepage keeps categories when latest recipes fail", async () => {
  globalThis.fetch = async (url) => String(url).includes("/categories")
    ? Response.json([{ id: "category", name: "Món Việt", slug: "mon-viet", description: null, recipeCount: 1 }])
    : new Response(null, { status: 503 });

  await render(<Home />);

  assert.match(text(view!.root), /Món Việt/);
  assert.match(text(view!.root), /Chưa thể tải công thức mới/);
});

test("homepage renders an explicit empty latest-recipes state", async () => {
  globalThis.fetch = async (url) => String(url).includes("/categories")
    ? Response.json([])
    : Response.json(emptyPage);

  await render(<Home />);

  assert.match(text(view!.root), /Chưa có công thức để hiển thị/);
});

test("homepage has a route loading state", async () => {
  await render(<Loading />);
  assert.match(text(view!.root), /Đang tải trang chủ/);
  assert.equal(view!.root.findByProps({ role: "status" }).type, "p");
});

test("homepage renders category list inside a sidebar alongside latest recipes", async () => {
  globalThis.fetch = async (url) => String(url).includes("/categories")
    ? Response.json([
        { id: "c1", name: "Món Việt", slug: "mon-viet", description: null, recipeCount: 5 },
        { id: "c2", name: "Món Chay", slug: "mon-chay", description: null, recipeCount: 3 },
      ])
    : Response.json({
        items: [{ id: "r1", slug: "canh-chua", title: "Canh chua cá lóc", description: "Đậm đà", prepTime: 15, cookTime: 25, servings: 4, difficulty: 1, thumbnailUrl: null, mediumUrl: null, publishedAt: null }],
        totalCount: 1, page: 1, pageSize: 6, totalPages: 1,
      });

  await render(<Home />);

  const sidebar = view!.root.findByType("aside");
  assert.equal(sidebar.props["aria-label"], "Danh mục món ăn");
  assert.match(text(sidebar), /Món Việt/);
  assert.match(text(sidebar), /Món Chay/);
  assert.match(text(view!.root), /Canh chua cá lóc/);
});

test("empty search or filtered recipe list offers a reset link back to all recipes", async () => {
  globalThis.fetch = async () => Response.json(emptyPage);
  await render(<RecipeListClient page={1} query="phở" />);
  const links = view!.root.findAll((node) => node.props.href === "/recipes");
  assert.ok(links.length >= 1);
  assert.match(text(view!.root), /Xem tất cả món ăn/);
});

test("sitemap includes static routes, categories, and recipes with graceful upstream fallback", async () => {
  globalThis.fetch = async (url) => String(url).includes("/categories")
    ? Response.json([{ id: "c1", name: "Món Việt", slug: "mon-viet", description: null, recipeCount: 2 }])
    : Response.json({
        items: [{ id: "r1", slug: "bun-bo-hue", title: "Bún bò Huế", description: "Cay nồng", prepTime: 20, cookTime: 60, servings: 4, difficulty: 2, thumbnailUrl: null, mediumUrl: null, publishedAt: "2026-10-20T00:00:00Z" }],
        totalCount: 1, page: 1, pageSize: 50, totalPages: 1,
      });

  const entries = await sitemap();
  const urls = entries.map((entry) => entry.url);
  assert.ok(urls.includes("http://localhost:3000/"));
  assert.ok(urls.includes("http://localhost:3000/recipes"));
  assert.ok(urls.includes("http://localhost:3000/categories"));
  assert.ok(urls.includes("http://localhost:3000/categories/mon-viet"));
  assert.ok(urls.includes("http://localhost:3000/recipes/bun-bo-hue"));

  globalThis.fetch = async () => new Response(null, { status: 503 });
  const fallbackEntries = await sitemap();
  assert.equal(fallbackEntries.length, 3);
});

test("recipe SEO helpers build Open Graph metadata and Schema.org Recipe JSON-LD", () => {
  const sampleRecipe = {
    id: "r1",
    slug: "pho-bo",
    title: "Phở bò truyền thống",
    description: "Nước dùng trong và thơm mùi quế hồi.",
    instructions: "Ninh xương và chan nước dùng.",
    prepTime: 30,
    cookTime: 120,
    servings: 4,
    difficulty: 2 as const,
    status: 2 as const,
    rowVersion: "AQ==",
    createdAt: "2026-10-01T00:00:00Z",
    publishedAt: "2026-10-02T00:00:00Z",
    author: { id: "a1", fullName: "Nguyễn Khiêm Thuận", avatarUrl: null },
    category: { id: "c1", name: "Món nước", slug: "mon-nuoc" },
    nutrition: { calories: 450, protein: 28, carbs: 52, fat: 12, fiber: 3 },
    ingredients: [{ id: "i1", name: "Bánh phở", quantity: 500, unit: "g", notes: null, orderIndex: 0 }],
    steps: [{ id: "s1", stepNumber: 1, title: "Ninh xương", description: "Hầm xương bò trong 2 giờ.", imageUrl: null, timerMinutes: 120, rowVersion: "AQ==" }],
    images: [{ id: "img1", originalUrl: "https://storage.test/pho-original.jpg", mediumUrl: "https://storage.test/pho-medium.jpg", thumbnailUrl: null, altText: "Tô phở bò", isPrimary: true, orderIndex: 0 }],
  };

  const metadata = buildRecipeMetadata(sampleRecipe, "https://culinary.example");
  assert.equal(metadata.title, "Phở bò truyền thống | Culinary Blog");
  assert.equal(metadata.alternates?.canonical, "https://culinary.example/recipes/pho-bo");
  assert.equal(metadata.openGraph?.title, "Phở bò truyền thống");

  const jsonLd = buildRecipeJsonLd(sampleRecipe, "https://culinary.example");
  assert.equal(jsonLd["@context"], "https://schema.org");
  assert.equal(jsonLd["@type"], "Recipe");
  assert.equal(jsonLd.name, "Phở bò truyền thống");
  assert.equal(jsonLd.prepTime, "PT30M");
  assert.equal(jsonLd.cookTime, "PT120M");
  assert.equal(jsonLd.totalTime, "PT150M");
  assert.deepEqual(jsonLd.recipeIngredient, ["500 g Bánh phở"]);
});

