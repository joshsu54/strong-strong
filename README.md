# strong strong：《雲境》煉丹玩法 V2

這裡放的是 2026-10-28 在 [z1nnz/yun-jing-rpg](https://github.com/z1nnz/yun-jing-rpg) 分支 `feature/alchemy-gameplay-v2` 上新增的煉丹玩法，只收錄這次新寫的檔案，不複製原專案其他人的程式與素材。

完整說明、測試結果與尚未驗證的項目，見 [`docs/execution/alchemy-gameplay-v2-report.md`](docs/execution/alchemy-gameplay-v2-report.md)。

## 內容

| 位置 | 內容 |
|---|---|
| `packages/content/alchemy.json` | 藥材、處理方式、回春丹與聚氣丹、火候常數、品質權重、熟練度與加成 |
| `services/api/src/alchemy.ts` | 伺服器端的爐火重算、紀錄檢查、品質判定、熟練度 |
| `tests/` | 煉丹伺服器測試、測試用玩家代理、TS／C# 爐火對照資料 |
| `apps/game/Assets/YunJing/Runtime/Alchemy/` | Unity 端：爐火模擬、多鏡頭、行走與觸控、工作台、竹簡介面、音效粒子、自動玩家路徑 |
| `apps/game/Assets/YunJing/Editor/AlchemyRoomBuilder.cs` | 匯入 B01–B12 模型、產生丹房、驗證、Windows 建置 |
| `patches/yun-jing-rpg-shared-files.patch` | 對原專案共用檔案的修改（`game.ts`、`types.ts`、`GameApi.cs`、`world.json`、`qingxi.json`、`.gitignore`） |
| `docs/execution/` | 環境盤點、交付報告、電腦版與手機版實機截圖 |

## 怎麼跑

這些檔案不是獨立專案，需要放回 yun-jing-rpg（基準 commit `ebe57d4`）才能執行：

```bash
git clone https://github.com/z1nnz/yun-jing-rpg.git
cd yun-jing-rpg
git checkout ebe57d4
```

再把本 repo 的檔案複製進去，並套用共用檔案的修改：

```bash
git apply ../strong-strong/patches/yun-jing-rpg-shared-files.patch
```

或者直接切到原專案的分支，內容相同：

```bash
git checkout feature/alchemy-gameplay-v2
```

之後的測試、伺服器與 Unity 建置步驟見交付報告第 1 節。

## 注意

- B01–B12 模型檔不在本 repo，產生丹房時從 yun-jing-rpg 的 `art/exports/qingxi-alchemy-b/` 匯入。
- 玩家角色、丹房外殼與音效目前是佔位。
- 手機版介面已完成並在電腦上以手機解析度驗證，但尚未出 Android／iOS 安裝檔，也沒有在實體手機上測試。
