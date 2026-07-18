using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// チュートリアルの1ステップ分の設定を保持する ScriptableObject
///
/// TutorialManager がこのデータを読み込み、
/// 指定された条件を満たしたら選択リストへ戻る
///
/// 主な用途
/// ・選択ボタンに表示する名前
/// ・表示するメッセージ
/// ・クリア条件
/// ・プレイヤーの出現位置・回転（このステップ専用の固定位置）
/// ・ステップ開始時に出現させるオブジェクト
/// ・GoalZone の設定
/// ・開始時／完了時イベント
/// </summary>
[CreateAssetMenu(fileName = "TutorialStep", menuName = "Tutorial/TutorialStep")]
public class TutorialStep : ScriptableObject
{
    // ─────────────────────────────────────────
    // 選択リスト表示設定
    // ─────────────────────────────────────────

    [Header("選択リスト表示設定")]
    // 選択画面のボタンに表示する名前
    // 例：「ジャンプの練習」「スピンで敵を倒す」など
    [CustomLabel("ステップ名（選択ボタンのラベル）")]
    public string stepName = "ステップ";

    // ─────────────────────────────────────────
    // 表示設定
    // ─────────────────────────────────────────

    [Header("表示設定")]
    // ステップ開始時に表示する説明文
    // 「ジャンプしてください」など
    [CustomLabel("表示するメッセージ")]
    [TextArea(2, 5)]
    public string message = "表示するメッセージをここに書いてください。";

    // ─────────────────────────────────────────
    // プレイヤー出現設定
    // ─────────────────────────────────────────

    [Header("プレイヤー出現設定")]
    // このステップ開始時にプレイヤーをテレポートさせる座標
    // ステップごとに固定の位置をあらかじめ設定しておく
    [CustomLabel("プレイヤー出現座標")]
    public Vector3 playerSpawnPosition;

    // このステップ開始時にプレイヤーへ設定する回転（オイラー角）
    [CustomLabel("プレイヤー出現回転（オイラー角）")]
    public Vector3 playerSpawnRotation;

    // ─────────────────────────────────────────
    // クリア条件
    // ─────────────────────────────────────────

    /// <summary>
    /// ステップをクリアする条件
    /// TutorialManager が現在の条件に応じて判定を行う
    /// </summary>
    [Tooltip("クリア条件")]
    public enum ClearCondition
    {
        Run,                 // 指定時間走り続ける
        Jump,                // ジャンプする
        Spin,                // スピンする
        SpinHitEnemy,        // スピンで敵を倒す
        ReachGoalZone,       // 指定 GoalZone に到達する
        ManualClear,         // 外部スクリプトから手動クリア
        AutoClear,           // 一定時間後に自動クリア
        CollectCoin,         // コインを取得する
        CoinHitEnemy,        // コインを投げて敵を倒す
        ActivateSpinSwitch,  // スピンスイッチを起動する
    }

    // SpinSwitch 用（ReachGoalZone の goalZoneObjectName と同じ考え方）
    // 空文字なら「どのスイッチでもOK」として扱う
    [CustomLabel("対象スイッチID（ActivateSpinSwitch時のみ、空=どれでも可）"), SerializeField]
    public string targetSwitchId = "";

    // このステップで使用するクリア条件
    [CustomLabel("クリア条件")]
    public ClearCondition clearCondition = ClearCondition.Jump;

    // ─────────────────────────────────────────
    // AutoClear 設定
    // ─────────────────────────────────────────

    [Header("AutoClear 設定（AutoClear 条件時）")]

    // AutoClear 条件時に
    // 何秒後に自動クリアするか
    [CustomLabel("自動クリアまでの表示時間（秒）")]
    public float autoClearDelay = 2.0f;

    // ─────────────────────────────────────────
    // Run 設定
    // ─────────────────────────────────────────

    [Header("Run 設定（Run 条件時）")]

    // プレイヤーが走り続ける必要がある合計時間
    // TutorialManager が蓄積時間を管理する
    [CustomLabel("走る必要がある合計時間（秒）")]
    public float runRequiredTime = 3.0f;

    // ─────────────────────────────────────────
    // 回数設定
    // ─────────────────────────────────────────

    [Header("回数設定（Jump / Spin / SpinHitEnemy 条件時）")]

    // Jump・Spin・SpinHitEnemy でクリアに必要な回数
    // 1のままなら従来通り1回で即クリア
    [CustomLabel("クリアに必要な回数")]
    public int requiredCount = 1;

    // ─────────────────────────────────────────
    // クリア演出設定
    // ─────────────────────────────────────────

    [Header("クリア演出設定")]

    // クリア条件を満たした直後に表示するメッセージ
    // 空の場合は message をそのまま表示し続ける
    [CustomLabel("クリア時に表示するメッセージ")]
    [TextArea(2, 3)]
    public string clearMessage = "クリア！";

    // ─────────────────────────────────────────
    // オブジェクト出現設定
    // ─────────────────────────────────────────

    [Header("オブジェクト出現設定")]

    // オブジェクト参照・出現座標・出現回転をまとめた構造体
    [CustomLabel("ステップ開始時に出現させるオブジェクト")]
    public SpawnObjectData[] spawnObjects = new SpawnObjectData[0];

    // ステップ完了時に生成オブジェクトを削除するか
    [CustomLabel("ステップ終了時に出現オブジェクトを消す")]
    public bool despawnOnComplete = true;

    // ─────────────────────────────────────────
    // GoalZone 連携
    // ─────────────────────────────────────────

    [Header("GoalZone 設定（ReachGoalZone 条件時）")]

    // ReachGoalZone 条件時に使用する
    // TutorialGoalZone オブジェクト名
    //
    // シーン内の GameObject 名と一致させる必要がある
    [CustomLabel("対象の TutorialGoalZone オブジェクト名")]
    public string goalZoneObjectName = "";

    // ─────────────────────────────────────────
    // イベント
    // ─────────────────────────────────────────

    [Header("イベント（Inspector から設定不可・コードで登録）")]

    /// <summary>
    /// ステップ開始時に呼ばれるイベント
    /// コードから AddListener で登録して使用する
    /// </summary>
    [System.NonSerialized]
    public UnityEvent onStepStart = new UnityEvent();

    /// <summary>
    /// ステップ完了時に呼ばれるイベント
    /// コードから AddListener で登録して使用する
    /// </summary>
    [System.NonSerialized]
    public UnityEvent onStepComplete = new UnityEvent();
}

// オブジェクト参照・出現座標・出現回転をまとめた構造体
[System.Serializable]
public class SpawnObjectData
{
    [CustomLabel("出現させる Prefab")]
    public GameObject prefab;

    [CustomLabel("出現座標")]
    public Vector3 position;

    [CustomLabel("出現回転")]
    public Vector3 rotation;
}