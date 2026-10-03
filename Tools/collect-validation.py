"""Collect successful Windows validation evidence after a development QA run."""
import argparse
import json
from pathlib import Path
import shutil

p = argparse.ArgumentParser()
p.add_argument('--project', type=Path, required=True)
p.add_argument('--capture', type=Path, required=True)
p.add_argument('--log', type=Path, required=True)
args = p.parse_args()
capture = json.loads((args.capture/'capture-report.json').read_text(encoding='utf-8-sig'))
soak = json.loads((args.capture/'realtime-soak.json').read_text(encoding='utf-8-sig'))
log = args.log.read_text(encoding='utf-8-sig', errors='replace')
assert capture['success'] and soak['success'] and len(capture['captures'])==19
assert 'GUGU_INPUT_PARITY PASS' in log and 'GUGU_AUDIO_PARITY PASS' in log
assert 'GUGU_DESKTOP_PARITY PASS' in log
assert soak['nativeClipDuration'] == soak['trackDuration']
out = args.project/'Validation'
out.mkdir(exist_ok=True)
shutil.copy2(args.capture/'capture-report.json', out/'native-capture.json')
shutil.copy2(args.capture/'realtime-soak.json', out/'realtime-soak.json')
checks = [line for line in log.splitlines() if line.startswith(('GUGU_INPUT_PARITY','GUGU_AUDIO_PARITY','GUGU_DESKTOP_PARITY',
    'floe-second:','floe-third:','drift-tide:','Loaded all','Calibration','GUGU_SOAK_SUCCESS','GUGU_CAPTURE_SUCCESS'))]
(out/'native-checks.txt').write_text('\n'.join(checks)+'\n', encoding='utf-8')
for name in ['input-parity.txt','audio-parity.txt','desktop-parity.txt']:
    shutil.copy2(args.capture/name, out/name)
(out/'Screenshots').mkdir(exist_ok=True)
for name in ['menu','setup','opening-full','swim-fat','surface','friends','desktop-fps','hover-Start-Botton','hover-Credits-Botton','hover-Exit-Botton']:
    shutil.copy2(args.capture/(name+'.png'), out/'Screenshots'/(name+'.png'))
summary = f'''# {capture['version']} 發布驗證

Unity 6000.6.4f1，Windows x64。HTML 第 13 版基準 commit：`1665a124f2a60c380989dd53af80a943386e2d7e`。

| 檢查 | 結果／證據 |
| --- | --- |
| 歷史 V13 JS 對照 C#（原消耗率） | 64 情境、6,014 檢查點、589,032 斷言通過；`core-parity.json` |
| 新版氧氣平衡 | 實際預設 0.5 倍；237 個斷言、81 組消耗率及 18 條完整路線通過；`oxygen-balance.json` |
| HTML UI 對照 | 相同文字 RGB、冰塊提亮 1.035 與柔和陰影；`html-ui-reference.json` 與 Hover 截圖 |
| 原生視窗 | 19 個場景正常；`native-capture.json` 與 `Screenshots` |
| Input System | 同次更新 50 個獨立按鍵邊緣、每一下都有吃魚回饋；`input-parity.txt` |
| 原生音訊 | 3 歌、16 音效、各事件實際觸發、45 個獨立 DSP 排程校正拍；`audio-parity.txt` |
| 選曲對話框 | 實際滑鼠選難度、阻擋背景 Exit、Credits、三個主選單 Hover 通過；詳見 `ui-review.json` |
| 桌面操作 | Alt+Enter 真正切换視窗模式、尺寸復原、F1 與暫停／呼吸隔離、系統游標；`desktop-parity.txt` |
| 音訊轉換 | WAV 與原始 gapless 解碼逐樣本一致；`audio-pcm.json` |
| Unity 匯入波形 | 三首歌起點偏移與總長度差均 0 samples；`audio-alignment.json` |
| 整首歌實時測試 | 音樂1／高手，{soak['trackDuration']:.4f} 秒，{soak['food']}/{soak['totalFish']} 魚、{soak['perfect']} PERFECT、{soak['misses']} Miss、朋友結局；`realtime-soak.json` |
| 暫停／繼續 | 音樂時鐘與氧氣完全凍結，恢復後繼續正常 |
| 正式壓縮包 | 全套 Windows 執行檔與資料、ZIP CRC、SHA-256、測試入口排除；`windows-package.json` |

實時測試透過真正的 Input System 按鍵事件與 DSP 音樂時鐘執行，未直接呼叫核心判定來製造命中。
最大自動輸入判定誤差 {soak['maximumJudgementErrorMs']:.3f} ms；這不是玩家反應、音樂辨識或聲學輸出延遲的量測。
暫停、途中連打補氣、成長與結算全部走正式遊戲邏輯。其餘歌曲／難度也由新版倍率的 18 條核心完整路線及歷史 JS 對照資料涵蓋。
背景測試使用隔離的虛擬鍵盤並暫時關閉 Input System 的失焦停用；正式遊戲仍在失焦時暫停。早期測試的失焦問題、重現和修正紀錄見 `soak-rerun-notes.json`。

截圖來自含測試入口的 QA 建置；Release 使用獨立正式建置，不含 QA 命令列入口。
畫面已檢查選曲、設定、CREDITS、三種體型、缺氧頭部漸變、吃魚、換氣及四種結局。
Unity 與瀏覽器的字型抗鋸齒與 GPU 呈現可能不同；硬體音訊延遲仍需使用遊戲內校正。
鍵盤快捷鍵使用原生 Input System 虛擬鍵盤整合測試；computer-use 的外部鍵盤注入未被遊戲接收（原有 Esc 亦同），未列為實體鍵盤測試通過。滑鼠使用 computer-use 實際操作，並由只讀記錄器核對 Windows IDC_HAND／IDC_ARROW。
這是自動化與畫面審視的結果，並非多裝置人工試玩或逐像素完全相同的保證。
'''
(out/'README.md').write_text(summary.replace('截图','截圖'), encoding='utf-8')
print(json.dumps(dict(success=True, captures=len(capture['captures']), songSeconds=soak['trackDuration'],
                     fish=soak['food'], misses=soak['misses'], outcome=soak['outcome'])))
