using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MetaStockSync
{
    public class CaptchaSolver
    {
        private readonly string _pythonPath;
        private readonly string _scriptPath;

        public CaptchaSolver()
        {
            // 目前依照使用者環境硬並路徑
            _pythonPath = @"C:\Users\user\AppData\Local\Programs\Python\Python311\python.exe";
            _scriptPath = @"d:\個人專案\MetaStock\metastock-sync\solve_captcha.py";
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
