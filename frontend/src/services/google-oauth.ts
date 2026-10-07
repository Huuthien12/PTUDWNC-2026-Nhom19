const GOOGLE_AUTHORIZE_URL = "https://accounts.google.com/o/oauth2/v2/auth";
const TRANSACTION_KEY = "culinary.google-oauth.v1";

type GoogleTransaction = { state: string; codeVerifier: string };

function encode(bytes: Uint8Array) {
  return btoa(String.fromCharCode(...bytes)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=/g, "");
}

function randomValue() {
  const bytes = new Uint8Array(32);
  crypto.getRandomValues(bytes);
  return encode(bytes);
}

async function codeChallenge(codeVerifier: string) {
  return encode(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(codeVerifier))));
}

function configuration() {
  const clientId = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;
  const redirectUri = process.env.NEXT_PUBLIC_GOOGLE_REDIRECT_URI;
  if (!clientId || !redirectUri) throw new Error("Đăng nhập Google chưa được cấu hình.");
  return { clientId, redirectUri };
}

export async function beginGoogleLogin() {
  const { clientId, redirectUri } = configuration();
  const state = randomValue();
  const codeVerifier = randomValue();
  window.sessionStorage.setItem(TRANSACTION_KEY, JSON.stringify({ state, codeVerifier } satisfies GoogleTransaction));
  const query = new URLSearchParams({
    client_id: clientId, redirect_uri: redirectUri, response_type: "code", scope: "openid email profile",
    state, code_challenge: await codeChallenge(codeVerifier), code_challenge_method: "S256",
  });
  window.location.assign(`${GOOGLE_AUTHORIZE_URL}?${query}`);
}

export function consumeGoogleCallback(params: URLSearchParams) {
  const error = params.get("error");
  const code = params.get("code");
  const state = params.get("state");
  const stored = window.sessionStorage.getItem(TRANSACTION_KEY);
  window.sessionStorage.removeItem(TRANSACTION_KEY);
  if (error) throw new Error("Google đã từ chối hoặc không thể hoàn tất đăng nhập.");
  if (!code || !state || !stored) throw new Error("Phiên đăng nhập Google không hợp lệ.");
  let transaction: GoogleTransaction;
  try { transaction = JSON.parse(stored) as GoogleTransaction; } catch { throw new Error("Phiên đăng nhập Google không hợp lệ."); }
  if (transaction.state !== state || !transaction.codeVerifier) throw new Error("Phiên đăng nhập Google không hợp lệ.");
  return { authorizationCode: code, codeVerifier: transaction.codeVerifier };
}
