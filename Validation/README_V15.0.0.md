# V15.0.0 驗證

- `core-parity.json`：64 情境、6,014 檢查點、589,032 assertions。
- `oxygen-balance.json`：936 assertions，含 27 組上岸換氣情境。
- `difficulty-balance.json`：125 assertions，含 6 組不完美路線。
- `URP/editor-checks.json`：原生 Renderer2D、74 個 Sprite 觀察、人工錨點保存、3 體型與動畫曲線取樣。
- `release-build.json`：V15.0.0 非開發 Windows 建置及實際啟動檢查。
- `windows-package.json`：177 個檔案、ZIP CRC、SHA-256，正式程式集不含 QA 入口。

`URP` 中的畫面／音效／輸入與完整歌曲報告是在更改版本字串前，同一輪 URP 實作的本機測試，因此報告內仍標示 V14.2.2。沒有把歷史報告改寫為 V15.0.0。完整歌曲以真實 DSP 與虛擬鍵盤跑完 211 秒，279/279 隻魚、289 Perfect、0 Miss；暫停／恢復通過。V15.0.0 正式建置另外重新執行核心與場景檢查，並人工檢視主畫面、選曲與開場吸氣。未聲稱重跑三首歌或驗證所有玩家音效裝置延遲。

正式包位於 `Builds/V15.0.0`，未啟用 Development 或 GUGU_QA。HUD 與對話框沿用原 IMGUI；背景、角色、魚與特效為可編輯原生場景物件。
