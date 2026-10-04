# 海豹呼呼：URP 2D 場景與角色編輯

V15.0.0 將遊戲名稱改為「海豹呼呼」，並使用 URP 2D、可編輯四幕與集中管理的角色動畫。

## 開啟遊戲與四幕

1. 開啟 `Assets/Scenes/Main.unity`。
2. Hierarchy 選 `Presentation · editable 2D scenes`。
3. Inspector 的 `Preview Scene` 切換 **Menu、Breath、Underwater、Ending**；不需 Play 就能看見場景。
4. 點「更新預覽並框選畫面」，在 Scene 視窗編輯。按 Play 從主畫面開始實際遊玩。

四組場景是實際遊戲共用的 native SpriteRenderer 物件，不是螢幕截圖或示意圖。背景、海豹、魚、冰塊、氣流和特效分組放在各幕底下。可移動群組或其 `Art … · edit offset` 父物件；下面 `Visual · animated` 的位置與尺寸由歌曲時間更新，請勿用該子物件保存手動位置。移動父物件後，Play 中也會套用。

主畫面標題與 Start／Credits／Exit 的位置和尺寸，使用 Presentation 下的四個 `position and size` 物件調整；點擊範圍會跟著調整。標題來源為透明 PNG `Assets/Resources/Art/huhu/title.png`。原本選曲、HUD、對話框與 Hover 邏輯仍保留現有 IMGUI 控制，不是新建一套 Canvas。

`HuhuGraphic` 的 Replacement 可指定替代圖，Replacement Crop 使用左上起算的像素裁切；Tint 控制色調、Receive Light 控制是否接受 2D 光照。動態譜面魚的物件會重用，整體位置請修改 Notes 群組，不要把單一 pool 槽當成特定音符。

## 海豹資產與動畫

- `Assets/Animation/Seal/SealArt.asset`：瘦／中／胖的游泳圖、裁切與嘴部定位，站立圖、吃魚及飢餓圖集。
- `Assets/Animation/Seal/Seal.controller`：Animator 狀態。
- `Assets/Animation/Seal/*.anim`：Idle、Hungry、Swim、Eat、Inhale、Rest、Friends。
- `Assets/Prefabs/SealActor.prefab`：共用動畫驅動與腹部變形設定。

Animation 視窗可編輯 `bodyScale`、`roll`、`bob`、`frame` 曲線。Swim 控制均勻縮放與輕微旋轉，Eat 控制吃魚格數，Hungry 與肚子音效同步，Inhale 控制整體吸氣放大。腹部使用一個 195 頂點 mesh，只讓脊背下方的腹部鼓起，避免數百個切片物件；幅度在 SealActor 的 Belly Expansion 調整。

動畫按歌曲／呼吸時間取樣，沒有自行推進的判定時鐘；上下移動立即切排，動畫不加入輸入冷卻。調整曲線會影響演出，不會移動譜面時間。缺氧的頸部漸層與三種體型表情仍沿用原本的遮罩計算。

Preview Pose 可用 `swim-thin`、`swim-medium`、`swim-fat`、`low-thin`、`low-medium`、`low-fat`，主畫面可填 `hungry`，結局可填 `friends`、`rest`、`angel`。勾 Preview Animation 可讓預覽隨時間動起來。

## URP 與本機驗證

Unity 6000.6.4f1，內附 URP **17.6.0**。Graphics 與所有 Quality 等級指定 `Assets/Settings/HuhuURP.asset`，使用 `HuhuRenderer2D.asset`，Sprite Lit 材質及一盞中性 Global Light 2D。可新增局部 Light 2D；初始全域光保持原本手繪顏色。

Tools → 海豹呼呼 → **驗證 URP 場景與動畫**，檢查真正的 Renderer2D、場景位置可保存、三種體型及動畫取樣。

Tools → 海豹呼呼 → **建置 URP 本機測試包（不發布）**，會跑核心／耗氧／難度檢查後，輸出至 `Builds/URP-Preview`；不覆蓋正式版本資料夾（例如 `Builds/V15.0.0`）。該包保留 QA 入口，只供本機測試，不是 Release。

「建立 URP 2D 可編輯場景」只建立缺少的配置，已有動畫曲線、材質和藝術家位置不會重建。場景改好後按 Inspector 的「儲存場景調整」或 Ctrl+S。

改名後會從原「海豹咕咕」的 Windows 本機設定讀取魚速、延遲、判定範圍與減少動態選項；只補上新名稱尚未儲存的設定，不會覆寫已重新調整的數值。

本機驗證結果位於 `Validation/URP`：64 組核心情境、耗氧與難度檢查、19 張狀態畫面與 17 張介面／比例畫面、輸入和音效路由。音樂 1 高手完整正常速度自動遊玩 211 秒，279/279 隻魚、0 Miss、暫停恢復成功；此為虛擬鍵盤測試，不代表人體輸入或特定音效裝置延遲量測。
