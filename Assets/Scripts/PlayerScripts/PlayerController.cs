using UnityEngine;

/// <summary>
/// プレイヤー操作 + カメラ追従コンポーネント
/// 
/// 主な役割：
/// ・プレイヤー移動
/// ・ジャンプ
/// ・惑星表面に沿った移動方向制御
/// ・カメラ追従
/// ・接地判定
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

    // プレイヤー回転速度
    [CustomLabel("カメラ回転速度"), SerializeField]
    private float rotationSpeed = 15f;

    // カメラ追従の滑らかさ
    [CustomLabel("カメラ追従の滑らかさ"), SerializeField]
    private float cameraSmooth = 5f;

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

    void Awake()
    {
        // 毎フレーム GetComponent しないようキャッシュ
        rb = GetComponent<Rigidbody>();
        cap = GetComponent<CapsuleCollider>();
        grabody = GetComponent<GravityBody>();
        spin = GetComponent<PlayerSpin>();

        // カメラ未設定時は MainCamera を使用
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
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
        // 接地判定
        isGrounded = CheckGrounded();

        // 移動処理
        HandleMove();

        // ジャンプ処理
        HandleJump();

        // カメラ更新
        // スピン中はカメラを固定
        if (spin == null || !spin.IsSpinning)
        {
            UpdateCamera();
        }
    }

    /// <summary>
    /// プレイヤー移動処理
    /// </summary>
    void HandleMove()
    {
        // 横入力（A,D / ←→）
        float h = Input.GetAxisRaw("Horizontal");

        // 縦入力（W,S / ↑↓）
        float v = Input.GetAxisRaw("Vertical");

        // 入力なし → 水平速度を減速して return
        if (Mathf.Approximately(h, 0f)
            && Mathf.Approximately(v, 0f))
        {
            Vector3 up = transform.up;
            Vector3 vel = rb.linearVelocity;
            Vector3 vVel = Vector3.Project(vel, up);
            Vector3 hVel = vel - vVel;

            // 地上のみ減速（空中は慣性を維持）
            if (isGrounded)
            {
                hVel = Vector3.MoveTowards(
                    hVel,
                    Vector3.zero,
                    deceleration * Time.fixedDeltaTime
                );
            }

            rb.linearVelocity = hVel + vVel;
            return;
        }

        // 地上で入力あり → 走り中としてチュートリアルへ通知
        if (isGrounded)
            TutorialManager.Instance?.NotifyRunning(Time.fixedDeltaTime);

        // ─────────────────────────────────
        // 惑星上方向
        // ─────────────────────────────────

        // GravityBody により transform.up が
        // 惑星法線方向を向いている
        Vector3 planetUp = transform.up;

        // ─────────────────────────────────
        // カメラ基準移動方向
        // ─────────────────────────────────

        // カメラ前方向を惑星表面へ投影
        // 上下成分を除去して地面に沿わせる
        Vector3 camForward =
            Vector3.ProjectOnPlane(
                cameraTransform.forward,
                planetUp
            ).normalized;

        // カメラ右方向も同様
        Vector3 camRight =
            Vector3.ProjectOnPlane(
                cameraTransform.right,
                planetUp
            ).normalized;

        // 入力方向合成
        Vector3 moveDir =
            (camForward * v + camRight * h)
            .normalized;

        // ─────────────────────────────────
        // プレイヤー回転
        // ─────────────────────────────────

        if (moveDir != Vector3.zero)
        {
            // 移動方向へ向ける
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

        // 目標水平速度
        Vector3 targetHorizontal = moveDir * moveSpeed;

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
            rb.AddForce(
                transform.up * jumpForce,
                ForceMode.Impulse
            );

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
        Vector3 center =
            transform.TransformPoint(cap.center);

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
        Vector3 origin =
            center - transform.up * height;

        // ─────────────────────────────────
        // SphereCast 接地判定
        // ─────────────────────────────────

        // 少し上から下方向へ SphereCast
        // 通常 Raycast より曲面地形に強い
        return Physics.SphereCast(
            origin,
            radius * 0.95f,

            // 惑星下方向
            -transform.up,

            out _,

            // 接地判定距離
            groundCheckDistance,

            // 地面レイヤー
            groundLayer
        );
    }

    /// <summary>
    /// カメラ追従処理
    /// </summary>
    void UpdateCamera()
    {
        // カメラ未設定
        if (cameraTransform == null)
            return;

        // 惑星法線方向
        Vector3 planetUp = transform.up;

        // ─────────────────────────────────
        // 目標位置計算
        // ─────────────────────────────────

        // プレイヤー後方上空
        Vector3 targetPosition =
            transform.position
            + planetUp * cameraHeightOffset
            + transform.forward * cameraDepthOffset;

        // 滑らかに移動
        cameraTransform.position =
            Vector3.Lerp(
                cameraTransform.position,
                targetPosition,
                Time.fixedDeltaTime * cameraSmooth
            );

        // ─────────────────────────────────
        // カメラ回転
        // ─────────────────────────────────

        // プレイヤー方向を見る
        Quaternion targetRot =
            Quaternion.LookRotation(
                transform.position
                - cameraTransform.position,

                // 惑星上方向
                planetUp
            );

        // 滑らか回転
        cameraTransform.rotation =
            Quaternion.Slerp(
                cameraTransform.rotation,
                targetRot,
                Time.fixedDeltaTime * cameraSmooth
            );
    }
}