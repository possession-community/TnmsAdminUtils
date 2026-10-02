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
| TOML で追加したカテゴリ | 通常コマンドと同じ |

- 最初の段の並びは「パネルを開く (権限がある場合) / お気に入り / プレイヤー / 通常 / サーバー / 通知 / TOML のカテゴリ」。お気に入りは空でも表示します (番号がずれないように)。
- 管理パネルでは「コマンド」ページのサイドバーに「検索 / お気に入り / すべて / カテゴリ」が並びます ([ADMIN_PANEL](ADMIN_PANEL.md) を参照)。
- コマンドの一覧は `!slay | キル` のように、チャットで打つコマンドと表示名を並べて表示します。
- プレイヤーの段には、対象を取るコマンドが分類に関係なく並びます。
- 対象の一覧には `@all` / `@ct` / `@t` / `@spec`、TargetingManager に登録されたほかのセレクター (`@alive` や、プラグインの
  `@zombies` など)、対象にできるプレイヤーが並びます。
- 権限の無いコマンドは表示されません。コマンドが1つも無い段も表示されません。
- 各ページに「戻る」があります。実行後はメニューが閉じます。

メニューはコマンドを本人として実行します (`ms_<コマンド> ...`)。権限チェック、ログ、全体通知はチャットで入力した場合と同じです。

## オプション

| 種類 | 選び方 |
|---|---|
| プリセット | メニュー TOML の値から選ぶ。「チャットで入力」で任意の値も可 |
| 選択肢 | 固定の一覧 (チーム、色、オン/オフ、ラウンド終了理由) |
| テキスト | チャットで入力 |
| 省略可 | 「既定値 (スキップ)」でコマンド側の既定値を使う |

### テキスト入力

テキストが必要な段では、メニューを閉じてチャットで入力を求めます。

- 次のチャット発言が値として使われ、チャットには流れません。
- `cancel` で中止します (管理パネルではパネル側で中止します。[ADMIN_PANEL](ADMIN_PANEL.md) を参照)。
- 60秒で無効になり、それ以降の発言は通常のチャットとして扱われます。
- 入力後、残りの段は新しいメニューとして開きます。

投票の選択肢はカンマ区切り (`,` または `、`) でまとめて入力します。2個以上必要です。

## メニューの TOML (configs/menus)

モジュールのフォルダの `configs/menus/` 以下にある `*.toml` を、サブフォルダも含めてパスの順にすべて読み込み、1つのメニューとしてまとめます。
他のプラグインが自分のファイル (例: `configs/menus/myplugin.toml`) を置けば、そのコマンドをメニューに追加できます。
読み込みは全プラグインの読み込み後に1回です。編集したら `!adminmenu_reload` (権限 `tnms.adminutil.menu.reload`) で読み直してください。

既定の `configs/menus/default.toml` は、デプロイ時にサーバーに無い場合だけコピーされます (既にあれば上書きしません)。
書き方の例はこのファイルのコメントにもあります。

間違いがあると、そのテーブル (プリセット / カテゴリ / 操作) だけを読み飛ばし、ファイル名・テーブル名・理由をエラーとしてログに出します。残りは読み込まれます。
知らないキーは警告を出して無視します。

> `menu.json` は読まなくなりました。残っている場合は起動時に警告が出ます。プリセットは `[admin.menu.presets]` に移してください。

### プリセット

組み込みコマンドの値の候補です。無いキーは既定値が使われます。

```toml
[admin.menu.presets]
slap = ["0", "1", "5", "10", "50", "100"]
give = ["ak47", "m4a1_silencer", "awp", "deagle", "knife"]
```

| キー | 使うところ |
|---|---|
| slap / hp / money / setkev / drop / gravity / speed | コマンドの値 |
| freeze / blind / shake | 時間 (秒) |
| give | 武器の一覧 |
| addtime / settime | 秒数 |
| terminateround | 遅延 (秒) |
| toast | 表示時間 |

### カテゴリ

コマンドの一覧を追加します。テーブルのキー (`fun`) が ID です。組み込みの一覧の後に、読み込んだ順で並びます。

```toml
[admin.menu.category.fun]
Name = { en = "Fun Commands", ja = "おもしろコマンド" }
RequiredPermission = "myplugin.menu.fun"   # 省略可。無い管理者には一覧ごと表示しない
```

`general` / `server` / `notification` は組み込みの一覧 (通常 / サーバー / 通知) の ID で、定義し直すことはできません。

### 操作

一覧の1項目です。テーブルのキー (`fun_slap`) が ID で、お気に入りにはこの ID が保存されます。組み込みコマンドの名前と同じ ID は使えません (置き換えはできません)。

```toml
[admin.menu.operations.fun_slap]
Category = "fun"                     # 必須。追加したカテゴリか general / server / notification
Command = "slap"                     # 必須。ms_ や ! を付けないコマンド名。ms_<Command> として実行する
RequiredPermission = "tnms.adminutil.management.ingame.command.slap"   # 必須
Name = { en = "Big Slap", ja = "強めのスラップ" }
Description = { en = "Slap hard.", ja = "強めに叩きます。" }           # 省略可

[[admin.menu.operations.fun_slap.args]]
Type = "target"

[[admin.menu.operations.fun_slap.args]]
Type = "int"
Name = { en = "Damage", ja = "ダメージ" }
SuggestValues = [50, 100, 200]
Min = 0
Max = 1000
```

引数はコマンドラインの順に `[[...args]]` で書きます (最大6個)。

| Type | 内容 | キー |
|---|---|---|
| `target` | プレイヤー選択 | `AllowSelectors` (`@all` などを出す、既定 true) |
| `int` / `float` | 数値。チャットで入力した値は整数・数値かどうかと範囲を確認する | `SuggestValues`, `AllowCustomInput`, `Min`, `Max` |
| `string` | テキスト | `SuggestValues`, `AllowCustomInput`, `Quote` (1つの引数としてクォートする、既定 false) |
| `string_list` | カンマ区切りのテキスト。それぞれクォートして渡す | `MinCount` (既定 1) |
| `bool` | オン / オフ (`1` / `0`) | |
| `team` | CT / T / 観戦 (`ct` / `t` / `spec`) | |
| `choice` | 固定の選択肢 | `Choices = [{ Value = "red", Name = { en = "Red", ja = "赤" } }, { Value = "blue" }]` |

- `SuggestValues` は候補、`AllowCustomInput` は「チャットで入力」を出すかどうか (既定 true) です。候補が無く `AllowCustomInput = false` の引数はエラーです。
- どの Type にも `Name` (欄の見出し)、`Optional` (省略可)、`Default` (省略した引数より後ろに値があるときに書く値) を書けます。最後以外の省略可の引数には `Default` が必要です。
- 文字は `Name = "Text"` (全言語共通)、`Name = { en = "...", ja = "..." }` (プレイヤーの言語。無ければ en、それも無ければ最初のもの)、`NameKey = "<lang のキー>"` (TnmsAdminUtils の `lang/` のキー) のどれかで書きます。`Description` も同じです。
- `Name` を省略した操作は `AdminMenu.Command.<Command>` の翻訳を使います。

## コマンド一覧のキャッシュ

管理者ごとの「使えるコマンドの一覧」は、参加して権限の読み込みが終わった時点で作り、メニューやパネルはそれを使います。
権限が変わったとき、コマンドの登録が変わったとき、`!adminmenu_reload` を実行したときに作り直します。

## 翻訳

メニューの文言は `lang/<culture>.json` の `AdminMenu.*` キーです。
コマンドの表示名は `AdminMenu.Command.<コマンド名>` で、キーが無い場合はコマンド名がそのまま表示されます。
TOML で追加したカテゴリと操作は、TOML に書いた文字 (`Name = { en = ..., ja = ... }`) を使えます。

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
| `Preset(key)` | メニュー TOML のプリセット |
| `Choice(titleKey, ...)` / `Toggle()` / `TeamChoice()` | 固定の選択肢 |
| `Text(titleKey, quote)` | チャットで入力するテキスト |
| `TextList(titleKey, minCount)` | カンマ区切りのテキスト。それぞれクォートして渡す |
| `Optional(defaultRaw)` | 直前の引数を省略可にする |
| `InCategory(category)` | 載せる一覧 (`AdminMenuCategory.General` / `Server` / `Notification`、または TOML のカテゴリ ID)。省略時は `Target` があれば `General`、無ければ `Server` |
| `Id(menuId)` | お気に入りに保存する ID (既定はコマンド名)。コマンド名を変えるときは旧名を渡すと、お気に入りが引き継がれる |

`InCategory` を指定しない場合、`Target` を持たないコマンドはサーバーコマンドの段に表示されます。
