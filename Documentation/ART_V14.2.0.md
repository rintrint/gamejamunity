# V14.2.0 飢餓動畫素材

- 工具：內建 image_gen（未使用 CLI/API fallback）。
- 原角色參考：`Assets/Resources/Art/tide/seal.png`。
- 專案素材：`Assets/Resources/Art/v14-2/menu-hunger.png`，1536×1024 RGBA，保留生成的透明度。
- 六格圖集；以同一音效 DSP 時鐘播放摸肚子、皺起表情與放鬆的動作，聲音結束後回到正常表情。只用於主畫面，失敗的飢餓幽靈素材不受影響。
- 背景保留原畫，以程式等比例裁為 16:9 構圖，再依實際視窗比例 cover，沒有拉伸海豹或按鈕。

## 最終生成提示詞

Use case: stylized-concept / identity-preserve. Create a production-ready transparent 2D game sprite sheet for a brief hungry-stomach rumble animation. The attached image is the exact character identity and drawing style reference: white chubby seal, head on the RIGHT and tail on the LEFT, dark indigo pencil outlines, big shiny eyes, pale icy-blue textured shadows, subtle warm cream highlights, pink tongue. Keep this same character and three-quarter pose, proportions and consistent silhouette/registration in every frame. Deliver ONE clean atlas, 3 equal columns by 2 equal rows, six full-body frames, read left to right then top to bottom. Each cell exactly the same size with generous transparent padding; same camera, body size, tail position and baseline. Frame 1 happy relaxed (like the reference); frame 2 starts feeling a stomach rumble, front flipper gently approaches belly, eyes soften; frame 3 a small hungry wince, flipper presses belly and abdomen tucks slightly; frame 4 strongest brief gentle rumble, eyes squeezed with a tiny wavy mouth and a few hand-drawn blue short motion strokes next to BELLY; frame 5 relaxing, flipper returns and eyes reopen; frame 6 happy relaxed again matching frame 1. Cute, healing, not distressed/crying, no tears. Must NOT use the crying ghost character. Original reference's colored-pencil hand-drawn look, avoid purple body tint. Fully transparent background and transparent cell gutters, no scenery, no grid lines, no text, no labels, no numbers, no drop shadows or glow. No cropping limbs, no seal duplication within a cell. Prefer 1536 by 1024 atlas.
