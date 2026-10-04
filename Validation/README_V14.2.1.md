# V14.2.1 發布驗證

Unity 6000.6.4f1，Windows x64。此版本只修正 UI Hover、文字顏色及控制項辨識度。

| 本次重新執行 | 結果／證據 |
| --- | --- |
| HTML V13 核心對照 | 64 情境、6,014 檢查點、589,032 斷言通過；`core-parity.json` |
| 氧氣平衡 | 237 斷言、81 組消耗率、18 條完整路線通過；`oxygen-balance.json` |
| 難度平衡 | 125 斷言、6 條不完美通關路線通過；`difficulty-balance.json` |
| Unity 正式建置 | Windows 非 Development 建置成功；`release-build.json` |
| UI 實際操作 | Editor 與正式 EXE 的選單／對話框檢查；`ui-review-V14.2.1.json` |
| 發布包 | 154 個檔案、ZIP CRC 與 SHA-256、測試入口排除；`windows-package.json` |

滑鼠檢查涵蓋一般文字不變色、未選與已選歌曲／難度 Hover、選取難度不穿透背景、練習選項及校正區辨識度、Start 與 CREDITS 的返回冰塊 Hover。正式 EXE 顯示 V14.2.1，使用 Microsoft JhengHei，檢查期間 Player.log 無 managed exception。

本次未重新進行整首歌、音訊 PCM／DSP 或多比例自動截圖測試。這些區域的原始程式與資產未在本次變更；先前結果保留原始版本欄位，不當作 V14.2.1 重測結果。前次完整驗證索引見 `README_V14.2.0.md`。
