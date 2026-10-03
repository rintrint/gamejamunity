# 海豹咕咕 V14.0.1

Unity **6.6 / 6000.6.4f1** 原生 Windows 版。以 HTML 第 13 版 `1665a124f2a60c380989dd53af80a943386e2d7e` 為移植基準，保留原始歌曲、譜面、手繪素材和遊戲規則。這個專案只有海豹咕咕，沒有舊版切換選單。

## 直接遊玩

到 GitHub Releases 下載 Windows x64 ZIP，完整解壓縮後執行 **SealGugu.exe**。請保留旁邊的 `SealGugu_Data`、`UnityPlayer.dll`、`MonoBleedingEdge` 等資料；不能只拿走 exe。

| 操作 | 按鍵 |
| --- | --- |
| 上排／上移 | D、F、↑ |
| 下排／下移 | J、K、↓ |
| 開場吸氣 | 看腹部與氣流收攏，按一次 Space 或任一上下排鍵收氣 |
| 途中上岸換氣 | 連打 Space 或上下排按鍵；倒數後自動下海 |
| 暫停／繼續 | Esc |

三首歌曲各有新手、中階、高手。預設判定 ±150 ms；可調魚速 0.1–2.0×、延遲 ±200 ms、判定範圍 ±40–200 ms。魚速只影響視覺距離，不會改動音樂或拍點。設定儲存在本機。

空拍可以自由移動；有魚靠近時，太早、太晚或按錯排會 Miss，不能連按補中。漏接的魚會繼續離開畫面。肺活量、飽食度、節奏穩定度與各歌曲的成長門檻沿用第 13 版；途中補氣採剩餘容量的 12%，永遠不會直接補到 100%。

V14.0.1 修正了選曲視窗的滑鼠穿透，主畫面統一由 Start 進入選曲及設定。Start／Credits／Exit 有暖色 Hover 提示。遊玩時每次上下按鍵都會播放吃魚音效，包含空拍；同一次命中不會重複疊加。完整修正內容見 `Documentation/RELEASE_V14.0.1.md`。

## 用 Unity 編輯

1. Unity Hub → Add / Add project from disk，選此 repo 資料夾。
2. 使用 **6000.6.4f1** 開啟，等待第一次素材匯入。
3. 開啟 `Assets/Scenes/Main.unity`，按 Play。

若尚未建立場景，執行 **Tools → 海豹咕咕 → 準備專案與主場景**。C# 邏輯與原生紋理繪製位於 `Assets/Scripts`；執行時不使用瀏覽器、WebView 或網路服務。

### 小組成員填 CREDITS

執行 **Tools → 海豹咕咕 → 編輯 CREDITS**，或在 Project 點選 **Assets/Resources/GameCredits.asset**。

- **小組（Group）**：小組名稱，預設「第二組」。
- **名單（Entries）**：每一格輸入一位成員的姓名與分工；按 **+** 新增。
- 預設第一格「內容待補」可直接覆寫。儲存專案後重新打包，就會反映在主畫面的 CREDITS。

名單是一般 ScriptableObject 資產，會隨 Git 一起保存；不需要修改程式碼。

## 打包與驗證

Unity 選单 **Tools → 海豹咕咕 → 打包 Windows V14.0.1**。輸出 `Builds/Windows/SealGugu.exe`。命令列也可使用：

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath "$PWD" -executeMethod SealGugu.Editor.ProjectBuild.BuildWindows -logFile "$PWD\build.log"
```

打包前會自動執行原版 JavaScript 對照資料測試。詳細數量與結果寫入 `Validation/core-parity.json`。對照生成方式見 `Tools/CORE_PARITY.md`。

開發建置加 `-guguDevelopment`；該建置可接受 `-guguCapture <絕對輸出路徑>`，一次輸出各場景的原生畫面與狀態報告。這些預覽／自動截圖入口不會編入正式 release。

只需自動驗證時可改加 `-guguQA`：保留測試入口，但不啟用 Unity Development Player 的偵錯連線。`-guguOutput <輸出資料夾>` 可把測試包和正式包分開保存。

## 結構

- `Assets/Scripts/Core`：不依賴 Unity 的確定性遊戲規則。
- `Assets/Scripts/Rendering`：保留原圖比例與裁切的 2D 場景、海豹及特效。
- `Assets/Scripts/Runtime`：介面、時間戳按鍵、DSP 音樂時鐘、音效與設定。
- `Assets/Resources/Data/chart.json`：三首歌的原始譜面。
- `Assets/Resources/Art`／`Audio`：原始美術與 gapless PCM 音訊；WebP 只做無損格式轉換。
- `SourceAudio`：20 份原始 MP3 的完整備份；舊 menu 音樂僅封存，不放入執行版。
- `Assets/Editor`：匯入設定、名單編輯與建置入口。
- `Tools`：資產準備、原版對照資料及測試工具。

## 一致性與實際裝置

核心測試比較逐個事件、判定、氧氣、分數、飽食度與結局，並涵蓋 30／144 FPS。音樂以 Unity DSP 排程，鍵盤採 Input System 事件時間戳記，避免累計畫面時間造成節奏漂移。中文字型、原始歌曲與必要素材均包在執行檔資料中。

Unity 使用與原網頁譜面分析相同的 SoundFile/libsndfile gapless 解碼流程，將三首歌及 16 個音效匯入為 32-bit float WAV，避免 MP3 編碼填充造成額外時間差。每份 WAV 逐樣本核對來源解碼，三首歌的解碼長度與譜面一致；此驗證不代表硬體延遲為零。主畫面與第 13 版一樣播放目前所選歌曲，舊版 menu 音樂只保留 MP3 封存。

Unity 與瀏覽器使用不同的字型、影像與音訊渲染器，像素抗鋸齒及裝置延遲仍可能有差異；軟體測試不能代替不同玩家、耳機與螢幕的實機校正。遊戲保留 16 秒拍點試聽與校正功能。

字型授權及素材來源記錄位於 `Documentation` 與 `Assets/Resources/Fonts`。發布包也附上字型授權。
