using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// チュートリアル全体の進行を管理するシングルトン
///
/// 主な役割：
/// ・TutorialStep リストを順番に実行
/// ・クリア条件（Jump / Spin / Run / ReachGoalZone など）の受付と判定
/// ・UI（メッセージ・進行度テキスト・ゲージ）の更新
/// ・ステップごとのオブジェクト出現・消去
/// ・GoalZone の有効化・無効化
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    // ─────────────────────────────────────────
    // インスペクタ設定
    // ─────────────────────────────────────────

    [Header("ステップ設定")]
    // 実行するチュートリアルの手順一覧
    // 上から順に処理されるため、並び順がそのまま進行順になる
    [CustomLabel("ステップリスト（順番に実行）"), SerializeField]
    private List<TutorialStep> steps = new List<TutorialStep>();

    [Header("UI")]
    // 画面上に表示する説明テキスト
    // 各ステップの message がここに反映される
    [CustomLabel("メッセージテキスト（TMP）"), SerializeField]
    private TMP_Text messageText;

    // チュートリアルUI全体の親オブジェクト
    // チュートリアル開始時に表示し、完了後に非表示にする
    [CustomLabel("チュートリアルUIルート（非表示切替用）"), SerializeField]
    private GameObject uiRoot;

    // 現在のステップ番号を示すテキスト（例：1/10）
    [CustomLabel("ステップ進行度テキスト（TMP）例：1/10）"), SerializeField]
    private TMP_Text stepCountText;

    // Run 条件ステップのみ表示するゲージ全体の親オブジェクト
    // 他の条件では自動で非表示になる
    [CustomLabel("ゲージのルートオブジェクト（Run条件時のみ表示）"), SerializeField]
    private GameObject gaugeRoot;

    // ゲージの進捗を 0〜1 で反映する Slider
    // value = 走行時間 / 必要時間 で更新される
    [CustomLabel("ゲージの Slider（value 0〜1 で制御）"), SerializeField]
    private Slider gaugeSlider;

    [Header("タイミング設定")]
    // 最初のステップ開始前に挟む待機時間
    // 画面遷移直後に急に表示されないよう余裕を持たせる
    [CustomLabel("ステップ開始前の待機時間（秒）"), SerializeField]
    private float stepStartDelay = 0.5f;

    // 1 ステップ完了メッセージを表示してから次へ進むまでの時間
    // プレイヤーが完了を認識できるよう短い間を置く
    [CustomLabel("ステップ完了後の待機時間（秒）"), SerializeField]
    private float stepCompleteDelay = 1.0f;

    // 全ステップ完了時に表示するメッセージ
    [CustomLabel("チュートリアル完了メッセージ"), SerializeField]
    private string completeMessage = "チュートリアル完了！";

    // 完了メッセージを表示してから UI を非表示にするまでの時間
    [CustomLabel("完了後にUIを非表示にするまでの時間（秒）"), SerializeField]
    private float hideUIDelay = 3.0f;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    // 現在処理中のステップ番号（-1 = まだ開始していない）
    private int _currentStepIndex = -1;

    // チュートリアルが進行中かどうか
    private bool _isRunning = false;

    // クリア処理の二重実行を防ぐフラグ
    // TryClearStep が同一ステップで複数回呼ばれても一度しか走らないようにする
    private bool _stepClearing = false;

    // 現在のステップで Instantiate したオブジェクトを追跡するリスト
    // ステップ完了時にまとめて破棄できるよう保持する
    private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

    // 現在有効化している GoalZone への参照
    // ステップ完了時に非表示化するために保持する
    private TutorialGoalZone _currentGoalZone;

    // Run ステップ用：プレイヤーが走り続けた累積時間（秒）
    private float _runAccumulatedTime = 0f;

    // 各通知メソッドから参照するプレイヤーコンポーネント
    private PlayerController _playerController;
    private PlayerSpin _playerSpin;

    // ─────────────────────────────────────────
    // 公開プロパティ
    // ─────────────────────────────────────────

    public int CurrentStepIndex => _currentStepIndex;
    public bool IsRunning => _isRunning;

    // 範囲外アクセスを避けつつ現在の TutorialStep を返す
    public TutorialStep CurrentStep =>
        (_currentStepIndex >= 0 && _currentStepIndex < steps.Count)
        ? steps[_currentStepIndex] : null;

    // ─────────────────────────────────────────
    // 初期化
    // ─────────────────────────────────────────

    private void Awake()
    {
        // シングルトン保証：重複インスタンスは即破棄
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // タグ "Player" でプレイヤーを自動検索し、必要なコンポーネントを取得
        // 手動でアサインする手間を省くための自動参照
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerController = player.GetComponent<PlayerController>();
            _playerSpin = player.GetComponent<PlayerSpin>();
        }

        // 開始前は UI を非表示にしておく
        if (uiRoot != null) uiRoot.SetActive(false);
        if (gaugeRoot != null) gaugeRoot.SetActive(false);

        // ステップが登録されていれば自動でチュートリアルを開始
        if (steps.Count > 0)
            StartCoroutine(StartTutorial());
    }

    // ─────────────────────────────────────────
    // チュートリアル進行
    // ─────────────────────────────────────────

    /// <summary>
    /// チュートリアルを開始するコルーチン
    ///
    /// 開始ディレイを挟んでから最初のステップへ進む
    /// </summary>
    private IEnumerator StartTutorial()
    {
        _isRunning = true;
        if (uiRoot != null) uiRoot.SetActive(true);

        yield return new WaitForSeconds(stepStartDelay);
        AdvanceToStep(0);
    }

    /// <summary>
    /// 指定インデックスのステップを開始する
    ///
    /// 全ステップを終えた場合は完了処理へ移行する
    /// </summary>
    private void AdvanceToStep(int index)
    {
        // 全ステップ消化したらチュートリアル完了へ
        if (index >= steps.Count)
        {
            StartCoroutine(CompleteTutorial());
            return;
        }

        _currentStepIndex = index;
        _stepClearing = false;
        _runAccumulatedTime = 0f;

        TutorialStep step = steps[index];
        Debug.Log($"[Tutorial] ステップ {index + 1}/{steps.Count}：{step.stepName}");

        // 進行度テキストを現在のインデックスに合わせて更新
        UpdateStepCountText();

        // ステップの説明文をUIへ反映
        ShowMessage(step.message);

        // Run 条件のときだけゲージを表示し、それ以外は非表示にする
        bool isRunStep = step.clearCondition == TutorialStep.ClearCondition.Run;
        if (gaugeRoot != null) gaugeRoot.SetActive(isRunStep);
        if (gaugeSlider != null) gaugeSlider.value = 0f;

        // このステップに紐づくオブジェクトをシーンへ出現させる
        SpawnStepObjects(step);

        // GoalZone をシーンから探して有効化する
        ActivateGoalZone(step);

        // ステップ開始イベントを発火（外部から追加処理を差し込めるようにするため）
        step.onStepStart?.Invoke();

        // AutoClear 条件の場合は内部タイマーで自動クリアする
        if (step.clearCondition == TutorialStep.ClearCondition.AutoClear)
            StartCoroutine(AutoClearCoroutine(step));
    }

    // ─────────────────────────────────────────
    // クリア条件チェック（外部から通知を受ける）
    // ─────────────────────────────────────────

    /// <summary>プレイヤーがジャンプしたときに呼ぶ（PlayerController から）</summary>
    public void NotifyJump()
    {
        if (CurrentStep?.clearCondition == TutorialStep.ClearCondition.Jump)
            TryClearStep();
    }

    /// <summary>プレイヤーがスピンしたときに呼ぶ（PlayerSpin から）</summary>
    public void NotifySpin()
    {
        if (CurrentStep?.clearCondition == TutorialStep.ClearCondition.Spin)
            TryClearStep();
    }

    /// <summary>スピンで敵を倒したときに呼ぶ（EnemyBase から）</summary>
    public void NotifySpinHitEnemy()
    {
        if (CurrentStep?.clearCondition == TutorialStep.ClearCondition.SpinHitEnemy)
            TryClearStep();
    }

    /// <summary>
    /// GoalZone に入ったときに TutorialGoalZone から呼ばれる
    ///
    /// ゾーン名が指定されている場合は一致するゾーンのみ受理する
    /// </summary>
    public void NotifyReachedGoalZone(string zoneName)
    {
        if (CurrentStep?.clearCondition != TutorialStep.ClearCondition.ReachGoalZone) return;

        // goalZoneObjectName が空でない場合は名前の一致を確認する
        // 複数 GoalZone が存在するシーンで誤判定を防ぐため
        if (CurrentStep.goalZoneObjectName != "" &&
            CurrentStep.goalZoneObjectName != zoneName) return;

        TryClearStep();
    }

    /// <summary>外部スクリプトから手動でクリアする</summary>
    public void ClearCurrentStep()
    {
        if (CurrentStep?.clearCondition == TutorialStep.ClearCondition.ManualClear)
            TryClearStep();
    }

    /// <summary>
    /// プレイヤーが走っているフレームごとに呼ぶ（PlayerController から）
    ///
    /// deltaTime を渡して蓄積走行時間を加算し、ゲージへ反映する
    /// 必要時間を満たしたらステップをクリアする
    /// </summary>
    public void NotifyRunning(float deltaTime)
    {
        if (CurrentStep?.clearCondition != TutorialStep.ClearCondition.Run) return;

        // クリア処理が始まっていれば加算を停止する
        if (_stepClearing) return;

        _runAccumulatedTime += deltaTime;

        // 蓄積時間を 0〜1 に正規化してゲージへ反映
        float required = CurrentStep.runRequiredTime;
        float ratio = Mathf.Clamp01(_runAccumulatedTime / required);
        if (gaugeSlider != null)
            gaugeSlider.value = ratio;

        if (_runAccumulatedTime >= required)
            TryClearStep();
    }

    // ─────────────────────────────────────────
    // ステップクリア処理
    // ─────────────────────────────────────────

    /// <summary>
    /// ステップのクリアを試みる
    ///
    /// _stepClearing フラグで二重実行を防いでいる
    /// </summary>
    private void TryClearStep()
    {
        if (_stepClearing || CurrentStep == null) return;
        _stepClearing = true;
        StartCoroutine(CompleteStepCoroutine());
    }

    /// <summary>
    /// ステップ完了処理のコルーチン
    ///
    /// イベント発火 → GoalZone 非表示 → スポーンオブジェクト消去 →
    /// 完了メッセージ表示 → 次ステップへ進む
    /// </summary>
    private IEnumerator CompleteStepCoroutine()
    {
        TutorialStep step = CurrentStep;

        Debug.Log($"[Tutorial] ステップ完了：{step.stepName}");

        // ステップ完了イベントを発火（外部での追加処理に対応するため）
        step.onStepComplete?.Invoke();

        // このステップ用の GoalZone を非表示にする
        if (_currentGoalZone != null)
        {
            _currentGoalZone.gameObject.SetActive(false);
            _currentGoalZone = null;
        }

        // ゲージは Run ステップ以外では使わないため、完了時に隠す
        if (gaugeRoot != null) gaugeRoot.SetActive(false);

        // 完了後に出現オブジェクトが不要なら一括破棄する
        if (step.despawnOnComplete)
            DespawnStepObjects();

        ShowMessage($"{step.stepName}");
        yield return new WaitForSeconds(stepCompleteDelay);

        AdvanceToStep(_currentStepIndex + 1);
    }

    /// <summary>
    /// 全ステップ完了後の処理
    ///
    /// 完了メッセージを一定時間表示してから UI を非表示にする
    /// </summary>
    private IEnumerator CompleteTutorial()
    {
        _isRunning = false;
        ShowMessage(completeMessage);
        Debug.Log("[Tutorial] チュートリアル完了！");

        yield return new WaitForSeconds(hideUIDelay);
        if (uiRoot != null) uiRoot.SetActive(false);
    }

    // ─────────────────────────────────────────
    // オブジェクト出現・消去
    // ─────────────────────────────────────────

    /// <summary>
    /// ステップに登録されたプレハブをシーンへ出現させる
    ///
    /// SpawnPoint が設定されていない場合は TutorialManager の位置に出現する
    /// </summary>
    private void SpawnStepObjects(TutorialStep step)
    {
        for (int i = 0; i < step.spawnPrefabs.Length; i++)
        {
            GameObject prefab = step.spawnPrefabs[i];
            if (prefab == null) continue;

            // SpawnPoint が対応するインデックスになければ自分の位置を使う
            Vector3 pos = (i < step.spawnPoints.Length && step.spawnPoints[i] != null)
                ? step.spawnPoints[i].position
                : transform.position;
            Quaternion rot = (i < step.spawnPoints.Length && step.spawnPoints[i] != null)
                ? step.spawnPoints[i].rotation
                : Quaternion.identity;

            GameObject obj = Instantiate(prefab, pos, rot);
            _spawnedObjects.Add(obj);
            Debug.Log($"[Tutorial] オブジェクト出現：{prefab.name}");
        }
    }

    /// <summary>
    /// このステップで出現させたオブジェクトをすべて破棄する
    /// </summary>
    private void DespawnStepObjects()
    {
        foreach (GameObject obj in _spawnedObjects)
            if (obj != null) Destroy(obj);
        _spawnedObjects.Clear();
    }

    // ─────────────────────────────────────────
    // GoalZone 管理
    // ─────────────────────────────────────────

    /// <summary>
    /// ステップの GoalZone をシーンから検索して有効化する
    ///
    /// ReachGoalZone 以外の条件、またはゾーン名が未設定の場合は何もしない
    /// </summary>
    private void ActivateGoalZone(TutorialStep step)
    {
        _currentGoalZone = null;
        if (step.clearCondition != TutorialStep.ClearCondition.ReachGoalZone) return;
        if (string.IsNullOrEmpty(step.goalZoneObjectName)) return;

        // 名前でシーン全体から GoalZone を検索する
        // 事前にアサインせず動的に探すことで、ステップ数が増えても管理しやすくなる
        GameObject zoneObj = GameObject.Find(step.goalZoneObjectName);
        if (zoneObj == null)
        {
            Debug.LogWarning($"[Tutorial] GoalZone '{step.goalZoneObjectName}' が見つかりません");
            return;
        }

        _currentGoalZone = zoneObj.GetComponent<TutorialGoalZone>();
        zoneObj.SetActive(true);
        Debug.Log($"[Tutorial] GoalZone 有効化：{step.goalZoneObjectName}");
    }

    // ─────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────

    /// <summary>
    /// メッセージテキストに文字列をセットする
    /// </summary>
    private void ShowMessage(string msg)
    {
        if (messageText != null)
            messageText.text = msg;
    }

    // ─────────────────────────────────────────
    // 進行度テキスト
    // ─────────────────────────────────────────

    /// <summary>
    /// 現在のステップ番号をカウントテキストへ反映する（例：2/10）
    /// </summary>
    private void UpdateStepCountText()
    {
        if (stepCountText == null) return;
        stepCountText.text = $"({_currentStepIndex + 1}/{steps.Count})";
    }

    // ─────────────────────────────────────────
    // AutoClear コルーチン
    // ─────────────────────────────────────────

    /// <summary>
    /// AutoClear 条件用のタイマーコルーチン
    ///
    /// autoClearDelay 秒後に自動でステップをクリアする
    /// </summary>
    private IEnumerator AutoClearCoroutine(TutorialStep step)
    {
        yield return new WaitForSeconds(step.autoClearDelay);
        TryClearStep();
    }
}