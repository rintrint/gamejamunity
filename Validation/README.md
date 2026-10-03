# V14.0.1 發布驗證

Unity 6000.6.4f1，Windows x64。HTML 第 13 版基準 commit：`1665a124f2a60c380989dd53af80a943386e2d7e`。

| 檢查 | 結果／證據 |
| --- | --- |
| 原始 JS 對照 C# | 64 情境、6,014 檢查點、589,032 斷言通過；`core-parity.json` |
| 原生視窗 | 19 個場景正常；`native-capture.json` 與 `Screenshots` |
| Input System | 同次更新 50 個獨立按鍵邊緣、每一下都有吃魚回饋；`input-parity.txt` |
| 原生音訊 | 3 歌、16 音效、各事件實際觸發、45 個獨立 DSP 排程校正拍；`audio-parity.txt` |
| 選曲對話框 | 程式與畫面檢查通過；實際滑鼠操作被 Windows 系統提示阻擋，未完成；詳見 `ui-review.json` |
| 音訊轉換 | WAV 與原始 gapless 解碼逐樣本一致；`audio-pcm.json` |
| Unity 匯入波形 | 三首歌起點偏移與總長度差均 0 samples；`audio-alignment.json` |
| 整首歌實時測試 | 音樂1／高手，207.6135 秒，279/279 魚、289 PERFECT、0 Miss、朋友結局；`realtime-soak.json` |
| 暫停／繼續 | 音樂時鐘與氧氣完全凍結，恢復後繼續正常 |
| 正式壓縮包 | 全套 Windows 執行檔與資料、ZIP CRC、SHA-256、測試入口排除；`windows-package.json` |

實時測試透過真正的 Input System 按鍵事件與 DSP 音樂時鐘執行，未直接呼叫核心判定來製造命中。
最大自動輸入判定誤差 7.033 ms；這不是玩家反應、音樂辨識或聲學輸出延遲的量測。
暫停、途中連打補氣、成長與結算全部走正式遊戲邏輯。其餘歌曲／難度由 JS 對照資料涵蓋。

截圖來自含測試入口的 QA 建置；Release 使用獨立正式建置，不含 QA 命令列入口。
畫面已檢查選曲、設定、CREDITS、三種體型、缺氧頭部漸變、吃魚、換氣及四種結局。
Unity 與瀏覽器的字型抗鋸齒與 GPU 呈現可能不同；硬體音訊延遲仍需使用遊戲內校正。
這是自動化與畫面審視的結果，並非多裝置人工試玩或逐像素完全相同的保證。
