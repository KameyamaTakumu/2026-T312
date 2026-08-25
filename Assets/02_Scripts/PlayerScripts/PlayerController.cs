using UnityEngine;

/// <summary>
/// プレイヤーの移動・ジャンプ・カメラ追従を管理するコンポーネント。
///
/// 主な役割：
/// ・Wキーによる前進、Sキーによる後退
/// ・ジャンプ（引力ジャンプ／通常ジャンプ）
/// ・惑星表面（球体重力）に沿った移動方向の制御
/// ・A/Dキーによるカメラの水平旋回
/// ・接地判定、移動床（PlanetSurfaceWalker）への追従
/// ・惑星ごとのカメラ見下ろし角度の切り替え（GravityAttractor側の設定を参照）
///
/// 移動方向はプレイヤー自身の入力キーではなく、カメラの向き（_camForward）を基準に決定する。
/// これによりプレイヤーの正面は常にカメラの正面と一致する。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GravityBody))]
public class PlayerController : MonoBehaviour
{
    [Header("移動設定")]

    [CustomLabel("移動速度"), SerializeField]
    private float moveSpeed = 8f;

    [CustomLabel("加速度（地上）"), SerializeField]
    private float acceleration = 60f;

    [CustomLabel("減速度（地上・入力なし）"), SerializeField]
    private float deceleration = 40f;

    [CustomLabel("空中加速度"), SerializeField]
    private float airAcceleration = 20f;

    [CustomLabel("ジャンプ力"), SerializeField]
    private float jumpForce = 14f;

    // 小さすぎると接地判定が不安定になる
    [CustomLabel("接地判定の距離"), SerializeField]
    private float groundCheckDistance = 0.1f;

    [CustomLabel("地面接地レイヤー"), SerializeField]
    private LayerMask groundLayer = ~0;

    [Header("カメラ設定")]

    // 未設定時は Camera.main を自動取得する
    [CustomLabel("追従カメラ"), SerializeField]
    private Transform cameraTransform;

    [CustomLabel("プレイヤー回転速度"), SerializeField]
    private float rotationSpeed = 15f;

    [CustomLabel("カメラ左右旋回速度（度/秒）"), SerializeField]
    private float cameraTurnSpeed = 120f;

    // 0で水平方向を向く。正の値でプレイヤー側（下方向）へ傾く。
    // 惑星側で見下ろし角度の上書き設定がない場合の基準角度。
    [CustomLabel("カメラ見下ろし角度"), SerializeField]
    private float cameraPitch = 20f;

    // GravityAttractor.OverrideCameraPitch の切り替え時、
    // この速度で現在角度から目標角度へ補間する
    [CustomLabel("惑星切り替え時のカメラ角度変化速度（度/秒）"), SerializeField]
    private float pitchTransitionSpeed = 60f;

    [CustomLabel("カメラの高さオフセット"), SerializeField]
    private float cameraHeightOffset = 4f;

    // マイナス値でプレイヤーの後方に配置される
    [CustomLabel("カメラの後方オフセット"), SerializeField]
    private float cameraDepthOffset = -8f;

    // ─────────────────────────────────────
    // 内部参照
    // ─────────────────────────────────────

    private Rigidbody rb;
    private CapsuleCollider cap;
    private GravityBody grabody;
    private PlayerSpin spin;

    // ─────────────────────────────────────
    // 状態
    // ─────────────────────────────────────

    private bool isGrounded;

    // Input.GetButtonDown は1フレームしか true にならないため、
    // Update で検知した入力を FixedUpdate まで保持する
    private bool jumpRequested;

    // 現在乗っている移動床
    private PlanetSurfaceWalker currentPlatform;

    // 直前フレームの床水平速度。床から降りた瞬間に余分な速度を取り除くために使用
    private Vector3 prevPlatformVelocity = Vector3.zero;

    // カメラの水平方向（惑星接線平面に投影・正規化済み）。
    // A/D入力でこの値を回転させ、プレイヤーの向き・移動方向の基準にする
    private Vector3 camForward;

    // 現在のカメラ見下ろし角度。cameraPitch と惑星側の上書き値の間を
    // pitchTransitionSpeed で滑らかに補間した現在値
    private float currentCameraPitch;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cap = GetComponent<CapsuleCollider>();
        grabody = GetComponent<GravityBody>();
        spin = GetComponent<PlayerSpin>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        InitCamForward();
        currentCameraPitch = cameraPitch;
    }

    private void Start()
    {
        if (RespawnManager.Instance != null && RespawnManager.Instance.HasRespawnPoint)
        {
            rb.position = RespawnManager.Instance.RespawnPosition;
            rb.rotation = RespawnManager.Instance.RespawnRotation;

            // ワープ時と同様、リスポーン直後は重力方向がずれるため同期させる
            grabody.ForceSyncGravity();
        }
    }

    private void Update()
    {
        // Update は毎フレーム呼ばれるため、単発入力の取得に用いる
        if (Input.GetButtonDown("Jump"))
            jumpRequested = true;
    }

    private void FixedUpdate()
    {
        PlanetSurfaceWalker prevPlatform = currentPlatform;

        isGrounded = CheckGrounded();
        grabody.IsGrounded = isGrounded;

        // 床から降りた瞬間、前フレームの床速度分を自分の速度から取り除く
        if (prevPlatform != null && currentPlatform == null)
        {
            Vector3 planetUp = transform.up;
            Vector3 velocity = rb.linearVelocity;
            Vector3 verticalVelocity = Vector3.Project(velocity, planetUp);
            Vector3 horizontalVelocity = velocity - verticalVelocity;

            horizontalVelocity -= prevPlatformVelocity;
            rb.linearVelocity = horizontalVelocity + verticalVelocity;
        }

        HandleCameraRotation();
        UpdateCameraPitchTarget();
        HandleMove();
        HandleJump();

        // 次フレームの比較用に、今フレームの床の水平速度を記憶する
        prevPlatformVelocity = currentPlatform != null
            ? currentPlatform.CurrentVelocity - Vector3.Project(currentPlatform.CurrentVelocity, transform.up)
            : Vector3.zero;

        // スピン演出中はカメラを固定するため更新しない
        if (spin == null || !spin.IsSpinning)
            UpdateCamera();
    }

    /// <summary>
    /// camForward を惑星上方向に対して垂直な単位ベクトルとして初期化する。
    /// </summary>
    private void InitCamForward()
    {
        Vector3 planetUp = transform.up;
        Vector3 baseDirection = cameraTransform != null ? cameraTransform.forward : transform.forward;

        Vector3 projected = Vector3.ProjectOnPlane(baseDirection, planetUp);

        // カメラ前方向と惑星上方向がほぼ平行な場合のフォールバック
        if (projected.sqrMagnitude < 0.0001f)
            projected = Vector3.ProjectOnPlane(transform.forward, planetUp);
        if (projected.sqrMagnitude < 0.0001f)
            projected = Vector3.ProjectOnPlane(Vector3.forward, planetUp);

        camForward = projected.normalized;
    }

    /// <summary>
    /// 現在乗っている惑星（GravityBody.CurrentAttractor）にカメラ角度の上書き設定が
    /// あればその角度へ、なければ通常の cameraPitch へ、pitchTransitionSpeed で近づける。
    /// </summary>
    private void UpdateCameraPitchTarget()
    {
        float targetPitch = cameraPitch;

        GravityAttractor currentPlanet = grabody.CurrentAttractor;
        if (currentPlanet != null && currentPlanet.OverrideCameraPitch)
            targetPitch = currentPlanet.CameraPitchOverride;

        currentCameraPitch = Mathf.MoveTowards(
            currentCameraPitch,
            targetPitch,
            pitchTransitionSpeed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// A/D入力によるカメラの水平旋回。camForward を惑星上方向を軸に回転させる。
    /// </summary>
    private void HandleCameraRotation()
    {
        float turn = Input.GetAxisRaw("Horizontal");

        if (GameSettingsManager.Instance != null && GameSettingsManager.Instance.InvertCamera)
            turn *= -1f;

        if (Mathf.Approximately(turn, 0f))
            return;

        Vector3 planetUp = rb.rotation * Vector3.up;
        float angle = turn * cameraTurnSpeed * Time.fixedDeltaTime;
        Quaternion rotation = Quaternion.AngleAxis(angle, planetUp);

        camForward = (rotation * camForward).normalized;
    }

    /// <summary>
    /// プレイヤーの移動処理。移動方向はカメラの向き（camForward）を基準に決定する。
    /// </summary>
    private void HandleMove()
    {
        bool moveInputForward = Input.GetKey(KeyCode.W);
        bool moveInputBackward = Input.GetKey(KeyCode.S);

        Vector3 planetUp = rb.rotation * Vector3.up;

        // 惑星をまたいだ移動でも camForward が接線平面上に留まるよう補正する
        Vector3 camForwardOnPlane = Vector3.ProjectOnPlane(camForward, planetUp).normalized;
        if (camForwardOnPlane.sqrMagnitude > 0.0001f)
            camForward = camForwardOnPlane;

        // 移動床の水平速度。目標速度の基準として使用する
        Vector3 platformVelocity = Vector3.zero;
        if (currentPlatform != null)
        {
            Vector3 velocity = currentPlatform.CurrentVelocity;
            platformVelocity = velocity - Vector3.Project(velocity, planetUp);
        }

        // 入力なし：水平速度を減速する
        if (!moveInputForward && !moveInputBackward)
        {
            Vector3 velocity = rb.linearVelocity;
            Vector3 verticalVelocity = Vector3.Project(velocity, planetUp);
            Vector3 horizontalVelocity = velocity - verticalVelocity;

            // 地上のみ減速（空中は慣性を維持する）。
            // 床がある場合は床速度へ、ない場合はゼロへ向かって減速する
            if (isGrounded)
            {
                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    platformVelocity,
                    deceleration * Time.fixedDeltaTime);
            }

            rb.linearVelocity = horizontalVelocity + verticalVelocity;
            return;
        }

        if (isGrounded)
            TutorialManager.Instance?.NotifyRunning(Time.fixedDeltaTime);

        Vector3 moveDirection = moveInputForward ? camForward : -camForward;

        // プレイヤーの前方向を移動方向（＝カメラの前方向）へ滑らかに合わせる
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, planetUp);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed));

        // 重力方向速度を分離し、ジャンプ・落下による垂直速度を保持する
        Vector3 verticalVel = Vector3.Project(rb.linearVelocity, planetUp);
        Vector3 currentHorizontal = rb.linearVelocity - verticalVel;

        // 目標水平速度＝床速度＋プレイヤー入力速度。
        // 床の上ではプレイヤーの入力が床基準の相対移動になる
        Vector3 targetHorizontal = platformVelocity + moveDirection * moveSpeed;

        float accel = isGrounded ? acceleration : airAcceleration;
        Vector3 newHorizontal = Vector3.MoveTowards(currentHorizontal, targetHorizontal, accel * Time.fixedDeltaTime);

        rb.linearVelocity = newHorizontal + verticalVel;
    }

    /// <summary>
    /// ジャンプ処理。GravityJumpZone内では引力ジャンプを優先する。
    /// </summary>
    private void HandleJump()
    {
        if (!jumpRequested)
            return;
        jumpRequested = false;

        // GravityJumpZone 内であれば引力ジャンプを試みる。成立した場合は通常ジャンプを行わない
        if (grabody.TryGravityJump())
            return;

        if (isGrounded)
        {
            rb.AddForce(rb.rotation * Vector3.up * jumpForce, ForceMode.Impulse);
            SE.Jump.Play();
            TutorialManager.Instance?.NotifyJump();
        }
    }

    /// <summary>
    /// SphereCastによる接地判定。曲面地形に対して通常のRaycastより安定する。
    /// </summary>
    private bool CheckGrounded()
    {
        Vector3 up = rb.rotation * Vector3.up;
        Vector3 center = rb.position + rb.rotation * cap.center;

        float radius = cap.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
        float halfHeight = Mathf.Max(cap.height * 0.5f - radius, 0f);
        Vector3 origin = center - up * halfHeight;

        bool grounded = Physics.SphereCast(
            origin,
            radius * 0.95f,
            -up,
            out RaycastHit hit,
            groundCheckDistance,
            groundLayer);

        currentPlatform = grounded ? hit.collider.GetComponent<PlanetSurfaceWalker>() : null;

        return grounded;
    }

    /// <summary>
    /// カメラ追従処理。
    ///
    /// 位置・回転ともに Lerp/Slerp による遅延補間を行わず、camForward から毎フレーム
    /// 直接計算する。これにより「位置は遅れて追従、回転は現在位置基準」というズレに
    /// よる違和感を防いでいる。
    ///
    /// 見下ろし角度のみ、UpdateCameraPitchTarget() で補間された currentCameraPitch を
    /// 用いることで、惑星ごとの角度切り替えを滑らかに反映する。
    /// </summary>
    private void UpdateCamera()
    {
        if (cameraTransform == null)
            return;

        Vector3 planetUp = rb.rotation * Vector3.up;

        cameraTransform.position =
            transform.position
            + planetUp * cameraHeightOffset
            + camForward * cameraDepthOffset;

        // 水平方向（camForward）を向く回転を基準に、見下ろし角度を別途加算する。
        // こうすることで camForward の変化に関わらず、見下ろし具合を独立して調整できる
        Quaternion baseRotation = Quaternion.LookRotation(camForward, planetUp);
        Quaternion pitchRotation = Quaternion.AngleAxis(currentCameraPitch, baseRotation * Vector3.right);

        cameraTransform.rotation = pitchRotation * baseRotation;
    }
}