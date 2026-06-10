# PlayerAnimator  ─  セットアップガイド

## ファイル構成

| ファイル | 置く場所 | 役割 |
|---|---|---|
| `PlayerAnimatorConfig.cs` | Assets/Scripts/ | データ定義・設定アセット本体 |
| `PlayerAnimator.cs` | Assets/Scripts/ | GameObject にアタッチするコンポーネント |
| `BuiltinParameterDrivers.cs` | Assets/Scripts/ | 標準ドライバー集（Idle→Run、ジャンプなど） |
| `PlayerAnimatorConfigEditor.cs` | **Assets/Editor/** | インスペクター UI（Editor フォルダ必須） |

> `PlayerAnimatorConfigEditor.cs` は必ず `Assets/Editor/` フォルダに置いてください。  
> `Editor` フォルダが無ければ新規作成してください。

---

## セットアップ手順

### 1. Config アセットを作る

Project ウィンドウで右クリック  
→ **Create → Player → Animator Config**

### 2. ステートを追加する

Config アセットを選択してインスペクターを開く。  
**「＋ ステートを追加」** ボタンを押す。

| 項目 | 説明 |
|---|---|
| ステート名 | 好きな名前（例: Ground、Air、Spin）|
| デフォルト | ゲーム開始時に最初に再生するステートに★ボタンを押す |
| Clip | 再生するアニメーションクリップをドラッグ |
| UseBlendTree | Idle ↔ Run のように複数クリップをブレンドするときにチェック |

### 3. ドライバーを追加する

**「＋ ドライバーを追加」** ボタンを押してドライバーを選ぶ。

| ドライバー名 | 何をするか |
|---|---|
| 水平速度 Float | 走る速さに応じて 0〜1 の数値を流す。BlendTree で使う。 |
| 空中判定 Bool | ジャンプ・落下中かどうかを true/false で流す。 |
| コンポーネント Bool → Trigger | 別スクリプトの bool が ON になった瞬間に Trigger を送る。 |
| 速度閾値 Bool | 一定速度を超えたら true を流す（ダッシュ判定など）。 |

ドライバーを追加したら **ParameterName** に Animator のパラメータ名を入力する。  
（例: `MoveSpeed`、`IsAir`、`SpinTrigger`）

### 4. 遷移を追加する

ステートカードを開いて **「＋ 遷移を追加」** ボタンを押す。  
**遷移先** はドロップダウンで選べる（存在するステート名のみ表示）。

| 項目 | 説明 |
|---|---|
| FromAnyState | ON にするとどのステートからでもこの遷移が発動する |
| 遷移先 | どのステートに移動するか |
| HasExitTime | ON: クリップを途中まで再生してから遷移 / OFF: 条件が揃い次第すぐ遷移 |
| ブレンド時間 | 遷移にかける秒数（0.1 秒程度が自然） |
| Conditions | 遷移する条件。パラメータ名と判定方法を入力する |

### 5. GameObject にセットする

1. プレイヤーの GameObject に `PlayerAnimator` コンポーネントをアタッチ  
2. Inspector の **Config** スロットに作成した Config アセットをドラッグ  
3. Play ボタンを押す → 自動でアニメーターが生成される

---

## カスタムドライバーの作り方

`BuiltinParameterDrivers.cs` の末尾にあるテンプレートをコピーして使ってください。

```csharp
[System.Serializable]
public class MyCustomDriver : AnimatorParameterDriver
{
    public float MyValue = 1f;

    public override void Drive(DriveContext ctx)
    {
        // ヘルパーを使うと短く書ける
        SetFloatSmooth(ctx, MyValue);
    }
}
```

その後 `PlayerAnimatorConfigEditor.cs` の `DriverTypes` 配列に 1 行追加：

```csharp
("メニューに表示する名前",  "説明文",  typeof(MyCustomDriver)),
```

これだけでインスペクターの「＋ ドライバーを追加」に表示されます。

---

## よく使うパターン

### Idle ↔ Run のブレンド

1. ステート `Ground` を追加、UseBlendTree にチェック、BlendParameter に `MoveSpeed`
2. BlendChildren に Idle（Threshold: 0）と Run（Threshold: 1）を追加
3. ドライバーに「水平速度 Float」を追加、ParameterName を `MoveSpeed` に設定

### ジャンプ

1. ステート `Air` を追加、ジャンプクリップをセット
2. Ground → Air 遷移を追加：Conditions に `IsAir = true`
3. Air → Ground 遷移を追加：Conditions に `IsAir = false`
4. ドライバーに「空中判定 Bool」を追加、ParameterName を `IsAir` に設定

### 単発アクション（スピン・攻撃）

1. ステート `Spin` を追加、スピンクリップをセット
2. AnyState → Spin 遷移を追加（FromAnyState にチェック）：Conditions に `SpinTrigger`
3. Spin → Ground 遷移を追加：HasExitTime にチェック、ExitTime を 0.9
4. ドライバーに「コンポーネント Bool → Trigger」を追加  
   ComponentName: `PlayerSpin`、PropertyName: `IsSpinning`、ParameterName: `SpinTrigger`
