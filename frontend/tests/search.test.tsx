import assert from "node:assert/strict";
import { after, afterEach, test } from "node:test";
import type { ReactElement } from "react";
import { act, create, type ReactTestInstance, type ReactTestRenderer } from "react-test-renderer";
import { AppRouterContext } from "next/dist/shared/lib/app-router-context.shared-runtime";
import Home from "../src/app/page";
import Loading from "../src/app/loading";
import RecipesPage from "../src/app/recipes/page";
import RecipeListClient from "../src/components/recipes/RecipeListClient";
import SearchPagination from "../src/components/search/SearchPagination";
import RecipePagination from "../src/components/recipes/RecipePagination";
import RecipeImage from "../src/components/shared/RecipeImage";
import { searchRecipes } from "../src/services/search-service";
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
    ["(min-width: 401px)", "https://storage.test/medium.jpg"],
  ]);
  assert.equal(view!.root.findByType("img").props.src, "https://storage.test/original.jpg");
});

test("a broken image variant falls back to the original before showing a placeholder", async () => {
  const src = "https://storage.test/original.jpg";
  const mediumSrc = "https://storage.test/medium.jpg";
  await render(<RecipeImage src={src} mediumSrc={mediumSrc} title="Pho" />);
  await act(async () => {
    view!.root.findByType("img").props.onError({ currentTarget: { src, currentSrc: mediumSrc } });
  });
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

test("homepage category failure never invents category names or slugs", async () => {
  globalThis.fetch = async (url) => String(url).endsWith("/health")
    ? Response.json({ message: "OK" })
    : new Response(null, { status: 503 });
  await render(await Home());
  assert.match(text(view!.root), /Chưa thể tải danh mục/);
  for (const fake of ["Món Việt", "Bữa sáng", "Món chay", "Tráng miệng", "mon-viet", "bua-sang", "mon-chay", "trang-mieng"]) {
    assert.ok(!JSON.stringify(view!.toJSON()).includes(fake));
  }
});

test("homepage renders an explicit empty category state", async () => {
  globalThis.fetch = async (url) => String(url).endsWith("/health")
    ? Response.json({ message: "OK" })
    : Response.json([]);
  await render(await Home());
  assert.match(text(view!.root), /Chưa có danh mục để hiển thị/);
  assert.doesNotMatch(text(view!.root), /Món Việt/);
});

test("homepage has a route loading state", async () => {
  await render(<Loading />);
  assert.match(text(view!.root), /Đang tải trang chủ/);
  assert.equal(view!.root.findByProps({ role: "status" }).type, "p");
});
