using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// チュートリアル選択リストの進行を管理するシングルトン
///
/// 主な役割：
/// ・ステップ数分の選択ボタンを生成し、プレイヤーが好きなステップを選べるようにする
/// ・選択されたステップ単体を実行（クリア条件の受付・判定）
/// ・UI（メッセージ・ステップ名・ゲージ）の更新
/// ・ステップごとのオブジェクト出現・消去
/// ・GoalZone の有効化・無効化
/// ・プレイヤーの出現（テレポート）・非表示
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    // 現在の進行モード
    private enum TutorialMode
    {
        Selecting, // 選択リスト表示中
        Playing,   // いずれかのステップを実行中
    }

    // ─────────────────────────────────────────
    // インスペクタ設定
    // ─────────────────────────────────────────

    [Header("ステップ設定")]
    // 選択リストに並べるチュートリアルの手順一覧
    // 上から順にボタンとして並ぶ
    [CustomLabel("ステップリスト（選択ボタンの並び順）"), SerializeField]
    private List<TutorialStep> steps = new List<TutorialStep>();

    [Header("ステップ選択リストUI")]
    // 選択ボタンを並べた画面全体のルート（非表示切替用）
    [CustomLabel("選択リストUIルート"), SerializeField]
    private GameObject selectionUIRoot;

    // ボタンを並べる親オブジェクト（Vertical/Grid Layout Group 推奨）
    [CustomLabel("ボタンを並べる親オブジェクト"), SerializeField]
    private Transform buttonContainer;

    // ステップ選択ボタンのプレハブ（子に TMP_Text を持つ Button）
    [CustomLabel("ステップ選択ボタンのプレハブ"), SerializeField]
    private Button stepButtonPrefab;

    [Header("プレイヤー")]
    // チュートリアル用プレイヤー（シーン内オブジェクトを直接アサイン推奨）
    // 未設定の場合は Tag "Player" で自動検索する
    [CustomLabel("プレイヤーオブジェクト"), SerializeField]
    private GameObject player;

    private Vector3 playerTransform = new Vector3(0, 38, 0);

    [Header("カメラ設定（Cinemachine）")]
    // 選択画面表示中に使う固定カメラ（初期位置に据え置くカメラ）
    [CustomLabel("選択画面用の固定カメラ"), SerializeField]
    private CinemachineCamera selectionCamera;

    // カメラ位置保存変数
    private Vector3 initialCameraPosition;

    // 選択画面表示中の優先度（プレイヤー追従カメラより高い値にする）
    [CustomLabel("選択画面カメラ：表示中の優先度"), SerializeField]
    private int selectionCameraActivePriority = 20;

    // ステップ実行中の優先度（プレイヤー追従カメラより低い値にする）
    [CustomLabel("選択画面カメラ：ステップ実行中の優先度"), SerializeField]
    private int selectionCameraInactivePriority = 0;

    [Header("UI")]
    // 画面上に表示する説明テキスト
    // 各ステップの message がここに反映される
    [CustomLabel("メッセージテキスト（TMP）"), SerializeField]
    private TMP_Text messageText;

    // チュートリアル実行中UI全体の親オブジェクト（選択リストとは別）
    [CustomLabel("チュートリアル実行中UIルート"), SerializeField]
    private GameObject uiRoot;

    // 現在実行中のステップ名を表示するテキスト
    [CustomLabel("ステップ名テキスト（TMP）"), SerializeField]
    private TMP_Text stepTitleText;

    // Run 条件ステップのみ表示するゲージ全体の親オブジェクト
    // 他の条件では自動で非表示になる
    [CustomLabel("ゲージのルートオブジェクト（Run条件時のみ表示）"), SerializeField]
    private GameObject gaugeRoot;

    // ゲージの進捗を 0〜1 で反映する Slider
    // value = 走行時間 / 必要時間 で更新される
    [CustomLabel("ゲージの Slider（value 0〜1 で制御）"), SerializeField]
    private Slider gaugeSlider;

    [Header("タイミング設定")]
    // ステップ選択後、実際にクリア判定を始めるまでの待機時間
    // 画面遷移直後に急に判定が始まらないよう余裕を持たせる
    [CustomLabel("ステップ開始前の待機時間（秒）"), SerializeField]
    private float stepStartDelay = 0.5f;

    // クリアメッセージを表示してから選択リストへ戻るまでの待機時間
    // ここを長くするほど、クリア後に即座に選択画面へ戻らなくなる
    [CustomLabel("クリア後、選択画面に戻るまでの待機時間（秒）"), SerializeField]
    private float stepCompleteDelay = 2.0f;

    [SerializeField] Volume globalVolume;

    private DepthOfField depthOfField;

    [Header("ゲームプレイUI（コイン・ライフ）")]
    [CustomLabel("コインキャンバス"), SerializeField]
    private GameObject coinCanvasRoot;

    [CustomLabel("ライフキャンバス"), SerializeField]
    private GameObject lifeCanvasRoot;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    private TutorialMode _mode = TutorialMode.Selecting;

    // 現在処理中のステップ番号（-1 = 選択画面中で未選択）
    private int _currentStepIndex = -1;

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

    // Jump / Spin / SpinHitEnemy ステップ用：現在の達成回数
    private int _actionCount = 0;

    // 各通知メソッドから参照するプレイヤーコンポーネント
    private PlayerController _playerController;
    private PlayerSpin _playerSpin;

    // ─────────────────────────────────────────
    // 公開プロパティ
    // ─────────────────────────────────────────

    public int CurrentStepIndex => _currentStepIndex;
    public bool IsPlayingStep => _mode == TutorialMode.Playing;

    // 範囲外アクセスを避けつつ現在の TutorialStep を返す
    // 選択画面中（_currentStepIndex == -1）は null を返す
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
        // 未アサインならタグ "Player" でプレイヤーを自動検索する
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            _playerController = player.GetComponent<PlayerController>();
            _playerSpin = player.GetComponent<PlayerSpin>();
        }

        globalVolume.profile.TryGet(out depthOfField);
        if (depthOfField == null)
            Debug.LogError("DepthOfField is not found in the global volume");

        SwitchDepthOfField(true);

        BGM.Tutorial.Play();

        // メッセージを初期化しておく
        ShowMessage("");

        // 実行中UIはまだ不要なので非表示
        if (uiRoot != null) uiRoot.SetActive(false);
        if (gaugeRoot != null) gaugeRoot.SetActive(false);

        // 選択画面開始時点ではプレイヤーは非表示にしておく
        HidePlayer();

        // カメラは選択画面用の固定カメラに合わせておく
        ActivateSelectionCamera();

        initialCameraPosition = selectionCamera.transform.position;

        // ステップ数分の選択ボタンを生成して選択画面を表示する
        BuildSelectionButtons();
        if (selectionUIRoot != null) selectionUIRoot.SetActive(true);
    }

    // ─────────────────────────────────────────
    // 選択リストUI
    // ─────────────────────────────────────────

    /// <summary>
    /// steps の内容をもとに選択ボタンを生成する
    /// 既存のボタンがあれば一度破棄してから作り直す
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

            Button btn = Instantiate(stepButtonPrefab, buttonContainer);

            TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = string.IsNullOrEmpty(step.stepName) ? $"ステップ {index + 1}" : step.stepName;

            btn.onClick.AddListener(() => StartStep(index));
        }
    }

    // ─────────────────────────────────────────
    // ステップ開始（選択ボタンから呼ばれる）
    // ─────────────────────────────────────────

    /// <summary>
    /// 指定したステップを単体で開始する
    /// 選択ボタンの onClick から呼び出される想定
    /// </summary>
    public void StartStep(int index)
    {
        // 既に別のステップ実行中なら無視（多重開始防止）
        if (_mode == TutorialMode.Playing) return;
        if (index < 0 || index >= steps.Count) return;


        StartCoroutine(StartStepCoroutine(index));

        SwitchDepthOfField(false);
    }

    private IEnumerator StartStepCoroutine(int index)
    {
        _mode = TutorialMode.Playing;

        // 選択画面を隠し、プレイヤー追従カメラ側へブレンドさせる
        if (selectionUIRoot != null) selectionUIRoot.SetActive(false);
        ActivateStepCamera();

        TutorialStep step = steps[index];

        // プレイヤーをこのステップの固定位置へ出現させる
        TeleportPlayer(step.playerSpawnPosition, step.playerSpawnRotation);
        ShowPlayer();

        // 画面遷移直後にいきなり判定が始まらないよう少し待つ
        yield return new WaitForSeconds(stepStartDelay);

        _currentStepIndex = index;
        _stepClearing = false;
        _runAccumulatedTime = 0f;
        _actionCount = 0;

        // 実行中UIを表示
        if (uiRoot != null) uiRoot.SetActive(true);

        // ステップ名・説明文をUIへ反映
        if (stepTitleText != null) stepTitleText.text = step.stepName;
        ShowMessage(step.message);

        // ステップ内容に応じてゲームプレイUIを表示
        UpdateGameplayCanvases(step);

        // Run / Jump / Spin / SpinHitEnemy は進捗が数値化できるためゲージを表示する
        // それ以外の条件（ReachGoalZone・ManualClear・AutoClear）では非表示
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
        TryIncrementCount(TutorialStep.ClearCondition.Jump);
    }

    /// <summary>プレイヤーがスピンしたときに呼ぶ（PlayerSpin から）</summary>
    public void NotifySpin()
    {
        TryIncrementCount(TutorialStep.ClearCondition.Spin);
    }

    /// <summary>スピンで敵を倒したときに呼ぶ（EnemyBase から）</summary>
    public void NotifySpinHitEnemy()
    {
        TryIncrementCount(TutorialStep.ClearCondition.SpinHitEnemy);
    }

    /// <summary>
    /// Jump / Spin / SpinHitEnemy 共通の回数カウント処理
    /// 現在のステップの条件と一致する場合のみカウントし、
    /// requiredCount に達したらステップをクリアする
    /// </summary>
    private void TryIncrementCount(TutorialStep.ClearCondition condition)
    {
        if (CurrentStep?.clearCondition != condition) return;

        // クリア演出が始まっていればカウントを止める
        if (_stepClearing) return;

        _actionCount++;

        int required = Mathf.Max(1, CurrentStep.requiredCount);
        float ratio = Mathf.Clamp01((float)_actionCount / required);
        if (gaugeSlider != null)
            gaugeSlider.value = ratio;

        if (_actionCount >= required)
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
    /// 完了表示の間 → 選択リストへ戻る
    /// クリア済みの記録（スタンプ等）は一切残さないため、
    /// 何度でも同じステップを選び直して確認できる
    /// </summary>
    private IEnumerator CompleteStepCoroutine()
    {
        TutorialStep step = CurrentStep;

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

        // クリアしたことが分かるようメッセージを切り替えてから待機する
        // （即座に選択画面へ戻ると分かりづらいため、必ずこの間を置く）
        ShowMessage(string.IsNullOrEmpty(step.clearMessage) ? step.message : step.clearMessage);

        yield return new WaitForSeconds(stepCompleteDelay);

        ReturnToSelection();
    }

    /// <summary>
    /// 選択リスト画面へ戻る
    /// プレイヤーを非表示にし、カメラを選択画面用の固定カメラへ戻す
    /// </summary>
    private void ReturnToSelection()
    {
        _mode = TutorialMode.Selecting;
        _currentStepIndex = -1;

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
    /// プレイヤーを指定座標・回転へ強制的に移動させる
    /// Rigidbody がある場合は速度もリセットし、
    /// GravityBody があれば重力姿勢を強制同期する（ワープ時と同じ処理）
    /// </summary>
    private void TeleportPlayer(Vector3 position, Vector3 rotationEuler)
    {
        if (player == null) return;

        Quaternion rotation = Quaternion.Euler(rotationEuler);
        Rigidbody rb = player.GetComponent<Rigidbody>();

        if (rb != null)
        {
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

        // プレイヤーの位置を設定した位置へ変更
        TeleportPlayer(playerTransform, Vector3.zero);
    }

    /// <summary>
    /// プレイヤーを非表示にする（破棄はしない方針）
    /// 選択画面に戻るたびに呼ばれる
    /// </summary>
    private void HidePlayer()
    {
        if (player != null) player.SetActive(false);
    }

    // ─────────────────────────────────────────
    // カメラ（選択画面用固定カメラ ⇔ プレイヤー追従カメラ）
    // ─────────────────────────────────────────

    /// <summary>
    /// 選択画面用の固定カメラの優先度を上げて、
    /// Cinemachine Brain に選択画面カメラへブレンドさせる
    /// </summary>
    private void ActivateSelectionCamera()
    {
        if (selectionCamera != null)
            selectionCamera.Priority = selectionCameraActivePriority;
    }

    /// <summary>
    /// 選択画面用の固定カメラの優先度を下げて、
    /// 既存のプレイヤー追従カメラ側へブレンドさせる
    /// </summary>
    private void ActivateStepCamera()
    {
        if (selectionCamera != null)
            selectionCamera.Priority = selectionCameraInactivePriority;
    }

    // ─────────────────────────────────────────
    // オブジェクト出現・消去
    // ─────────────────────────────────────────

    /// <summary>
    /// ステップに登録されたプレハブをシーンへ出現させる
    /// SpawnPoint が設定されていない場合は TutorialManager の位置に出現する
    /// </summary>
    private void SpawnStepObjects(TutorialStep step)
    {
        foreach (var data in step.spawnObjects)
        {
            if (data == null || data.prefab == null)
                continue;

            GameObject obj = Instantiate(
                data.prefab,
                data.position,
                Quaternion.Euler(data.rotation)
            );

            _spawnedObjects.Add(obj);

            Debug.Log(
                $"オブジェクト出現：{data.prefab.name} " +
                $"Pos={data.position} Rot={data.rotation}"
            );
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
            Debug.LogWarning($"GoalZone '{step.goalZoneObjectName}' が見つかりません");
            return;
        }

        _currentGoalZone = zoneObj.GetComponent<TutorialGoalZone>();
        zoneObj.SetActive(true);
        Debug.Log($"GoalZone 有効化：{step.goalZoneObjectName}");
    }

    // ─────────────────────────────────────────
    // UI（メッセージ）
    // ─────────────────────────────────────────

    /// <summary>
    /// メッセージテキストに文字列をセットする
    /// </summary>
    private void ShowMessage(string msg)
    {
        if (messageText != null)
            messageText.text = msg;
    }

    /// <summary>
    /// ステップのクリア条件に応じて、コインキャンバス・ライフキャンバスの
    /// 表示/非表示を切り替える。
    /// コインが絡む条件では coinCanvasRoot、
    /// 敵が絡む条件では lifeCanvasRoot を表示する（両方絡む場合は両方表示）
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

                // Run / Jump / Spin / ActivateSpinSwitch / ReachGoalZone /
                // ManualClear / AutoClear はコイン・敵のいずれにも関係しないため
                // 両方非表示のまま
        }

        if (coinCanvasRoot != null) coinCanvasRoot.SetActive(showCoin);
        if (lifeCanvasRoot != null) lifeCanvasRoot.SetActive(showLife);
    }

    /// <summary>コインを取得したときに呼ぶ（Coin から）</summary>
    public void NotifyCoinCollected()
    {
        TryIncrementCount(TutorialStep.ClearCondition.CollectCoin);
    }

    /// <summary>コインを投げて敵を倒したときに呼ぶ（LaunchedCoin から）</summary>
    public void NotifyCoinHitEnemy()
    {
        TryIncrementCount(TutorialStep.ClearCondition.CoinHitEnemy);
    }

    /// <summary>
    /// スピンスイッチが起動したときに呼ぶ（SpinSwitch から）
    /// targetSwitchId が指定されていれば一致するスイッチのみ受理する
    /// </summary>
    public void NotifySpinSwitchActivated(string switchId)
    {
        if (CurrentStep?.clearCondition != TutorialStep.ClearCondition.ActivateSpinSwitch) return;

        if (!string.IsNullOrEmpty(CurrentStep.targetSwitchId) &&
            CurrentStep.targetSwitchId != switchId) return;

        TryIncrementCount(TutorialStep.ClearCondition.ActivateSpinSwitch);
    }

    // ─────────────────────────────────────────
    // AutoClear コルーチン
    // ─────────────────────────────────────────

    /// <summary>
    /// AutoClear 条件用のタイマーコルーチン
    /// autoClearDelay 秒後に自動でステップをクリアする
    /// </summary>
    private IEnumerator AutoClearCoroutine(TutorialStep step)
    {
        yield return new WaitForSeconds(step.autoClearDelay);
        TryClearStep();
    }

    public void SwitchDepthOfField(bool _switch)
    {
        Debug.Log($"SwitchDepthOfField: {_switch}");

        if (_switch)
        {
            depthOfField.active = true;
        }
        else
        {
            depthOfField.active = false;
        }
    }
}