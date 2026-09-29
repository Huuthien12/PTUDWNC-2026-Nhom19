import type { Metadata } from "next";
import Header from "@/components/Header";
import AuthLifecycle from "@/components/auth/AuthLifecycle";
import Footer from "@/components/layout/footer";
import "./globals.css";

export const metadata: Metadata = {
  title: "Culinary Blog",
  description: "Chia sẻ và khám phá các công thức nấu ăn",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="vi"
      className="h-full antialiased"
    >
      <body className="min-h-screen flex flex-col">
        <AuthLifecycle />
        <a className="cb-skip" href="#main-content">Đến nội dung chính</a>
        <Header />
        <main id="main-content" tabIndex={-1} className="flex min-w-0 flex-1 flex-col">
          {children}
        </main>
        <Footer />
      </body>
    </html>
  );
}
