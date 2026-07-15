# コマンド一覧

全コマンドは `ms_` プレフィックス付きで登録されます (例: `ms_slay`)。
チャットでは `!slay` / `css_slay` でも使用可能です。

## チャットコマンド

| コマンド | 権限ノード | 説明 |
|---|---|---|
| !say \<message\> | tnms.adminutil.chat.command.say.normal | 全プレイヤーにチャットでメッセージ送信 |
| !csay \<message\> | tnms.adminutil.chat.command.say.center | 全プレイヤーにセンター表示でメッセージ送信 |
| !hsay \<message\> | tnms.adminutil.chat.command.say.hint | 全プレイヤーにヒント表示でメッセージ送信 |
| !asay \<message\> | tnms.adminutil.chat.command.say.admins | 管理者のみにメッセージ送信 |
| !psay \<player\> \<message\> | tnms.adminutil.chat.command.say.private | 特定プレイヤーにプライベートメッセージ送信 |
| @\<message\> (チャット) | tnms.adminutil.chat.command.say.admins | 管理者ブロードキャスト — 一般には `[ADMIN] msg`、管理者には `[ADMIN 名前] msg` |

## プレイヤー管理コマンド

### 戦闘 / ステータス

| コマンド | 権限ノード | 説明 |
|---|---|---|
| !slay \<player\> | tnms.adminutil.management.ingame.command.slay | プレイヤーをキル |
| !slap \<player\> [damage] | tnms.adminutil.management.ingame.command.slap | プレイヤーを叩く (ダメージ指定可) |
| !hp \<player\> \<health\> | tnms.adminutil.management.ingame.command.hp | 体力を設定 (100超はMaxHealthも調整) |
| !god \<player\> \<0\|1\> | tnms.adminutil.management.ingame.command.god | 無敵モード切替 |
| !speed \<player\> \<value\> | tnms.adminutil.management.ingame.command.speed | 速度倍率を設定 (0.0–10.0) |
| !gravity \<player\> \<scale\> | tnms.adminutil.management.ingame.command.gravity | 重力スケールを設定 (0.0–100.0) |
| !money \<player\> \<amount\> | tnms.adminutil.management.ingame.command.money | 所持金を設定 (0–60000) |

### 移動 / スポーン

| コマンド | 権限ノード | 説明 |
|---|---|---|
| !noclip \<player\> [0\|1] | tnms.adminutil.management.ingame.command.noclip | ノークリップ切替。値なしでトグル |
| !freeze \<player\> [time] | tnms.adminutil.management.ingame.command.freeze | プレイヤーをフリーズ (緑色に変化)。時間指定で自動解除 |
| !unfreeze \<player\> | tnms.adminutil.management.ingame.command.freeze | フリーズ解除 |
| !bury \<player\> | tnms.adminutil.management.ingame.command.bury | プレイヤーを地面に埋める |
| !unbury \<player\> | tnms.adminutil.management.ingame.command.bury | プレイヤーを地面から掘り出す |
| !respawn \<player\> | tnms.adminutil.management.ingame.command.respawn | 死亡プレイヤーをリスポーン |
| !revive \<player\> | tnms.adminutil.management.ingame.command.respawn | プレイヤーをリスポーン (生死問わず) |

### 視覚エフェクト

| コマンド | 権限ノード | 説明 |
|---|---|---|
| !blind \<player\> [time] | tnms.adminutil.management.ingame.command.blind | プレイヤーをブラインド (暗転)。デフォルト999秒 |
| !unblind \<player\> | tnms.adminutil.management.ingame.command.blind | ブラインド解除 |
| !shake \<player\> [time] | tnms.adminutil.management.ingame.command.shake | 画面を揺らす。デフォルト999秒 |
| !unshake \<player\> | tnms.adminutil.management.ingame.command.shake | 画面揺れ停止 |
| !color \<player\> [color] | tnms.adminutil.management.ingame.command.color | プレイヤーの色を変更。色指定なしで白にリセット |
| !glow \<player\> \<0\|1\> [color] | tnms.adminutil.management.ingame.command.glow | アウトラインのグロー切替 (色指定可) |

使用可能な色: `red`, `green`, `blue`, `yellow`, `orange`, `purple`, `cyan`, `pink`, `white`, `black`

### チーム / アイテム / その他

| コマンド | 権限ノード | 説明 |
|---|---|---|
| !team \<player\> \<ct\|t\|spec\> | tnms.adminutil.management.ingame.command.team | チーム変更 (プレイヤーは死亡) |
| !swap \<player\> | tnms.adminutil.management.ingame.command.team | 反対チームにスワップ (死亡せず) |
| !give \<player\> \<weapon\> | tnms.adminutil.management.ingame.command.give | 武器を付与 (`weapon_` プレフィックス自動補完) |
| !strip \<player\> | tnms.adminutil.management.ingame.command.strip | 全武器を剥奪 |
| !drop \<player\> \<weaponIndex\> | tnms.adminutil.management.ingame.command.drop | 指定インデックスの武器を強制ドロップ |
| !rename \<player\> \<newname\> | tnms.adminutil.management.ingame.command.rename | プレイヤー名を変更 |

### テレポート

| コマンド | 権限ノード | 説明 |
|---|---|---|
| !bring \<player\> | tnms.adminutil.management.ingame.command.bring | プレイヤーを実行者の位置にテレポート |
| !goto \<player\> | tnms.adminutil.management.ingame.command.goto | プレイヤーの位置にテレポート |
| !send \<target\> \<to\> | tnms.adminutil.management.ingame.command.send | プレイヤーを別プレイヤーの位置にテレポート |

## サーバー管理コマンド

| コマンド | 権限ノード | 説明 |
|---|---|---|
| !rcon \<command\> | tnms.adminutil.management.server.command.rcon | サーバーコンソールコマンドを実行 |
| !cvar \<cvar\> [value] | tnms.adminutil.management.server.command.cvar | ConVar の表示・設定 |
| !settime \<seconds\> | tnms.adminutil.management.ingame.command.settime | ラウンドタイマーを設定 |
| !addtime \<seconds\> | tnms.adminutil.management.ingame.command.addtime | ラウンド時間を追加/短縮 |
| !endround [reason] | tnms.adminutil.management.ingame.command.terminateround | ラウンドを強制終了 |

## クライアント管理コマンド

| コマンド | 権限ノード | 説明 |
|---|---|---|
| !qcvar \<target\> \<cvar\> | tnms.adminutil.management.client.command.querycvar | クライアントの ConVar 値を照会 |
| !rcvar \<target\> \<cvar\> \<value\> | tnms.adminutil.management.client.command.replicatecvar | クライアントに ConVar 値を複製 |

## 投票コマンド

| コマンド | 権限ノード | 説明 |
|---|---|---|
| !vote \<question\> \<option1\> \<option2\> [...] | tnms.adminutil.vote.command.vote | 複数選択肢投票を開始 (NativeVoteManagerMS) |
| !ynvote \<question\> | tnms.adminutil.vote.command.vote | Yes/No 投票を開始 (CS2 ネイティブ投票 UI) |

## ターゲット指定子

`<player>` パラメータは以下のターゲット指定子に対応しています:

| 指定子 | 説明 |
|---|---|
| `name` | 名前の部分一致 |
| `@all` | 全プレイヤー |
| `@me` | 自分自身 |
| `@ct` | カウンターテロリスト |
| `@t` | テロリスト |
| `@spec` | 観戦者 |
| `@alive` | 生存プレイヤー |
| `@dead` | 死亡プレイヤー |
| `@bots` | BOT |
| `@humans` | 人間プレイヤー |
