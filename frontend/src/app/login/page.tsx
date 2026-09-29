import LoginForm from "@/components/auth/LoginForm";

export default async function LoginPage({ searchParams }: {
  searchParams: Promise<{ next?: string; reason?: string }>;
}) {
  const { next, reason } = await searchParams;
  return <LoginForm next={next} reason={reason} />;
}
