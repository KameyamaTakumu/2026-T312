using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// チュートリアルの1ステップ分の設定を保持する ScriptableObject
///
/// TutorialManager がこのデータを順番に読み込み、
/// 指定された条件を満たしたら次のステップへ進行する
///
/// 主な用途
/// ・表示するメッセージ
/// ・クリア条件
/// ・ステップ開始時に出現させるオブジェクト
/// ・GoalZone の設定
/// ・開始時／完了時イベント
/// </summary>
[CreateAssetMenu(fileName = "TutorialStep", menuName = "Tutorial/TutorialStep")]
public class TutorialStep : ScriptableObject
{
    // ─────────────────────────────────────────
    // 表示設定
    // ─────────────────────────────────────────

    [Header("表示設定")]

    // 管理用の名前
    // プレイヤーには表示されない
    // デバッグログやステップ管理に使用する
    [CustomLabel("ステップ名（管理用）")]
    public string stepName = "Step_01";

    // ステップ開始時に表示する説明文
    // 「ジャンプしてください」など
    [CustomLabel("表示するメッセージ")]
    [TextArea(2, 5)]
    public string message = "ジャンプしてください！";

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
        Run,            // 指定時間走り続ける
        Jump,           // ジャンプする
        Spin,           // スピンする
        SpinHitEnemy,   // スピンで敵を倒す
        ReachGoalZone,  // 指定 GoalZone に到達する
        ManualClear,    // 外部スクリプトから手動クリア
        AutoClear,      // 一定時間後に自動クリア
    }

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
    // オブジェクト出現設定
    // ─────────────────────────────────────────

    [Header("オブジェクト出現設定")]

    // ステップ開始時に生成するプレハブ一覧
    [CustomLabel("ステップ開始時に出現させる Prefab 一覧")]
    public GameObject[] spawnPrefabs = new GameObject[0];

    // 各プレハブの出現位置
    // 配列の番号を spawnPrefabs と対応させる
    [CustomLabel("出現位置（各 Prefab に対応）")]
    public Transform[] spawnPoints = new Transform[0];

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