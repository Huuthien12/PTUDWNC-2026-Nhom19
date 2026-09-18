export default async function Home() {
  let healthStatus = "Chưa kết nối được Backend";
  let isSuccess = false;

  try {
    // Sửa cổng từ 5000 thành 5062 theo đúng cổng Backend đang phát
    const res = await fetch("http://localhost:5062/health", { cache: "no-store" });
    if (res.ok) {
      const data = await res.json();
      healthStatus = `Kết nối Backend thành công! Message: ${data.message}`;
      isSuccess = true;
    }
  } catch (error) {
    healthStatus = "Lỗi: Backend chưa bật hoặc sai cổng.";
  }

  return (
    <main className="flex flex-1 flex-col items-center justify-center px-6 py-12 sm:p-24 bg-gray-900 text-white">
      <h1 className="text-3xl font-bold mb-6">Culinary Blog - Test System</h1>
      <div className="p-6 border rounded-xl bg-gray-800 shadow-lg max-w-md text-center">
        <p className="font-semibold text-gray-300 mb-2">Trạng thái Backend:</p>
        <p className={`font-medium ${isSuccess ? "text-green-400" : "text-red-400"}`}>
          {healthStatus}
        </p>
      </div>
    </main>
  );
}