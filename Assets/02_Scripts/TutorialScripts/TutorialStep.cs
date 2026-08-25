using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// チュートリアルの1ステップ分の設定を保持するScriptableObject。
///
/// TutorialManagerがこのデータを読み込み、指定された条件を満たしたら選択リストへ戻る。
/// 選択ボタンの表示名、説明メッセージ、クリア条件、プレイヤーの出現位置、
/// ステップ開始時に出現させるオブジェクト、GoalZoneとの連携、開始/完了イベントを保持する。
/// </summary>
[CreateAssetMenu(fileName = "TutorialStep", menuName = "Tutorial/TutorialStep")]
public class TutorialStep : ScriptableObject
{
    [Header("選択リスト表示設定")]

    // 選択画面のボタンに表示する名前。例：「ジャンプの練習」「スピンで敵を倒す」
    [CustomLabel("ステップ名（選択ボタンのラベル）")]
    public string stepName = "ステップ";

    [Header("表示設定")]

    // ステップ開始時に表示する説明文。例：「ジャンプしてください」
    [CustomLabel("表示するメッセージ")]
    [TextArea(2, 5)]
    public string message = "表示するメッセージをここに書いてください。";

    [Header("プレイヤー出現設定")]

    // このステップ開始時にプレイヤーをテレポートさせる座標・回転（オイラー角）
    [CustomLabel("プレイヤー出現座標")]
    public Vector3 playerSpawnPosition;

    [CustomLabel("プレイヤー出現回転（オイラー角）")]
    public Vector3 playerSpawnRotation;

    /// <summary>
    /// ステップをクリアする条件。TutorialManagerが現在の条件に応じて判定を行う。
    /// </summary>
    public enum ClearCondition
    {
        Run,                 // 指定時間走り続ける
        Jump,                // ジャンプする
        Spin,                // スピンする
        SpinHitEnemy,        // スピンで敵を倒す
        ReachGoalZone,       // 指定GoalZoneに到達する
        ManualClear,         // 外部スクリプトから手動クリア
        AutoClear,           // 一定時間後に自動クリア
        CollectCoin,         // コインを取得する
        CoinHitEnemy,        // コインを投げて敵を倒す
        ActivateSpinSwitch,  // スピンスイッチを起動する
    }

    [CustomLabel("クリア条件")]
    public ClearCondition clearCondition = ClearCondition.Jump;

    // ActivateSpinSwitch時のみ使用。空文字なら「どのスイッチでもOK」として扱う
    [CustomLabel("対象スイッチID（ActivateSpinSwitch時のみ、空=どれでも可）")]
    public string targetSwitchId = "";

    [Header("AutoClear設定（AutoClear条件時）")]

    [CustomLabel("自動クリアまでの表示時間（秒）")]
    public float autoClearDelay = 2.0f;

    [Header("Run設定（Run条件時）")]

    // プレイヤーが走り続ける必要がある合計時間。TutorialManagerが蓄積時間を管理する
    [CustomLabel("走る必要がある合計時間（秒）")]
    public float runRequiredTime = 3.0f;

    [Header("回数設定（Jump / Spin / SpinHitEnemy条件時）")]

    // 1のままなら従来通り1回で即クリア
    [CustomLabel("クリアに必要な回数")]
    public int requiredCount = 1;

    [Header("クリア演出設定")]

    // クリア条件を満たした直後に表示するメッセージ。空の場合はmessageをそのまま表示し続ける
    [CustomLabel("クリア時に表示するメッセージ")]
    [TextArea(2, 3)]
    public string clearMessage = "クリア！";

    [Header("オブジェクト出現設定")]

    [CustomLabel("ステップ開始時に出現させるオブジェクト")]
    public SpawnObjectData[] spawnObjects = Array.Empty<SpawnObjectData>();

    [CustomLabel("ステップ終了時に出現オブジェクトを消す")]
    public bool despawnOnComplete = true;

    [Header("GoalZone設定（ReachGoalZone条件時）")]

    // シーン内のTutorialGoalZoneオブジェクトのGameObject名と一致させる必要がある
    [CustomLabel("対象のTutorialGoalZoneオブジェクト名")]
    public string goalZoneObjectName = "";

    [Header("イベント（Inspectorから設定不可・コードで登録）")]

    /// <summary>
    /// ステップ開始時に呼ばれるイベント。コードからAddListenerで登録して使用する。
    /// </summary>
    [NonSerialized]
    public UnityEvent onStepStart = new UnityEvent();

    /// <summary>
    /// ステップ完了時に呼ばれるイベント。コードからAddListenerで登録して使用する。
    /// </summary>
    [NonSerialized]
    public UnityEvent onStepComplete = new UnityEvent();
}

/// <summary>
/// ステップ開始時に出現させるオブジェクトの参照・出現座標・出現回転をまとめたデータ。
/// </summary>
[Serializable]
public class SpawnObjectData
{
    [CustomLabel("出現させるPrefab")]
    public GameObject prefab;

    [CustomLabel("出現座標")]
    public Vector3 position;

    [CustomLabel("出現回転")]
    public Vector3 rotation;
}