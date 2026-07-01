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

    /// <summary>
    /// 前面・後面ゾーンの配置オフセット（ワールド単位）
    /// 親スケールの影響を受けない絶対距離で指定する
    /// </summary>
    [CustomLabel("ダメージゾーンのオフセット距離"), SerializeField]
    private float damageZoneOffset = 0.6f;

    /// <summary>
    /// ゾーンのワールド空間上のサイズ（幅・高さ・奥行き）
    /// 親スケールに関わらずこのサイズがそのまま判定範囲になる
    /// </summary>
    [CustomLabel("ゾーンサイズ（ワールド単位）"), SerializeField]
    private Vector3 damageZoneWorldSize = new Vector3(1f, 1.5f, 0.3f);

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

        // ダメージゾーンの位置・サイズを更新
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
    /// ダメージゾーン用子オブジェクトを生成する。
    /// </summary>
    private PlatformDamageZone CreateDamageZone(string zoneName)
    {
        GameObject obj = new GameObject(zoneName);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        // BoxCollider サイズと localScale は UpdateDamageZonePositions で設定する
        BoxCollider col = obj.AddComponent<BoxCollider>();
        col.isTrigger = true;

        PlatformDamageZone zone = obj.AddComponent<PlatformDamageZone>();
        return zone;
    }

    /// <summary>
    /// 毎フレーム、前面・後面ゾーンを進行方向に合わせて配置する。
    ///
    /// 【スケール対応の仕組み】
    /// 子は親の子なので、何もしないと親スケールを継承して判定が大きくなる。
    /// これを防ぐため「子の localScale = 1 / 親の lossyScale」として
    /// ワールドスケールを (1,1,1) に打ち消し、
    /// BoxCollider.size に damageZoneWorldSize をそのまま指定することで
    /// 親スケールに関わらず常に同じワールドサイズの判定を維持する。
    ///
    /// オフセットは transform.forward（ワールド方向）に直接掛けるため
    /// 親スケールの影響を受けない。
    /// </summary>
    private void UpdateDamageZonePositions()
    {
        // ─────────────────────────────────
        // 親スケールを打ち消す localScale を計算
        // ゼロ除算防止のため各成分が 0 に近い場合はスキップしない
        // （lossyScale に 0 が入るケースは通常ないが念のため保護）
        // ─────────────────────────────────
        Vector3 ps = transform.lossyScale;
        Vector3 counterScale = new Vector3(
            Mathf.Approximately(ps.x, 0f) ? 1f : 1f / ps.x,
            Mathf.Approximately(ps.y, 0f) ? 1f : 1f / ps.y,
            Mathf.Approximately(ps.z, 0f) ? 1f : 1f / ps.z);

        // ─────────────────────────────────
        // 前面ゾーン：進行方向 (+Z) 側
        // ─────────────────────────────────
        if (frontDamageZone != null)
        {
            frontDamageZone.transform.position =
                transform.position + transform.forward * damageZoneOffset;
            frontDamageZone.transform.rotation = transform.rotation;
            frontDamageZone.transform.localScale = counterScale;

            BoxCollider col = frontDamageZone.GetComponent<BoxCollider>();
            if (col != null)
                col.size = damageZoneWorldSize;
        }

        // ─────────────────────────────────
        // 後面ゾーン：進行方向 (-Z) 側
        // ─────────────────────────────────
        if (backDamageZone != null)
        {
            backDamageZone.transform.position =
                transform.position - transform.forward * damageZoneOffset;
            backDamageZone.transform.rotation = transform.rotation;
            backDamageZone.transform.localScale = counterScale;

            BoxCollider col = backDamageZone.GetComponent<BoxCollider>();
            if (col != null)
                col.size = damageZoneWorldSize;
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
        // ゾーンサイズはワールド単位固定なので Gizmos.matrix でスケール補正する
        Matrix4x4 originalMatrix = Gizmos.matrix;

        // 前面（赤）
        Gizmos.color = new Color(1f, 0.2f, 0f, 0.6f);
        Vector3 frontPos = transform.position + transform.forward * damageZoneOffset;
        Gizmos.matrix = Matrix4x4.TRS(frontPos, transform.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, damageZoneWorldSize);

        // 後面（青）
        Gizmos.color = new Color(0f, 0.4f, 1f, 0.6f);
        Vector3 backPos = transform.position - transform.forward * damageZoneOffset;
        Gizmos.matrix = Matrix4x4.TRS(backPos, transform.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, damageZoneWorldSize);

        Gizmos.matrix = originalMatrix;
    }
#endif
}