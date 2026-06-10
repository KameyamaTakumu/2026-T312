using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// スピン攻撃で起動できるスイッチ
/// 
/// 主な役割：
/// ・PlayerSpin からスピンヒット通知を受け取る
/// ・スイッチ起動状態を管理する
/// ・UnityEvent を発火する
/// ・SpinSwitchGroup に状態を通知する
/// </summary>
public class SpinSwitch : MonoBehaviour
{
    // ─────────────────────────────────────────
    // インスペクタ設定
    // ─────────────────────────────────────────

    [Header("スイッチ設定")]

    // SwitchGroup 内で識別するためのID
    // デバッグやログ表示にも使用する
    [CustomLabel("スイッチID"), SerializeField]
    private string switchId = "Switch_01";

    // true の場合、一度起動したら再度反応しない
    [CustomLabel("1回だけ起動する"), SerializeField]
    private bool activateOnce = true;

    // 現在起動済みかどうか
    [CustomLabel("起動済みか"), SerializeField]
    private bool isActivated = false;

    [Header("イベント")]

    // スイッチ起動時に呼ばれるイベント
    // インスペクタから自由に処理を登録できる
    [CustomLabel("起動時に呼ぶ処理（単体使用時）")]
    public UnityEvent onActivated;

    [Header("SpinSwitchGroup 連携")]

    // 複数スイッチをまとめて管理するグループ
    // null の場合は単体スイッチとして動作する
    [CustomLabel("所属する SpinSwitchGroup"), SerializeField]
    private SpinSwitchGroup spinSwitchGroup;

    // ─────────────────────────────────────────
    // 内部変数
    // ─────────────────────────────────────────

    // Renderer キャッシュ
    // 毎回 GetComponent を呼ばないように保持しておく
    private Renderer render;

    // 元のマテリアル
    // Reset 時に戻すため保存しておく
    private Material originalMaterial;

    // ─────────────────────────────────────────
    // 公開プロパティ
    // ─────────────────────────────────────────

    /// <summary>
    /// 現在起動済みか
    /// 外部から参照専用
    /// </summary>
    public bool IsActivated => isActivated;

    /// <summary>
    /// スイッチID
    /// Group 側などから参照する
    /// </summary>
    public string SwitchId => switchId;

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    private void Awake()
    {
        // 子オブジェクトも含めて Renderer を取得
        render = GetComponentInChildren<Renderer>();

        // 元マテリアル保存
        if (render != null)
            originalMaterial = render.material;
    }

    private void Start()
    {
        // SpinSwitchGroup に自動登録
        // Group 側で全スイッチ管理できるようにする
        if (spinSwitchGroup != null)
            spinSwitchGroup.RegisterSwitch(this);
    }

    // ─────────────────────────────────────────
    // PlayerSpin から呼ばれるメソッド
    // ─────────────────────────────────────────

    /// <summary>
    /// スピン攻撃が当たったときに呼ばれる
    /// PlayerSpin.cs の PerformSpinAttack() 内で
    /// GetComponent によって取得され通知される
    /// </summary>
    /// <param name="playerTransform">
    /// スピン攻撃を行ったプレイヤーの Transform
    /// </param>
    public void OnSpin(Transform playerTransform)
    {
        // 既に起動済みで、1回だけ起動設定なら無視
        if (isActivated && activateOnce) return;

        Activate();
    }

    // ─────────────────────────────────────────
    // 内部処理
    // ─────────────────────────────────────────

    /// <summary>
    /// スイッチ起動処理
    /// </summary>
    private void Activate()
    {
        // 起動状態へ変更
        isActivated = true;

        // UnityEvent 発火
        // ? 演算子により null チェック付きで安全に呼び出す
        onActivated?.Invoke();

        Debug.Log($"{switchId}が起動しました");

        // Group に通知
        // Group 側で全スイッチ起動を判定する
        if (spinSwitchGroup != null)
            spinSwitchGroup.NotifyActivated(this);
    }

    /// <summary>
    /// スイッチを初期状態へ戻す
    /// 主に SpinSwitchGroup.ResetAll() から呼ばれる
    /// </summary>
    public void ResetSwitch()
    {
        // 未起動状態へ戻す
        isActivated = false;

        // 元のマテリアルへ戻す
        if (render != null && originalMaterial != null)
            render.material = originalMaterial;
    }

    // ─────────────────────────────────────────
    // Gizmo
    // ─────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 起動状態によって色変更
        Gizmos.color = isActivated
            ? new Color(0f, 1f, 0.3f, 0.4f) // 緑 = 起動済み
            : new Color(1f, 0.8f, 0f, 0.4f); // 黄色 = 未起動

        // スイッチ位置を可視化
        Gizmos.DrawSphere(transform.position, 0.5f);
    }
#endif
}