import type { Metadata } from "next";
import Header from "@/components/Header";
import AuthLifecycle from "@/components/auth/AuthLifecycle";
import Footer from "@/components/layout/footer";
import QueryProvider from "@/components/QueryProvider";
import { buildSiteMetadata } from "@/services/recipe-seo";
import "./globals.css";

export const metadata: Metadata = buildSiteMetadata();

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="vi"
      className="h-full antialiased"
    >
      <body className="min-h-screen flex flex-col">
        <QueryProvider>
          <AuthLifecycle />
          <a className="cb-skip" href="#main-content">Đến nội dung chính</a>
          <Header />
          <main id="main-content" tabIndex={-1} className="flex min-w-0 flex-1 flex-col">
            {children}
          </main>
          <Footer />
        </QueryProvider>
      </body>
    </html>
  );
}
