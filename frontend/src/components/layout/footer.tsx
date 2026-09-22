export default function Footer() {
  return (
    <footer className="shrink-0 border-t border-gray-700 bg-gray-900 text-gray-200">
      <div className="mx-auto flex max-w-6xl flex-col gap-4 px-6 py-8 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-lg font-semibold text-white">Culinary Blog</p>
          <p className="mt-2 max-w-md text-sm leading-6">
            Chia sẻ công thức và cảm hứng nấu ăn mỗi ngày.
          </p>
        </div>
        <p className="text-sm leading-6 sm:max-w-xs sm:text-right">
          Nhóm 19 · Phát triển ứng dụng Web nâng cao
        </p>
      </div>
    </footer>
  );
}
