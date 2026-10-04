海豹呼呼 **V15.0.0**，Unity 6.6 Windows x64 大版本更新。

下載 `SealGugu-V15.0.0-Windows-x64.zip`，完整解壓縮後執行 **SealGugu.exe**。請保留同資料夾的遊戲資料、MonoBleedingEdge 與 DLL。

- **新名稱與標題美術**：遊戲更名「海豹呼呼」，主畫面使用符合冰海手繪風格的透明標題圖。
- **URP 2D**：以 Unity 6.6 內附 URP 17.6.0 的 2D Renderer 呈現背景、角色、魚與特效；提供 Sprite Lit 材質及中性 Global Light 2D。
- **可編輯四幕**：Main 場景包含主畫面、吸氣、海底與結局。不需 Play 即可切換預覽，並調整美術位置、替換圖片及燈光；人工位置與程式動畫分層保存。
- **角色動畫整理**：瘦／中／胖資產集中管理，游泳、吃魚、飢餓、吸氣與結局使用可編輯 Animator clips。腹部以單一網格變形；動畫仍依原歌曲時鐘取樣，保留立即切排的操作。
- **保留遊戲設定與玩法**：改名時讀取舊名稱的魚速、延遲、判定與減少動態設定。三首歌、難度、節拍判定、音效與途中換氣耗氧規則維持既有版本。

本次升為 V15.0.0，代表渲染及場景編輯架構的更新。HUD 與選曲對話框仍沿用既有 IMGUI 邏輯，未遷移為 Canvas。

小組編輯：開啟 `Assets/Scenes/Main.unity`，選取 `Presentation · editable 2D scenes`，由 Inspector 的 Preview Scene 切換四幕。編輯 `Art … · edit offset` 父物件以保存人工調整；動畫與體型在 `Assets/Animation/Seal`。完整說明見 `Documentation/HUHU_URP_EDITING.md`。CREDITS 仍可直接在 GameCredits 資產輸入。

驗證包含核心 64 組情境、耗氧與難度測試、四幕與動畫取樣、19 個狀態畫面及不同視窗比例、按鍵和音效路由。URP 工作版本以實際音樂時鐘完成高手音樂 1：279/279 隻魚、0 Miss、暫停恢復成功。正式 Windows 包另檢查建置、啟動及 ZIP 完整性，排除 QA 測試入口。

本機正式輸出：`Builds/V15.0.0`。
