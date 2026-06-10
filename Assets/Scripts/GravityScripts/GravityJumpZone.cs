using UnityEngine;

/// <summary>
/// 引力ジャンプゾーン
/// プレイヤーがこのゾーン内で引力ジャンプを行うと、
/// ゾーン中心へ向かって引き寄せられる
///
/// 主な役割：
/// ・プレイヤーの侵入検知
/// ・引力ジャンプ先の管理
/// ・到着判定
/// ・着地後に固定する惑星の指定
/// </summary>
public class GravityJumpZone : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 引力設定
    // ─────────────────────────────────────────

    [Header("引力設定")]

    /// <summary>
    /// ゾーン中心へ引き寄せる力
    /// 値が大きいほど加速が強くなる
    /// </summary>
    [CustomLabel("引き寄せ力"), SerializeField]
    private float attractForce = 20f;

    /// <summary>
    /// 引き寄せ中の最高速度
    /// 速度が上がりすぎるのを防ぐ
    /// </summary>
    [CustomLabel("引き寄せ最大速度"), SerializeField]
    private float maxAttractSpeed = 15f;

    /// <summary>
    /// ゾーン中心へ到達したとみなす距離
    /// </summary>
    [CustomLabel("到着判定距離"), SerializeField]
    private float arrivalDistance = 1.5f;

    // ─────────────────────────────────────────
    // 着地後設定
    // ─────────────────────────────────────────

    [Header("着地後の設定")]

    /// <summary>
    /// 到着後に惑星を固定する時間
    /// この間は最寄り惑星の再判定を行わないため、
    /// 着地直後の誤判定を防げる
    /// </summary>
    [CustomLabel("到着後の惑星固定時間（秒）"),
     Tooltip("この間は引力が無効になり通常移動できる"),
     SerializeField]
    private float groundedLockDuration = 2.0f;

    /// <summary>
    /// 到着後に固定する惑星
    /// null の場合は最寄り惑星を自動選択する
    /// </summary>
    [CustomLabel("このゾーンが属する惑星（GravityAttractor）"), SerializeField]
    private GravityAttractor targetPlanet;

    // ─────────────────────────────────────────
    // Gizmo設定
    // ─────────────────────────────────────────

    [Header("演出")]

    /// <summary>
    /// Sceneビューで表示する色
    /// </summary>
    [CustomLabel("引力ゾーンの可視化色"), SerializeField]
    private Color gizmoColor = new Color(0.4f, 0.8f, 1f, 0.2f);

    // ─────────────────────────────────────────
    // 公開プロパティ
    // ─────────────────────────────────────────

    public float AttractForce => attractForce;
    public float MaxAttractSpeed => maxAttractSpeed;
    public float ArrivalDistance => arrivalDistance;
    public float GroundedLockDuration => groundedLockDuration;

    /// <summary>
    /// 到着後に固定する惑星
    /// null の場合は最寄り惑星が使用される
    /// </summary>
    public GravityAttractor TargetPlanet => targetPlanet;

    // ─────────────────────────────────────────
    // ゾーン侵入検知
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤーがゾーンに入った
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // プレイヤーから GravityBody を取得
        GravityBody gb = other.GetComponent<GravityBody>()
                      ?? other.GetComponentInParent<GravityBody>();

        // GravityBody に通知
        if (gb != null)
            gb.OnEnterGravityJumpZone(this);
    }

    /// <summary>
    /// プレイヤーがゾーンから出た
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        GravityBody gb = other.GetComponent<GravityBody>()
                      ?? other.GetComponentInParent<GravityBody>();

        if (gb != null)
            gb.OnExitGravityJumpZone(this);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Sceneビューで引力ゾーンを可視化
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        SphereCollider col = GetComponent<SphereCollider>();

        // コライダー半径を取得
        float r = col != null ? col.radius : 5f;

        // ゾーン範囲
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, r);

        Gizmos.color =
            new Color(
                gizmoColor.r,
                gizmoColor.g,
                gizmoColor.b,
                0.9f);

        Gizmos.DrawWireSphere(transform.position, r);

        // 到着判定範囲
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawWireSphere(
            transform.position,
            arrivalDistance);
    }
#endif
}