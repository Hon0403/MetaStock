
import sys
import ddddocr
import base64
import os

# 強制將 stdin/stdout 設定為 UTF-8 編碼，避免在 Windows 上發生編碼錯誤
sys.stdin.reconfigure(encoding='utf-8')
sys.stdout.reconfigure(encoding='utf-8')

def solve_captcha(image_path=None, image_data_b64=None):
    # 啟用 beta 模式以提高對複雜驗證碼的辨識準確度
    ocr = ddddocr.DdddOcr(show_ad=False, beta=True)
    
    if image_path:
        with open(image_path, 'rb') as f:
            image_bytes = f.read()
    elif image_data_b64:
        image_bytes = base64.b64decode(image_data_b64)
    else:
        return "錯誤: 未提供圖片"

    res = ocr.classification(image_bytes)
    return res

if __name__ == "__main__":
    if len(sys.argv) > 1:
        # 模式 1: 透過參數傳入檔案路徑
        image_path = sys.argv[1]
        if os.path.exists(image_path):
            print(solve_captcha(image_path=image_path))
        else:
            print("錯誤: 找不到檔案")
    else:
        # 模式 2: 標準輸入 (Base64 字串)
        # 這種方式效能較好 (不需要磁碟 I/O)
        try:
            # 從 stdin 讀取所有輸入
            input_data = sys.stdin.read().strip()
            if input_data:
                print(solve_captcha(image_data_b64=input_data))
            else:
                print("錯誤: 輸入內容為空")
        except Exception as e:
            print(f"錯誤: {e}")
