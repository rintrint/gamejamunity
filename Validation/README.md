# V14.2.2 發布驗證

Unity 6000.6.4f1，Windows x64。修正途中上岸連打換氣的持續耗氧。

| 本次重新執行 | 結果／證據 |
| --- | --- |
| HTML V13 核心對照 | 64 情境、6,014 檢查點、589,032 斷言通過；`core-parity.json` |
| 目前氧氣平衡 | 936 斷言、81 組消耗率、18 條完整歌曲路線、27 組換氣時序組合通過；`oxygen-balance.json` |
| 難度平衡 | 125 斷言、6 條不完美通關路線通過；`difficulty-balance.json` |
| Unity 正式建置 | 非 Development Windows 建置成功，輸出 `Builds/V14.2.2`；`release-build.json` |
| 正式 EXE 啟動 | 已查看 V14.2.2 主畫面、Start 選曲及開場吸氣，正常關閉；Player.log 無 managed exception |
| 發布包 | 154 個檔案、ZIP CRC 與 SHA-256、測試入口排除檢查通過；`windows-package.json` |

換氣測試使用獨立公式計算各難度耗氧，涵蓋 30／60／144 FPS 與 -200／0／+200 ms 校正。驗證連打前先計入時間耗氧、停止連打後下降、暫停與恢復、自動下海跨越的時間邊界、缺氧時不能靠遲來的按鍵復活、練習模式與安全的結尾岸上。開場仍採原本一次收氣。

歷史 HTML golden 明確使用 `legacyBalance = true`，保留原作的岸上免耗氧；目前版本的完整路線及新測試使用預設的新規則。沒有改寫歷史預期值以迎合 C#。

本次未重跑完整歌曲的影音實機測試或音訊 PCM／DSP、比例與 Hover 自動檢查。其先前報告保留原始版本欄位，不當作 V14.2.2 的重測結果。前次驗證索引見 `README_V14.2.1.md`。
