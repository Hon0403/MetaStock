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

                // 檢查是否有錯誤 (Python 腳本已改為輸出 "錯誤")
                if (!string.IsNullOrEmpty(error) && (error.Contains("ERROR") || error.Contains("錯誤")))
                {
                    Console.WriteLine($"Python 錯誤: {error}");
                    return string.Empty;
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
