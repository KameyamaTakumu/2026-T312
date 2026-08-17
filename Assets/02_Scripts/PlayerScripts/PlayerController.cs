using UnityEngine;

/// <summary>
/// プレイヤー操作 + カメラ追従コンポーネント
/// 
/// 主な役割：
/// ・プレイヤー移動（Wキーのみで前進）
/// ・ジャンプ
/// ・惑星表面に沿った移動方向制御
/// ・カメラ追従（A/Dキーでカメラを左右に旋回）
/// ・接地判定
/// ・惑星ごとのカメラ見下ろし角度の切り替え（GravityAttractorの設定を参照）
///
/// 仕様変更点：
/// ・移動入力はWキーのみ（前進のみ、後退・左右移動なし）
/// ・A/Dキーはプレイヤー移動ではなく、カメラの水平旋回に使用する
/// ・プレイヤーの前方向は常にカメラの前方向と一致する
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GravityBody))]
public class PlayerController : MonoBehaviour
{
    [Header("移動設定")]

    // 水平方向の移動速度
    [CustomLabel("移動速度"), SerializeField]
    private float moveSpeed = 8f;

    // 地上での加速度
    [CustomLabel("加速度（地上）"), SerializeField]
    private float acceleration = 60f;

    // 地上での減速度（入力なしのとき）
    [CustomLabel("減速度（地上・入力なし）"), SerializeField]
    private float deceleration = 40f;

    // 空中での加速度
    [CustomLabel("空中加速度"), SerializeField]
    private float airAcceleration = 20f;

    // ジャンプ時に加える力
    [CustomLabel("ジャンプ力"), SerializeField]
    private float jumpForce = 14f;

    // 接地判定を行う距離
    // 小さすぎると接地判定が不安定になる
    [CustomLabel("接地判定の距離"), SerializeField]
    private float groundCheckDistance = 0.1f;

    // 接地対象レイヤー
    // 地面判定したい Layer を設定する
    [CustomLabel("地面接地レイヤー"), SerializeField]
    private LayerMask groundLayer = ~0;

    [Header("カメラ設定")]

    // プレイヤー追従カメラ
    // 未設定なら MainCamera を自動取得
    [CustomLabel("追従カメラ"), SerializeField]
    private Transform cameraTransform;

    // プレイヤー回転速度（プレイヤーがカメラ方向へ向く速さ）
    [CustomLabel("プレイヤー回転速度"), SerializeField]
    private float rotationSpeed = 15f;

    // A/D 入力によるカメラ旋回速度（度/秒）
    [CustomLabel("カメラ左右旋回速度"), SerializeField]
    private float cameraTurnSpeed = 120f;

    // カメラの見下ろし角度（度）
    // 0で水平方向を向く。正の値で下方向（プレイヤー側）へ傾く
    // これが通常時（惑星側で上書き指定していない時）の基準角度になる
    [CustomLabel("カメラ見下ろし角度"), SerializeField]
    private float cameraPitch = 20f;

    // 惑星ごとのカメラ角度上書き（GravityAttractor.OverrideCameraPitch）が
    // 切り替わった時、この速度で現在角度から目標角度へなめらかに変化する
    [CustomLabel("惑星切り替え時のカメラ角度変化速度（度/秒）"), SerializeField]
    private float pitchTransitionSpeed = 60f;

    // プレイヤー上方向へのカメラオフセット
    [CustomLabel("カメラの高さオフセット"), SerializeField]
    private float cameraHeightOffset = 4f;

    // プレイヤー後方へのカメラオフセット
    // マイナス値にすることで後ろへ下がる
    [CustomLabel("カメラの後方オフセット"), SerializeField]
    private float cameraDepthOffset = -8f;

    // ─────────────────────────────────────
    // 内部参照
    // ─────────────────────────────────────

    // Rigidbody キャッシュ
    Rigidbody rb;

    // CapsuleCollider キャッシュ
    CapsuleCollider cap;

    // 重力制御コンポーネント
    GravityBody grabody;

    // スピン制御参照
    PlayerSpin spin;

    // ─────────────────────────────────────
    // 状態管理
    // ─────────────────────────────────────

    // 現在接地しているか
    bool isGrounded;

    // Update → FixedUpdate 間のジャンプ入力保持
    // Input.GetButtonDown は1フレームしか true にならないため、
    // FixedUpdate で入力を取りこぼさないようにする
    bool jumpRequested;

    // 現在乗っている移動床
    PlanetSurfaceWalker _currentPlatform;

    // 前フレームの床水平速度を記憶
    // 床から降りた瞬間に余分な速度を取り除くために使用
    Vector3 _prevPlatformVelocity = Vector3.zero;

    // カメラの水平方向（惑星接線平面に投影済み、正規化済み）
    // A/D 入力でこの値を回転させ、プレイヤーの向き・移動方向の基準にする
    Vector3 _camForward;

    // 現在実際に使っているカメラ見下ろし角度
    // 惑星切り替え時、cameraPitch ⇔ 各惑星のCameraPitchOverride の間を
    // pitchTransitionSpeed でなめらかに変化させるための現在値
    float _currentCameraPitch;

    void Awake()
    {
        // 毎フレーム GetComponent しないようキャッシュ
        rb = GetComponent<Rigidbody>();
        cap = GetComponent<CapsuleCollider>();
        grabody = GetComponent<GravityBody>();
        spin = GetComponent<PlayerSpin>();

        // カメラ未設定時は MainCamera を使用
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // カメラ水平方向の初期化
        InitCamForward();

        // カメラ角度の初期値は通常時の角度からスタート
        _currentCameraPitch = cameraPitch;
    }

    void Start()
    {
        if (RespawnManager.Instance != null && RespawnManager.Instance.HasRespawnPoint)
        {
            var rb = GetComponent<Rigidbody>();
            rb.position = RespawnManager.Instance.RespawnPosition;
            rb.rotation = RespawnManager.Instance.RespawnRotation;

            // 重力方向のズレ解消（ワープ時と同じ対処）
            GetComponent<GravityBody>()?.ForceSyncGravity();
        }
    }

    /// <summary>
    /// _camForward の初期化・立て直し
    /// 惑星上方向に対して垂直な単位ベクトルを保証する
    /// </summary>
    void InitCamForward()
    {
        Vector3 planetUp = transform.up;

        Vector3 baseDir = (cameraTransform != null)
            ? cameraTransform.forward
            : transform.forward;

        Vector3 projected = Vector3.ProjectOnPlane(baseDir, planetUp);

        // カメラ前方向と惑星上方向がほぼ平行な場合のフォールバック
        if (projected.sqrMagnitude < 0.0001f)
            projected = Vector3.ProjectOnPlane(transform.forward, planetUp);

        if (projected.sqrMagnitude < 0.0001f)
            projected = Vector3.ProjectOnPlane(Vector3.forward, planetUp);

        _camForward = projected.normalized;
    }

    void Update()
    {
        // ジャンプ入力検知
        // Update は毎フレーム呼ばれるため入力取得に向いている
        if (Input.GetButtonDown("Jump"))
            jumpRequested = true;
    }

    void FixedUpdate()
    {
        // 前フレームの床を記憶
        PlanetSurfaceWalker prevPlatform = _currentPlatform;

        // 接地判定
        isGrounded = CheckGrounded();

        // gravitybody に接地状態を通知
        grabody.IsGrounded = isGrounded;

        // 床から降りた瞬間：前フレームの床速度を速度から取り除く
        if (prevPlatform != null && _currentPlatform == null)
        {
            Vector3 planetUp = transform.up;
            Vector3 vel = rb.linearVelocity;
            Vector3 vVel = Vector3.Project(vel, planetUp);
            Vector3 hVel = vel - vVel;

            // 前フレームの床速度分を引いて「自分の速度だけ」に戻す
            hVel -= _prevPlatformVelocity;
            rb.linearVelocity = hVel + vVel;
        }

        // A/D によるカメラ旋回（惑星接線平面上で回転）
        HandleCameraRotation();

        // 現在の惑星に応じてカメラ見下ろし角度をなめらかに切り替える
        UpdateCameraPitchTarget();

        // 移動処理（Wキーのみ）
        HandleMove();

        // ジャンプ処理
        HandleJump();

        // 今フレームの床速度を次フレームのために記憶
        if (_currentPlatform != null)
        {
            Vector3 pv = _currentPlatform.CurrentVelocity;
            _prevPlatformVelocity =
                pv - Vector3.Project(pv, transform.up);
        }
        else
        {
            _prevPlatformVelocity = Vector3.zero;
        }

        // カメラ更新
        // スピン中はカメラを固定
        if (spin == null || !spin.IsSpinning)
            UpdateCamera();
    }

    /// <summary>
    /// 現在乗っている惑星（GravityBody.CurrentAttractor）に
    /// カメラ角度の上書き設定があればその角度へ、
    /// なければ通常の cameraPitch へ、pitchTransitionSpeed でなめらかに近づける。
    ///
    /// ボスがいる第3惑星などで overrideCameraPitch を有効にしておくと、
    /// その惑星に入った瞬間からカメラが自動的に切り替わる。
    /// </summary>
    void UpdateCameraPitchTarget()
    {
        float targetPitch = cameraPitch;

        GravityAttractor currentPlanet = grabody != null ? grabody.CurrentAttractor : null;
        if (currentPlanet != null && currentPlanet.OverrideCameraPitch)
        {
            targetPitch = currentPlanet.CameraPitchOverride;
        }

        _currentCameraPitch = Mathf.MoveTowards(
            _currentCameraPitch,
            targetPitch,
            pitchTransitionSpeed * Time.fixedDeltaTime
        );
    }

    /// <summary>
    /// カメラの水平旋回（A/D入力）
    /// _camForward を惑星上方向を軸に回転させる
    /// </summary>
    void HandleCameraRotation()
    {
        // A(-1) / D(+1)
        float turn = Input.GetAxisRaw("Horizontal");

        if (GameSettingsManager.Instance != null &&
            GameSettingsManager.Instance.InvertCamera)
        {
            turn *= -1f;
        }

        if (Mathf.Approximately(turn, 0f))
            return;

        //Vector3 planetUp = transform.up;
        Vector3 planetUp = rb.rotation * Vector3.up;

        // D で右旋回、A で左旋回になるよう符号をマイナスにしている
        // （見た目が逆の場合は符号を反転させる）
        float angle = turn * cameraTurnSpeed * Time.fixedDeltaTime;

        Quaternion rot = Quaternion.AngleAxis(angle, planetUp);

        _camForward = (rot * _camForward).normalized;
    }

    /// <summary>
    /// プレイヤー移動処理
    /// </summary>
    void HandleMove()
    {
        // 前進入力
        bool moveInputW = Input.GetKey(KeyCode.W);
        bool moveInputS = Input.GetKey(KeyCode.S);

        // ─────────────────────────────────
        // 惑星上方向
        // ─────────────────────────────────

        // GravityBody により transform.up が
        // 惑星法線方向を向いている
        //Vector3 planetUp = transform.up;
        Vector3 planetUp = rb.rotation * Vector3.up;

        // _camForward は惑星が変わっても常に接線平面上にあるよう補正
        Vector3 camForwardOnPlane =
            Vector3.ProjectOnPlane(_camForward, planetUp).normalized;

        if (camForwardOnPlane.sqrMagnitude > 0.0001f)
            _camForward = camForwardOnPlane;

        // ─────────────────────────────────
        // 移動床の水平速度を取得
        // 目標速度の基準として使用する（毎フレーム加算はしない）
        // ─────────────────────────────────
        Vector3 platformVelocity = Vector3.zero;
        if (_currentPlatform != null)
        {
            Vector3 pv = _currentPlatform.CurrentVelocity;
            // 重力方向成分は除外（プレイヤー自身の重力で処理するため）
            platformVelocity = pv - Vector3.Project(pv, planetUp);
        }

        // 入力なし（Wが押されていない）→ 水平速度を減速して return
        if (!moveInputW && !moveInputS)
        {
            Vector3 vel = rb.linearVelocity;
            Vector3 vVel = Vector3.Project(vel, planetUp);
            Vector3 hVel = vel - vVel;

            // 地上のみ減速（空中は慣性を維持）
            // 床がある場合は「床速度へ向かって減速」する
            // 床がない場合はゼロへ向かって減速
            if (isGrounded)
            {
                hVel = Vector3.MoveTowards(
                    hVel,
                    platformVelocity,
                    deceleration * Time.fixedDeltaTime);
            }

            rb.linearVelocity = hVel + vVel;
            return;
        }

        // 地上で入力あり → 走り中としてチュートリアルへ通知
        //if (isGrounded)
        //    TutorialManager.Instance?.NotifyRunning(Time.fixedDeltaTime);
        if (isGrounded && (moveInputW || moveInputS))
            TutorialManager.Instance?.NotifyRunning(Time.fixedDeltaTime);

        // ─────────────────────────────────
        // 移動方向 = カメラ前方向（Wキーのみなので常に前進）
        // ─────────────────────────────────
        //Vector3 moveDir = _camForward;
        Vector3 moveDir = Vector3.zero;

        if (moveInputW)
        {
            moveDir = _camForward;
        }
        else if (moveInputS)
        {
            moveDir = -_camForward;
        }

        // ─────────────────────────────────
        // プレイヤー回転
        // プレイヤーの前方向は常にカメラの前方向と一致させる
        // ─────────────────────────────────

        if (moveDir != Vector3.zero)
        {
            Quaternion targetRot =
                Quaternion.LookRotation(
                    moveDir,
                    planetUp
                );

            // 滑らか回転
            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRot,
                    Time.fixedDeltaTime * rotationSpeed
                )
            );
        }

        // ─────────────────────────────────
        // 速度設定（加速度ベース）
        // ─────────────────────────────────

        // 重力方向速度を分離（ジャンプ・落下を保持するため）
        Vector3 verticalVelocity =
            Vector3.Project(rb.linearVelocity, planetUp);

        // 現在の水平速度
        Vector3 currentHorizontal =
            rb.linearVelocity - verticalVelocity;

        // 目標水平速度 = 床速度 + プレイヤー入力速度
        // 床の上ではプレイヤーの入力が床を基準とした相対移動になる
        Vector3 targetHorizontal = platformVelocity + moveDir * moveSpeed;

        // 地上・空中で加速度を切り替え
        float accel = isGrounded ? acceleration : airAcceleration;

        // 現在速度から目標速度へ加速度で近づける
        // MoveTowards により最大速度を超えない
        Vector3 newHorizontal = Vector3.MoveTowards(
            currentHorizontal,
            targetHorizontal,
            accel * Time.fixedDeltaTime
        );

        rb.linearVelocity = newHorizontal + verticalVelocity;
    }

    /// <summary>
    /// ジャンプ処理
    /// </summary>
    void HandleJump()
    {
        if (!jumpRequested) return;
        jumpRequested = false;

        // ─────────────────────────────────
        // 引力ジャンプ
        // ─────────────────────────────────

        // GravityJumpZone 内にいるときは引力ジャンプを試みる
        // TryGravityJump が true を返したら通常ジャンプはしない
        if (grabody.TryGravityJump()) return;

        // ─────────────────────────────────
        // 通常ジャンプ
        // ─────────────────────────────────

        if (isGrounded)
        {
            rb.AddForce((rb.rotation * Vector3.up) * jumpForce, ForceMode.Impulse);

            SE.Jump.Play();

            // チュートリアルへ通知
            TutorialManager.Instance?.NotifyJump();
        }
    }

    /// <summary>
    /// 接地判定
    /// </summary>
    bool CheckGrounded()
    {
        // ─────────────────────────────────
        // カプセル情報取得
        // ─────────────────────────────────

        // Collider中心位置
        //Vector3 center =
        //    transform.TransformPoint(cap.center);
        Vector3 up = rb.rotation * Vector3.up;
        Vector3 center = rb.position + rb.rotation * cap.center;

        // 半径
        // Scale を考慮
        float radius =
            cap.radius
            * Mathf.Max(
                transform.localScale.x,
                transform.localScale.z
            );

        // カプセル下半分高さ
        float height =
            Mathf.Max(
                cap.height * 0.5f - radius,
                0f
            );

        // 足元位置
        //Vector3 origin =
        //    center - transform.up * height;
        Vector3 origin = center - up * height;

        // ─────────────────────────────────
        // SphereCast 接地判定
        // ─────────────────────────────────

        // 少し上から下方向へ SphereCast
        // 通常 Raycast より曲面地形に強い
        bool grounded = Physics.SphereCast(
            origin,
            radius * 0.95f,

            // 惑星下方向
            //-transform.up,
            -up,

            out RaycastHit hit,

            // 接地判定距離
            groundCheckDistance,

            // 地面レイヤー
            groundLayer
        );

        // 乗っている床を更新
        _currentPlatform = grounded
            ? hit.collider.GetComponent<PlanetSurfaceWalker>()
            : null;

        return grounded;
    }

    /// <summary>
    /// カメラ追従処理
    /// カメラは _camForward（A/Dで旋回する水平方向）を基準に配置する
    ///
    /// 慣性なし仕様：
    /// 位置・回転ともに Lerp/Slerp による遅延を行わず、
    /// _camForward から直接（同じ情報源から）計算することで
    /// 「位置は遅れて追従、回転は現在位置基準」というズレによる
    /// 加速・回転して見える違和感を無くす。
    ///
    /// 見下ろし角度（ピッチ）だけは cameraPitch を直接使わず、
    /// UpdateCameraPitchTarget() でなめらかに補間された
    /// _currentCameraPitch を使用する（惑星ごとの角度切り替え用）。
    /// </summary>
    void UpdateCamera()
    {
        // カメラ未設定
        if (cameraTransform == null)
            return;

        // 惑星法線方向
        //Vector3 planetUp = transform.up;
        Vector3 planetUp = rb.rotation * Vector3.up;

        // ─────────────────────────────────
        // 位置：_camForward を基準に毎フレーム直接配置（Lerpなし）
        // ─────────────────────────────────
        cameraTransform.position =
            transform.position
            + planetUp * cameraHeightOffset
            + _camForward * cameraDepthOffset;

        // ─────────────────────────────────
        // 回転：位置の差分ではなく _camForward から直接計算（Slerpなし）
        // まず水平方向（_camForward）を向く回転を作り、
        // そこに見下ろし角度（_currentCameraPitch）を別途加える。
        // 角度をここで独立して制御できるので、_camForward の変化に
        // 左右されず、Inspector上の値だけで見下ろし具合を調整できる。
        // ─────────────────────────────────
        Quaternion baseRot =
            Quaternion.LookRotation(_camForward, planetUp);

        // カメラ自身の右方向を軸にピッチ回転
        // （正の値で下向きになる。逆になる場合は符号を反転してください）
        Quaternion pitchRot =
            Quaternion.AngleAxis(_currentCameraPitch, baseRot * Vector3.right);

        cameraTransform.rotation = pitchRot * baseRot;
    }
}