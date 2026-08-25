using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// チュートリアル選択リストの進行を管理するシングルトン。
///
/// 主な役割：
/// ・ステップ数分の選択ボタンを生成し、プレイヤーが好きなステップを選べるようにする
/// ・選択されたステップ単体を実行（クリア条件の受付・判定）
/// ・UI（メッセージ・ステップ名・ゲージ）の更新
/// ・ステップごとのオブジェクト出現・消去、GoalZoneの有効化・無効化
/// ・プレイヤーの出現（テレポート）・非表示
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    private enum TutorialMode
    {
        Selecting, // 選択リスト表示中
        Playing,   // いずれかのステップを実行中
    }

    // ─────────────────────────────────────────
    // インスペクタ設定
    // ─────────────────────────────────────────

    [Header("ステップ設定")]

    // 選択リストに並べるチュートリアルの手順一覧。上から順にボタンとして並ぶ
    [CustomLabel("ステップリスト（選択ボタンの並び順）"), SerializeField]
    private List<TutorialStep> steps = new List<TutorialStep>();

    [Header("ステップ選択リストUI")]

    [CustomLabel("選択リストUIルート"), SerializeField]
    private GameObject selectionUIRoot;

    // ボタンを並べる親オブジェクト（Vertical/Grid Layout Group推奨）
    [CustomLabel("ボタンを並べる親オブジェクト"), SerializeField]
    private Transform buttonContainer;

    // 子にTMP_Textを持つButtonプレハブ
    [CustomLabel("ステップ選択ボタンのプレハブ"), SerializeField]
    private Button stepButtonPrefab;

    [Header("プレイヤー")]

    // 未設定の場合はTag "Player" で自動検索する
    [CustomLabel("プレイヤーオブジェクト"), SerializeField]
    private GameObject player;

    [Header("カメラ設定（Cinemachine）")]

    // 選択画面表示中に使う固定カメラ
    [CustomLabel("選択画面用の固定カメラ"), SerializeField]
    private CinemachineCamera selectionCamera;

    private Vector3 initialCameraPosition;

    // プレイヤー追従カメラより高い値にする
    [CustomLabel("選択画面カメラ：表示中の優先度"), SerializeField]
    private int selectionCameraActivePriority = 20;

    // プレイヤー追従カメラより低い値にする
    [CustomLabel("選択画面カメラ：ステップ実行中の優先度"), SerializeField]
    private int selectionCameraInactivePriority = 0;

    [Header("UI")]

    [CustomLabel("メッセージテキスト（TMP）"), SerializeField]
    private TMP_Text messageText;

    // チュートリアル実行中UI全体の親オブジェクト（選択リストとは別）
    [CustomLabel("チュートリアル実行中UIルート"), SerializeField]
    private GameObject uiRoot;

    [CustomLabel("ステップ名テキスト（TMP）"), SerializeField]
    private TMP_Text stepTitleText;

    // Run条件ステップのみ表示するゲージ全体の親オブジェクト。他の条件では自動で非表示になる
    [CustomLabel("ゲージのルートオブジェクト（Run条件時のみ表示）"), SerializeField]
    private GameObject gaugeRoot;

    // value = 走行時間 / 必要時間 で更新される
    [CustomLabel("ゲージのSlider（value 0〜1で制御）"), SerializeField]
    private Slider gaugeSlider;

    [Header("タイミング設定")]

    // ステップ選択後、実際にクリア判定を始めるまでの待機時間
    [CustomLabel("ステップ開始前の待機時間（秒）"), SerializeField]
    private float stepStartDelay = 0.5f;

    // クリアメッセージを表示してから選択リストへ戻るまでの待機時間
    [CustomLabel("クリア後、選択画面に戻るまでの待機時間（秒）"), SerializeField]
    private float stepCompleteDelay = 2.0f;

    [Header("被写界深度設定")]

    [CustomLabel("被写界深度を含むGlobal Volume"), SerializeField]
    private Volume globalVolume;

    private DepthOfField depthOfField;

    [Header("ゲームプレイUI（コイン・ライフ）")]

    [CustomLabel("コインキャンバス"), SerializeField]
    private GameObject coinCanvasRoot;

    [CustomLabel("ライフキャンバス"), SerializeField]
    private GameObject lifeCanvasRoot;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    private TutorialMode mode = TutorialMode.Selecting;

    // 現在処理中のステップ番号（-1 = 選択画面中で未選択）
    private int currentStepIndex = -1;

    // TryClearStepが同一ステップで複数回呼ばれても一度しか走らないようにするフラグ
    private bool stepClearing;

    // 現在のステップでInstantiateしたオブジェクト。ステップ完了時にまとめて破棄する
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    // 現在有効化しているGoalZone。ステップ完了時に非表示化するために保持する
    private TutorialGoalZone currentGoalZone;

    // Runステップ用：プレイヤーが走り続けた累積時間（秒）
    private float runAccumulatedTime;

    // Jump / Spin / SpinHitEnemyなどのステップ用：現在の達成回数
    private int actionCount;

    private PlayerController playerController;
    private PlayerSpin playerSpin;

    // ─────────────────────────────────────────
    // 公開プロパティ
    // ─────────────────────────────────────────

    public int CurrentStepIndex => currentStepIndex;
    public bool IsPlayingStep => mode == TutorialMode.Playing;

    // 選択画面中（currentStepIndex == -1）はnullを返す
    public TutorialStep CurrentStep =>
        currentStepIndex >= 0 && currentStepIndex < steps.Count ? steps[currentStepIndex] : null;

    // ─────────────────────────────────────────
    // 初期化
    // ─────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            playerSpin = player.GetComponent<PlayerSpin>();
        }

        if (globalVolume != null && globalVolume.profile.TryGet(out depthOfField))
            SwitchDepthOfField(true);
        else
            Debug.LogError("DepthOfField is not found in the global volume");

        BGM.Tutorial.Play();

        ShowMessage("");

        if (uiRoot != null) uiRoot.SetActive(false);
        if (gaugeRoot != null) gaugeRoot.SetActive(false);

        // 選択画面開始時点ではプレイヤーは非表示にしておく
        HidePlayer();

        ActivateSelectionCamera();
        initialCameraPosition = selectionCamera.transform.position;

        BuildSelectionButtons();
        if (selectionUIRoot != null) selectionUIRoot.SetActive(true);
    }

    // ─────────────────────────────────────────
    // 選択リストUI
    // ─────────────────────────────────────────

    /// <summary>
    /// stepsの内容をもとに選択ボタンを生成する。既存のボタンは一度破棄してから作り直す。
    /// </summary>
    private void BuildSelectionButtons()
    {
        if (buttonContainer == null || stepButtonPrefab == null) return;

        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        for (int i = 0; i < steps.Count; i++)
        {
            TutorialStep step = steps[i];
            int index = i; // ラムダに渡すためのローカルコピー（クロージャ対策）

            Button button = Instantiate(stepButtonPrefab, buttonContainer);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = string.IsNullOrEmpty(step.stepName) ? $"ステップ {index + 1}" : step.stepName;

            button.onClick.AddListener(() => StartStep(index));
        }
    }

    // ─────────────────────────────────────────
    // ステップ開始（選択ボタンから呼ばれる）
    // ─────────────────────────────────────────

    /// <summary>
    /// 指定したステップを単体で開始する。選択ボタンのonClickから呼び出される想定。
    /// </summary>
    public void StartStep(int index)
    {
        if (mode == TutorialMode.Playing) return; // 多重開始防止
        if (index < 0 || index >= steps.Count) return;

        SwitchDepthOfField(false);
        StartCoroutine(StartStepCoroutine(index));
    }

    private IEnumerator StartStepCoroutine(int index)
    {
        mode = TutorialMode.Playing;

        // 選択画面を隠し、プレイヤー追従カメラ側へブレンドさせる
        if (selectionUIRoot != null) selectionUIRoot.SetActive(false);
        ActivateStepCamera();

        TutorialStep step = steps[index];

        // プレイヤーをこのステップの固定位置へ出現させる
        TeleportPlayer(step.playerSpawnPosition, step.playerSpawnRotation);
        ShowPlayer();

        // 画面遷移直後にいきなり判定が始まらないよう少し待つ
        yield return new WaitForSeconds(stepStartDelay);

        currentStepIndex = index;
        stepClearing = false;
        runAccumulatedTime = 0f;
        actionCount = 0;

        if (uiRoot != null) uiRoot.SetActive(true);

        if (stepTitleText != null) stepTitleText.text = step.stepName;
        ShowMessage(step.message);

        UpdateGameplayCanvases(step);

        // Run / Jump / Spin など進捗を数値化できる条件のみゲージを表示する
        // （ReachGoalZone・ManualClear・AutoClearでは非表示）
        bool showGauge =
            step.clearCondition == TutorialStep.ClearCondition.Run ||
            step.clearCondition == TutorialStep.ClearCondition.Jump ||
            step.clearCondition == TutorialStep.ClearCondition.Spin ||
            step.clearCondition == TutorialStep.ClearCondition.SpinHitEnemy ||
            step.clearCondition == TutorialStep.ClearCondition.CollectCoin ||
            step.clearCondition == TutorialStep.ClearCondition.CoinHitEnemy ||
            step.clearCondition == TutorialStep.ClearCondition.ActivateSpinSwitch;

        if (gaugeRoot != null) gaugeRoot.SetActive(showGauge);
        if (gaugeSlider != null) gaugeSlider.value = 0f;

        SpawnStepObjects(step);
        ActivateGoalZone(step);

        step.onStepStart?.Invoke();

        if (step.clearCondition == TutorialStep.ClearCondition.AutoClear)
            StartCoroutine(AutoClearCoroutine(step));
    }

    // ─────────────────────────────────────────
    // クリア条件チェック（外部から通知を受ける）
    // ─────────────────────────────────────────

    /// <summary>プレイヤーがジャンプしたときに呼ぶ（PlayerControllerから）</summary>
    public void NotifyJump() => TryIncrementCount(TutorialStep.ClearCondition.Jump);

    /// <summary>プレイヤーがスピンしたときに呼ぶ（PlayerSpinから）</summary>
    public void NotifySpin() => TryIncrementCount(TutorialStep.ClearCondition.Spin);

    /// <summary>スピンで敵を倒したときに呼ぶ（EnemyBaseから）</summary>
    public void NotifySpinHitEnemy() => TryIncrementCount(TutorialStep.ClearCondition.SpinHitEnemy);

    /// <summary>コインを取得したときに呼ぶ（Coinから）</summary>
    public void NotifyCoinCollected() => TryIncrementCount(TutorialStep.ClearCondition.CollectCoin);

    /// <summary>コインを投げて敵を倒したときに呼ぶ（LaunchedCoinから）</summary>
    public void NotifyCoinHitEnemy() => TryIncrementCount(TutorialStep.ClearCondition.CoinHitEnemy);

    /// <summary>
    /// Jump / Spin / SpinHitEnemyなど、回数で判定する条件の共通カウント処理。
    /// 現在のステップの条件と一致する場合のみカウントし、requiredCountに達したらクリアする。
    /// </summary>
    private void TryIncrementCount(TutorialStep.ClearCondition condition)
    {
        if (CurrentStep?.clearCondition != condition) return;
        if (stepClearing) return;

        actionCount++;

        int required = Mathf.Max(1, CurrentStep.requiredCount);
        float ratio = Mathf.Clamp01((float)actionCount / required);
        if (gaugeSlider != null)
            gaugeSlider.value = ratio;

        if (actionCount >= required)
            TryClearStep();
    }

    /// <summary>
    /// GoalZoneに入ったときにTutorialGoalZoneから呼ばれる。
    /// ゾーン名が指定されている場合は一致するゾーンのみ受理する。
    /// </summary>
    public void NotifyReachedGoalZone(string zoneName)
    {
        if (CurrentStep?.clearCondition != TutorialStep.ClearCondition.ReachGoalZone) return;

        // 複数GoalZoneが存在するシーンでの誤判定を防ぐため、名前を確認する
        if (!string.IsNullOrEmpty(CurrentStep.goalZoneObjectName) &&
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
    /// プレイヤーが走っているフレームごとに呼ぶ（PlayerControllerから）。
    /// deltaTimeを渡して蓄積走行時間を加算し、ゲージへ反映する。必要時間に達したらクリアする。
    /// </summary>
    public void NotifyRunning(float deltaTime)
    {
        if (CurrentStep?.clearCondition != TutorialStep.ClearCondition.Run) return;
        if (stepClearing) return;

        runAccumulatedTime += deltaTime;

        float required = CurrentStep.runRequiredTime;
        float ratio = Mathf.Clamp01(runAccumulatedTime / required);
        if (gaugeSlider != null)
            gaugeSlider.value = ratio;

        if (runAccumulatedTime >= required)
            TryClearStep();
    }

    /// <summary>
    /// スピンスイッチが起動したときに呼ぶ（SpinSwitchから）。
    /// targetSwitchIdが指定されていれば一致するスイッチのみ受理する。
    /// </summary>
    public void NotifySpinSwitchActivated(string switchId)
    {
        if (CurrentStep?.clearCondition != TutorialStep.ClearCondition.ActivateSpinSwitch) return;

        if (!string.IsNullOrEmpty(CurrentStep.targetSwitchId) &&
            CurrentStep.targetSwitchId != switchId) return;

        TryIncrementCount(TutorialStep.ClearCondition.ActivateSpinSwitch);
    }

    // ─────────────────────────────────────────
    // ステップクリア処理
    // ─────────────────────────────────────────

    /// <summary>
    /// ステップのクリアを試みる。stepClearingフラグで二重実行を防いでいる。
    /// </summary>
    private void TryClearStep()
    {
        if (stepClearing || CurrentStep == null) return;
        stepClearing = true;
        StartCoroutine(CompleteStepCoroutine());
    }

    /// <summary>
    /// ステップ完了処理：イベント発火 → GoalZone非表示 → スポーンオブジェクト消去 →
    /// 完了表示の間 → 選択リストへ戻る。
    /// クリア済みの記録は一切残さないため、何度でも同じステップを選び直して確認できる。
    /// </summary>
    private IEnumerator CompleteStepCoroutine()
    {
        TutorialStep step = CurrentStep;

        step.onStepComplete?.Invoke();

        if (currentGoalZone != null)
        {
            currentGoalZone.gameObject.SetActive(false);
            currentGoalZone = null;
        }

        if (gaugeRoot != null) gaugeRoot.SetActive(false);

        if (step.despawnOnComplete)
            DespawnStepObjects();

        // 即座に選択画面へ戻ると分かりづらいため、クリアメッセージを見せてから待機する
        ShowMessage(string.IsNullOrEmpty(step.clearMessage) ? step.message : step.clearMessage);

        yield return new WaitForSeconds(stepCompleteDelay);

        ReturnToSelection();
    }

    /// <summary>
    /// 選択リスト画面へ戻る。プレイヤーを非表示にし、カメラを選択画面用の固定カメラへ戻す。
    /// </summary>
    private void ReturnToSelection()
    {
        mode = TutorialMode.Selecting;
        currentStepIndex = -1;

        if (uiRoot != null) uiRoot.SetActive(false);
        if (gaugeRoot != null) gaugeRoot.SetActive(false);
        if (coinCanvasRoot != null) coinCanvasRoot.SetActive(false);
        if (lifeCanvasRoot != null) lifeCanvasRoot.SetActive(false);

        ShowMessage("");

        HidePlayer();
        ActivateSelectionCamera();
        selectionCamera.transform.position = initialCameraPosition;

        if (selectionUIRoot != null) selectionUIRoot.SetActive(true);

        SwitchDepthOfField(true);
    }

    // ─────────────────────────────────────────
    // プレイヤーの出現・非表示
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤーを指定座標・回転へ強制的に移動させる。Rigidbodyがあれば速度もリセットし、
    /// GravityBodyがあれば重力姿勢を強制同期する（ワープ時と同じ処理）。
    /// </summary>
    private void TeleportPlayer(Vector3 position, Vector3 rotationEuler)
    {
        if (player == null) return;

        Quaternion rotation = Quaternion.Euler(rotationEuler);
        Rigidbody rb = player.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            player.transform.SetPositionAndRotation(position, rotation);
        }

        // 惑星重力システムとの姿勢ズレを防ぐため、ワープ時と同じく強制同期する
        GravityBody gravityBody = player.GetComponent<GravityBody>();
        if (gravityBody != null)
            gravityBody.ForceSyncGravity();
    }

    /// <summary>プレイヤーを表示状態にする</summary>
    private void ShowPlayer()
    {
        if (player != null) player.SetActive(true);
    }

    /// <summary>プレイヤーを非表示にする（破棄はしない）。選択画面に戻るたびに呼ばれる。</summary>
    private void HidePlayer()
    {
        if (player != null) player.SetActive(false);
    }

    // ─────────────────────────────────────────
    // カメラ（選択画面用固定カメラ ⇔ プレイヤー追従カメラ）
    // ─────────────────────────────────────────

    /// <summary>選択画面用の固定カメラの優先度を上げ、Cinemachine Brainに選択画面カメラへブレンドさせる</summary>
    private void ActivateSelectionCamera()
    {
        if (selectionCamera != null)
            selectionCamera.Priority = selectionCameraActivePriority;
    }

    /// <summary>選択画面用の固定カメラの優先度を下げ、プレイヤー追従カメラ側へブレンドさせる</summary>
    private void ActivateStepCamera()
    {
        if (selectionCamera != null)
            selectionCamera.Priority = selectionCameraInactivePriority;
    }

    // ─────────────────────────────────────────
    // オブジェクト出現・消去
    // ─────────────────────────────────────────

    /// <summary>ステップに登録されたプレハブをシーンへ出現させる</summary>
    private void SpawnStepObjects(TutorialStep step)
    {
        foreach (SpawnObjectData data in step.spawnObjects)
        {
            if (data == null || data.prefab == null)
                continue;

            GameObject obj = Instantiate(data.prefab, data.position, Quaternion.Euler(data.rotation));
            spawnedObjects.Add(obj);

            Debug.Log($"オブジェクト出現：{data.prefab.name} Pos={data.position} Rot={data.rotation}");
        }
    }

    /// <summary>このステップで出現させたオブジェクトをすべて破棄する</summary>
    private void DespawnStepObjects()
    {
        foreach (GameObject obj in spawnedObjects)
            if (obj != null) Destroy(obj);

        spawnedObjects.Clear();
    }

    // ─────────────────────────────────────────
    // GoalZone管理
    // ─────────────────────────────────────────

    /// <summary>
    /// ステップのGoalZoneをシーンから検索して有効化する。
    /// ReachGoalZone以外の条件、またはゾーン名が未設定の場合は何もしない。
    /// </summary>
    private void ActivateGoalZone(TutorialStep step)
    {
        currentGoalZone = null;
        if (step.clearCondition != TutorialStep.ClearCondition.ReachGoalZone) return;
        if (string.IsNullOrEmpty(step.goalZoneObjectName)) return;

        // 事前にアサインせず名前で動的に探すことで、ステップ数が増えても管理しやすくする
        GameObject zoneObj = GameObject.Find(step.goalZoneObjectName);
        if (zoneObj == null)
        {
            Debug.LogWarning($"GoalZone '{step.goalZoneObjectName}' が見つかりません");
            return;
        }

        currentGoalZone = zoneObj.GetComponent<TutorialGoalZone>();
        zoneObj.SetActive(true);
        Debug.Log($"GoalZone有効化：{step.goalZoneObjectName}");
    }

    // ─────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────

    private void ShowMessage(string message)
    {
        if (messageText != null)
            messageText.text = message;
    }

    /// <summary>
    /// ステップのクリア条件に応じて、コインキャンバス・ライフキャンバスの表示/非表示を切り替える。
    /// コインが絡む条件ではcoinCanvasRoot、敵が絡む条件ではlifeCanvasRootを表示する
    /// （両方絡む場合は両方表示。関係しない条件は両方非表示のまま）。
    /// </summary>
    private void UpdateGameplayCanvases(TutorialStep step)
    {
        bool showCoin = false;
        bool showLife = false;

        switch (step.clearCondition)
        {
            case TutorialStep.ClearCondition.CollectCoin:
                showCoin = true;
                break;

            case TutorialStep.ClearCondition.CoinHitEnemy:
                showCoin = true;
                showLife = true;
                break;

            case TutorialStep.ClearCondition.SpinHitEnemy:
                showLife = true;
                break;
        }

        if (coinCanvasRoot != null) coinCanvasRoot.SetActive(showCoin);
        if (lifeCanvasRoot != null) lifeCanvasRoot.SetActive(showLife);
    }

    // ─────────────────────────────────────────
    // AutoClearコルーチン
    // ─────────────────────────────────────────

    /// <summary>AutoClear条件用のタイマー。autoClearDelay秒後に自動でステップをクリアする</summary>
    private IEnumerator AutoClearCoroutine(TutorialStep step)
    {
        yield return new WaitForSeconds(step.autoClearDelay);
        TryClearStep();
    }

    /// <summary>被写界深度エフェクトの有効/無効を切り替える（選択画面ではON、ステップ実行中はOFF）</summary>
    public void SwitchDepthOfField(bool enable)
    {
        if (depthOfField == null) return;
        depthOfField.active = enable;
    }
}