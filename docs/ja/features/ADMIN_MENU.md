# 管理メニュー

`!admin` で、管理コマンドを入力せずに実行できるメニューを開きます。

権限: `tnms.adminutil.menu`

> コマンドの `OnRegistered` フックを持つ TnmsPluginFoundation (commit `8267947` 以降) が必要です。
> このバージョンを入れる前に `shared/TnmsPluginFoundation` を差し替えてください。古い Foundation のままではプラグインの読み込みに失敗します。

## メニューの構成

| 最初の段 | 流れ |
|---|---|
| プレイヤー | 対象 → コマンド → オプション → 確認 → 実行 |
| 通常コマンド | コマンド → オプション → 対象 → 確認 → 実行 |
| サーバーコマンド | コマンド → オプション → 確認 → 実行 |
| 通知コマンド (say / asay / csay / hsay / psay / toast) | コマンド → (対象) → メッセージ → 確認 → 実行 |

- 最初の段の並びは「パネルを開く (権限がある場合) / お気に入り / プレイヤー / 通常 / サーバー / 通知」。お気に入りは空でも表示します (番号がずれないように)。
- コマンドの一覧は `!slay | キル` のように、チャットで打つコマンドと表示名を並べて表示します。
- プレイヤーの段には、対象を取るコマンドが分類に関係なく並びます。
- 対象の一覧には `@all` / `@ct` / `@t` / `@spec` と、対象にできるプレイヤーが並びます。
- 権限の無いコマンドは表示されません。コマンドが1つも無い段も表示されません。
- 各ページに「戻る」があります。実行後はメニューが閉じます。

メニューはコマンドを本人として実行します (`ms_<コマンド> ...`)。権限チェック、ログ、全体通知はチャットで入力した場合と同じです。

## オプション

| 種類 | 選び方 |
|---|---|
| プリセット | `menu.json` の値から選ぶ。「チャットで入力」で任意の値も可 |
| 選択肢 | 固定の一覧 (チーム、色、オン/オフ、ラウンド終了理由) |
| テキスト | チャットで入力 |
| 省略可 | 「既定値 (スキップ)」でコマンド側の既定値を使う |

### テキスト入力

テキストが必要な段では、メニューを閉じてチャットで入力を求めます。

- 次のチャット発言が値として使われ、チャットには流れません。
- `cancel` で中止します。
- 60秒で無効になり、それ以降の発言は通常のチャットとして扱われます。
- 入力後、残りの段は新しいメニューとして開きます。

投票の選択肢はカンマ区切り (`,` または `、`) でまとめて入力します。2個以上必要です。

## menu.json

プラグインの読み込み時に読み込みます (無ければモジュールのフォルダに既定のプリセットで作られます)。
編集したら `!adminmenu_reload` (権限 `tnms.adminutil.menu.reload`) で読み直してください。
無いキーは既定値が使われます。

```json
{
  "Presets": {
    "slap": ["0", "1", "5", "10", "50", "100"],
    "give": ["ak47", "m4a1_silencer", "awp", "deagle", "knife"]
  }
}
```

| キー | 使うところ |
|---|---|
| slap / hp / money / setkev / drop / gravity / speed | コマンドの値 |
| freeze / blind / shake | 時間 (秒) |
| give | 武器の一覧 |
| addtime / settime | 秒数 |
| terminateround | 遅延 (秒) |

## コマンド一覧のキャッシュ

管理者ごとの「使えるコマンドの一覧」は、参加して権限の読み込みが終わった時点で作り、メニューやパネルはそれを使います。
権限が変わったとき、コマンドの登録が変わったとき、`!adminmenu_reload` を実行したときに作り直します。

## 翻訳

メニューの文言は `lang/<culture>.json` の `AdminMenu.*` キーです。
コマンドの表示名は `AdminMenu.Command.<コマンド名>` で、キーが無い場合はコマンド名がそのまま表示されます。

## コマンドをメニューに載せる

コマンドの `OnRegistered` でエントリーを登録します。引数はコマンドラインの順に並べます。

```csharp
private const string Permission = "tnms.adminutil.management.ingame.command.slap";

protected override void OnRegistered()
    => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(
        AdminMenuEntry.Create(CommandName, Permission).Target().Preset("slap").Optional());
```

| メソッド | 引数 |
|---|---|
| `Target(allowSelectors)` | プレイヤー選択。最初のものがプレイヤーの段で選んだ対象になる |
| `Preset(key)` | `menu.json` のプリセット |
| `Choice(titleKey, ...)` / `Toggle()` / `TeamChoice()` | 固定の選択肢 |
| `Text(titleKey, quote)` | チャットで入力するテキスト |
| `TextList(titleKey, minCount)` | カンマ区切りのテキスト。それぞれクォートして渡す |
| `Optional(defaultRaw)` | 直前の引数を省略可にする |
| `InCategory(category)` | 載せる一覧 (`Normal` / `Server` / `Notification`)。省略時は `Target` があれば `Normal`、無ければ `Server` |
| `Id(menuId)` | お気に入りに保存する ID (既定はコマンド名)。コマンド名を変えるときは旧名を渡すと、お気に入りが引き継がれる |

`InCategory` を指定しない場合、`Target` を持たないコマンドはサーバーコマンドの段に表示されます。
