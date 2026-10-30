"use client";
export default function ErrorState({ reset }: { reset: () => void }) {
  return <main className="cb-container cb-section" role="alert">Không thể tải công thức. <button onClick={reset}>Thử lại</button></main>;
}
