# 上岸與結局調整（納入 V15.1.0）

- 每個 surface 冰洞（包含大跳前的換氣）及最後 exit 都共用同一段 0.85 秒演出：衝出、0.26 秒撞飛垂釣者、移到原座位、落定。之後才接受補氣輸入。音樂不暫停，碰撞期間持續耗氧。
- 高手水下耗氧恢復 HTML 第 13 版公式 `(4.15 + depth * 0.012)` 百分點／秒；新手與中階皆為 50%。途中岸上用深度 0 計算；連打每次補剩餘容量的 12%。
- 吃魚第二次成長門檻是 `targets.fat`，不是歌曲時間。死亡時少於門檻為餓死鬼，達到則為天使；平安完成則依 `targets.full` 分成趴平休息／棒棒糖朋友。門檻維持依各歌魚數與難度調整，取 10 隻為單位。
- 標題取自使用者 `aa4a7ddbb536d394.webp`，天使取自 `天使.webp`；只有格式轉 PNG，圖案不改動。新趴平圖由內建 imagegen 生成，提示保存在 `RESTING_SEAL_PROMPT.txt`。垂釣者圖集沿用本次 HTML 提案的三姿勢素材。
- 新美術仍可在 `Assets/Animation/Seal/SealArt.asset` 指派；主畫面標題保持原 scene anchor 並等比例縮放。
- 今後建置與包裝使用 `SealBreath.exe` / `SealBreath_Data`。過去已發布的下載包不變。
- Ctrl+Shift+F8 或 Tools → 海豹呼呼 → 驗證並建置上岸結局測試版，輸出 `Builds/Shore-Preview/SealBreath.exe`。

## 編輯與驗證

選取場景的 HuhuStage，Inspector「撞飛 → 接替 → 吸氣」可預覽共用上岸演出，開啟 previewAnimation 看循環，或拖 previewTime 看指定秒數。旁邊的朋友／攤平／餓死鬼／天使按鈕可預覽結局。角色素材在 SealArt.asset；Credits 在 GameCredits.asset。

本次本機驗證：
- HTML V13 原始邏輯回歸：64 scenarios、6014 checkpoints、589032 assertions。
- 上岸新流程：729 個歌曲／難度／幀率／延遲／冰洞組合，7686 assertions，包含四種結局邊界、演出途中窒息、暫停恢復。
- 耗氧：936 assertions、81 組速率案例、18 次完整路線；另有 6 次簡易／中階容錯完整路線。
- Unity 原生輸入 21 assertions、音效 189 assertions；16 種音效均成功載入。
- 三首音樂長度與譜面精確到一個取樣內；實際執行第一首高手完整路線 211 秒，279/279 魚、289 Perfect、0 Miss，含途中換氣及暫停恢復。這是自動輸入整合測試，不代表人工設備延遲校正。
- 21 個主要畫面以及不同比例與動畫畫面已擷取至 Validation/ShorePreview。

初次實作僅保留本機驗證；後續依使用者指示與主畫面美術一同納入 V15.1.0 發布。Builds/Shore-Preview 是本機 QA 試玩檔，正常啟動即可遊玩。
最後排版修正版已以獨立副本背景建置成功，回存到上述試玩目錄；21 個主要畫面、17 個額外比例／動畫畫面、音效與輸入複驗均通過。最終畫面檢查時間 2026-10-04 12:27（UTC+8）。
