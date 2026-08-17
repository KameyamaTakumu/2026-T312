using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// オブジェクトの表示・非表示を制御するコンポーネント
///
/// 主な役割：
/// ・対象オブジェクトの表示
/// ・対象オブジェクトの非表示
/// ・表示時にCinema Cameraの注視対象へ設定
/// ・一定時間経過、またはプレイヤーが動いたら自動でカメラを戻す
/// </summary>
public class ObjectVisibilityController : MonoBehaviour
{
    [Header("表示・非表示を切り替えるオブジェクト")]
    [SerializeField] private GameObject targetObject;

    [Header("通常時のカメラ")]
    [SerializeField] private CinemachineCamera mainCamera;

    [Header("惑星表示用カメラ")]
    [SerializeField] private CinemachineCamera planetCamera;

    [Header("カメラ自動復帰設定")]

    // 惑星カメラへ切り替えてから自動でカメラを戻すまでの時間（秒）
    // 0以下にすると時間経過による復帰を行わない
    [CustomLabel("自動復帰までの時間（秒）"), SerializeField]
    private float autoRevertTime = 3f;

    // プレイヤーの移動入力を検知してカメラを戻すか
    [CustomLabel("移動入力で復帰するか"), SerializeField]
    private bool revertOnPlayerMove = true;

    // 移動入力とみなす閾値
    // スティックのわずなブレなどを無視するためのデッドゾーン
    [CustomLabel("移動入力の検知しきい値"), SerializeField]
    private float moveInputThreshold = 0.1f;

    // ─────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────

    // 現在惑星カメラを表示中かどうか
    bool _isShowingPlanetCamera;

    // 惑星カメラへ切り替えてからの経過時間
    float _showTimer;

        void Awake()
    {
        // 起動時のPriorityをInspectorの値に依存させず、
        // 必ず「非表示状態（MainCamera優先）」で開始させる
        ChangeToMainCamera();

        if (targetObject != null)
            targetObject.SetActive(false);

        _isShowingPlanetCamera = false;
    }

    void Update()
    {
        if (!_isShowingPlanetCamera)
            return;

        // ─────────────────────────────────
        // 時間経過チェック
        // ─────────────────────────────────
        if (autoRevertTime > 0f)
        {
            _showTimer += Time.deltaTime;

            if (_showTimer >= autoRevertTime)
            {
                ChangeToMainCamera();
                return;
            }
        }

        // ─────────────────────────────────
        // プレイヤー移動入力チェック
        // ─────────────────────────────────
        if (revertOnPlayerMove && IsPlayerMoving())
        {
            ChangeToMainCamera();
        }
    }

    private void ChangeToMainCamera()
    {
        if (mainCamera != null)
            mainCamera.Priority = 100;
        if (planetCamera != null)
            planetCamera.Priority = 10;
    }

        /// <summary>
        /// プレイヤーの移動入力があるかどうかを判定する
        /// PlayerController と同じ Input.GetAxisRaw を参照することで
        /// 入力検知の挙動を一致させる
        /// </summary>
        bool IsPlayerMoving()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // しきい値を超えていれば「動いた」と判定
        return Mathf.Abs(h) > moveInputThreshold
            || Mathf.Abs(v) > moveInputThreshold;
    }

    /// <summary>
    /// オブジェクトを表示する
    /// </summary>
    public void Show()
    {
        if (targetObject == null)
            return;

        targetObject.SetActive(true);

        if (mainCamera != null)
            mainCamera.Priority = 10;
        if (planetCamera != null)
            planetCamera.Priority = 100;

        // 自動復帰タイマーをリセットして監視を開始
        _showTimer = 0f;
        _isShowingPlanetCamera = true;
    }

    /// <summary>
    /// オブジェクトを非表示にする
    /// </summary>
    public void Hide()
    {
        if (targetObject == null)
            return;

        targetObject.SetActive(false);

        // 監視終了
        _isShowingPlanetCamera = false;
    }

    /// <summary>
    /// 表示状態を切り替える
    /// </summary>
    public void Toggle()
    {
        if (targetObject == null)
            return;

        if (targetObject.activeSelf)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }
}