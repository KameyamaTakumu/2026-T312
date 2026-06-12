using UnityEngine;

/// <summary>
/// 惑星表面円周移動コンポーネント
///
/// 主な役割：
/// ・GravityAttractor による惑星重力適用
/// ・惑星中心軸まわりの円運動
/// ・地面法線に沿った姿勢維持
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlanetSurfaceWalker : MonoBehaviour
{
    // ─────────────────────────────────────────
    // インスペクター設定
    // ─────────────────────────────────────────

    [Header("移動設定")]

    /// <summary>
    /// 移動速度（m/s）
    /// </summary>
    [CustomLabel("移動速度"), SerializeField]
    private float moveSpeed = 4f;

    /// <summary>
    /// 向き補間速度
    /// 大きいほどキビキビ回転する
    /// </summary>
    [CustomLabel("回転補間速度"), SerializeField]
    private float rotationSpeed = 8f;

    [Header("重力設定")]

    /// <summary>
    /// 重力源となる惑星
    /// 未設定ならシーン内の最寄り GravityAttractor を自動取得
    /// </summary>
    [CustomLabel("重力源の惑星"), SerializeField]
    private GravityAttractor targetPlanet;

    [Header("ダメージゾーン設定")]
    [CustomLabel("前面ダメージゾーン"), SerializeField]
    private PlatformDamageZone frontDamageZone;

    [CustomLabel("後面ダメージゾーン"), SerializeField]
    private PlatformDamageZone backDamageZone;

    [CustomLabel("ダメージゾーンのオフセット距離"), SerializeField]
    private float damageZoneOffset = 0.6f;

    // ─────────────────────────────────────────
    // 内部参照
    // ─────────────────────────────────────────

    /// <summary>
    /// Rigidbody キャッシュ
    /// </summary>
    private Rigidbody _rb;

    /// <summary>
    /// 現在の円運動方向（惑星表面上の接線方向）
    /// </summary>
    private Vector3 _moveDir;

    // 外部から現在の移動速度を参照できるようにする
    public Vector3 CurrentVelocity => _rb.linearVelocity;

    // ─────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Unity 標準重力は使用しない
        _rb.useGravity = false;

        // 回転は本スクリプトで制御
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Start()
    {
        // 惑星未設定なら最寄りを自動取得
        if (targetPlanet == null)
        {
            GravityAttractor[] all =
                FindObjectsByType<GravityAttractor>(
                    FindObjectsSortMode.None);

            float minDist = float.MaxValue;
            foreach (var a in all)
            {
                float d = Vector3.Distance(
                    transform.position,
                    a.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    targetPlanet = a;
                }
            }
        }

        // 初期移動方向を決める
        // 惑星上方向に対して垂直な任意の水平方向を取得する
        InitializeMoveDirection();

        SetupDamageZones();
    }

    private void FixedUpdate()
    {
        if (targetPlanet == null) return;

        // ─────────────────────────────────
        // 惑星重力を適用
        // GravityAttractor.Attract が
        // 重力付加・地面法線への姿勢補正
        // の両方を行ってくれる
        // ─────────────────────────────────
        targetPlanet.Attract(_rb);

        // ─────────────────────────────────
        // 惑星上方向（法線）を取得
        // ─────────────────────────────────
        Vector3 planetUp =
            (transform.position - targetPlanet.transform.position)
            .normalized;

        // ─────────────────────────────────
        // 移動方向を惑星表面に投影
        // 重力適用後に若干ズレるため毎フレーム補正
        // ─────────────────────────────────
        _moveDir =
            Vector3.ProjectOnPlane(_moveDir, planetUp)
            .normalized;

        // ─────────────────────────────────
        // 速度設定
        // 重力方向成分（落下速度）は保持し、
        // 水平速度だけを上書きする
        // ─────────────────────────────────
        Vector3 verticalVelocity =
            Vector3.Project(_rb.linearVelocity, planetUp);

        _rb.linearVelocity =
            _moveDir * moveSpeed + verticalVelocity;

        // ─────────────────────────────────
        // 進行方向へ向きを補間
        // ─────────────────────────────────
        if (_moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot =
                Quaternion.LookRotation(_moveDir, planetUp);

            _rb.MoveRotation(
                Quaternion.Slerp(
                    _rb.rotation,
                    targetRot,
                    rotationSpeed * Time.fixedDeltaTime));
        }

        // ダメージゾーンの位置を進行方向に合わせて更新
        UpdateDamageZonePositions();
    }

    // ─────────────────────────────────────────
    // ダメージゾーン管理
    // ─────────────────────────────────────────

    /// <summary>
    /// 前面・後面のダメージゾーンを初期設定する。
    /// インスペクタで設定済みならそのまま使い、
    /// 未設定なら子オブジェクトを自動生成する。
    /// </summary>
    private void SetupDamageZones()
    {
        if (frontDamageZone == null)
            frontDamageZone = CreateDamageZone("DamageZone_Front");

        if (backDamageZone == null)
            backDamageZone = CreateDamageZone("DamageZone_Back");

        UpdateDamageZonePositions();
    }

    /// <summary>
    /// ダメージゾーン用子オブジェクトを生成する
    /// </summary>
    private PlatformDamageZone CreateDamageZone(string zoneName)
    {
        GameObject obj = new GameObject(zoneName);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        // ダメージ判定用 BoxCollider（IsTrigger=ON）
        BoxCollider col = obj.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1f, 1.5f, 0.3f); // 幅・高さ・奥行き

        PlatformDamageZone zone = obj.AddComponent<PlatformDamageZone>();
        return zone;
    }

    /// <summary>
    /// 毎フレーム、前面・後面ゾーンを進行方向に合わせて配置する
    /// </summary>
    private void UpdateDamageZonePositions()
    {
        if (frontDamageZone != null)
        {
            frontDamageZone.transform.position = transform.position + transform.forward * damageZoneOffset;
            frontDamageZone.transform.rotation = transform.rotation;
        }

        if (backDamageZone != null)
        {
            backDamageZone.transform.position = transform.position - transform.forward * damageZoneOffset;
            backDamageZone.transform.rotation = transform.rotation;
        }
    }

    // ─────────────────────────────────────────
    // 初期移動方向の決定
    // ─────────────────────────────────────────

    /// <summary>
    /// 惑星表面に平行な初期移動方向を設定する。
    /// transform.forward を惑星上方向へ投影し、
    /// ほぼゼロの場合は transform.right を代わりに使う。
    /// </summary>
    private void InitializeMoveDirection()
    {
        if (targetPlanet == null) return;

        Vector3 planetUp =
            (transform.position - targetPlanet.transform.position)
            .normalized;

        // transform.forward を惑星面へ投影
        _moveDir =
            Vector3.ProjectOnPlane(
                transform.forward, planetUp)
            .normalized;

        // 真上を向いていて投影がゼロになる場合のフォールバック
        if (_moveDir.sqrMagnitude < 0.01f)
        {
            _moveDir =
                Vector3.ProjectOnPlane(
                    transform.right, planetUp)
                .normalized;
        }
    }

    // ─────────────────────────────────────────
    // Gizmo
    // ─────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 前面（赤）
        Gizmos.color = new Color(1f, 0.2f, 0f, 0.6f);
        Gizmos.DrawCube(transform.position + transform.forward * damageZoneOffset,
                        new Vector3(1f, 1.5f, 0.3f));

        // 後面（青）
        Gizmos.color = new Color(0f, 0.4f, 1f, 0.6f);
        Gizmos.DrawCube(transform.position - transform.forward * damageZoneOffset,
                        new Vector3(1f, 1.5f, 0.3f));
    }
#endif
}
