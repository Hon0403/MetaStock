using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace MetaStockSync
{
    public class CaptchaSolver
    {
        private readonly string _pythonPath;
        private readonly string _scriptPath;

        public CaptchaSolver()
        {
            // 自動偵測作業系統並設定 Python 指令
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _pythonPath = "python"; // Windows 建議加入環境變數，或使用預設指令
                _scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "solve_captcha.py");
            }
            else
            {
                // Linux (GitHub Actions) 環境使用 python3
                _pythonPath = "python3";
                _scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "solve_captcha.py");
            }

            // 如果是在開發環境 (dotnet run)，BaseDirectory 可能在 bin/Debug 下，
            // 需要往上找或是直接檢查檔案是否存在
            if (!File.Exists(_scriptPath))
            {
                // 嘗試找專案目錄下的路徑
                var projectPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "solve_captcha.py");
                if (File.Exists(projectPath)) _scriptPath = projectPath;
            }
        }

        public string Solve(byte[] imageBytes)
        {
            try
            {
                if (imageBytes == null || imageBytes.Length < 100)
                {
                    Console.WriteLine($"  [診斷] 驗證碼圖片異常: {imageBytes?.Length ?? 0} bytes (太小，可能非圖片)");
                    return string.Empty;
                }

                var base64Image = Convert.ToBase64String(imageBytes);

                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = $"\"{_scriptPath}\"", // 傳入腳本路徑
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                // 將 Base64 影像傳送到標準輸入
                using (var writer = process.StandardInput)
                {
                    writer.Write(base64Image);
                }

                // 讀取結果
                string result = process.StandardOutput.ReadToEnd().Trim();
                string error = process.StandardError.ReadToEnd().Trim();

                process.WaitForExit();

                // 檢查是否有錯誤
                if (!string.IsNullOrEmpty(error))
                {
                    // 任何 stderr 輸出都記錄下來，方便診斷
                    if (error.Contains("ERROR") || error.Contains("錯誤") || error.Contains("Traceback") || error.Contains("ModuleNotFoundError"))
                    {
                        Console.WriteLine($"  [診斷] Python 錯誤: {error}");
                        return string.Empty;
                    }
                }

                // 如果結果為空，輸出診斷資訊
                if (string.IsNullOrWhiteSpace(result))
                {
                    Console.WriteLine($"  [診斷] OCR 回傳空字串。腳本路徑: {_scriptPath}, 檔案存在: {File.Exists(_scriptPath)}, 圖片大小: {imageBytes.Length} bytes");
                    if (!string.IsNullOrEmpty(error))
                        Console.WriteLine($"  [診斷] Python stderr: {error}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"驗證碼破解器異常: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
