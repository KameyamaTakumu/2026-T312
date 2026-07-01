using System.Collections;
using UnityEngine;

/// <summary>
/// マリオギャラクシー風 追跡＋突進ボス
///
/// 主な役割：
/// ・プレイヤーを追跡する
/// ・一定間隔で予備動作 → 突進を行う
/// ・突進中に岩（RockTag）へ衝突するとダメージを受ける
/// ・突進失敗後は硬直する
/// </summary>
public class BossEnemyChaser : EnemyBase
{
    // ─────────────────────────────────────────
    // ステート定義
    // ─────────────────────────────────────────

    private enum BossState
    {
        Chasing,     // 追跡中
        Telegraph,   // 突進予備動作
        Charging,    // 突進中
        Recovering   // 突進後の硬直
    }

    // ─────────────────────────────────────────
    // Inspector 設定
    // ─────────────────────────────────────────

    [Header("追跡設定")]

    [CustomLabel("追跡速度"), SerializeField]
    private float chaseSpeed = 4f;

    [CustomLabel("追跡時の旋回速度"), SerializeField]
    private float turnSpeed = 4f;

    [CustomLabel("プレイヤーとのこれ以上近づかない距離"), SerializeField]
    private float minChaseDistance = 2.5f;

    [Header("突進設定")]

    [CustomLabel("突進間隔（秒）"), SerializeField]
    private float chargeInterval = 4f;

    [CustomLabel("突進予備動作の時間（秒）"), SerializeField]
    private float telegraphDuration = 0.8f;

    [CustomLabel("突進速度"), SerializeField]
    private float chargeSpeed = 14f;

    [CustomLabel("突進の最大持続時間（秒）"), SerializeField]
    private float chargeMaxDuration = 2f;

    [CustomLabel("突進が外れた時の硬直時間（秒）"), SerializeField]
    private float recoverDuration = 1.2f;

    [CustomLabel("岩に激突した時の硬直時間（秒）"), SerializeField]
    private float rockHitStunDuration = 1.5f;

    [Header("岩判定")]

    [CustomLabel("突進中にダメージを受ける岩のタグ"), SerializeField]
    private string rockTag = "Rock";

    [Header("参照")]

    [CustomLabel("プレイヤーのTransform（未設定なら自動検索）"), SerializeField]
    private Transform playerTransform;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    private Rigidbody _rb;
    private BossState _state = BossState.Chasing;

    private float _chargeTimer = 0f;      // 次の突進までのタイマー
    private float _stateTimer = 0f;       // 各ステート内での経過時間
    private Vector3 _chargeDirection;     // 突進開始時に確定する方向

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody>();

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        if (playerTransform == null) return;

        switch (_state)
        {
            case BossState.Chasing:
                UpdateChasing();
                break;
            case BossState.Telegraph:
                UpdateTelegraph();
                break;
            case BossState.Charging:
                UpdateCharging();
                break;
            case BossState.Recovering:
                UpdateRecovering();
                break;
        }
    }

    // ─────────────────────────────────────────
    // 追跡ステート
    // ─────────────────────────────────────────

    private void UpdateChasing()
    {
        Vector3 up = transform.up;

        // プレイヤー方向を惑星表面の接平面に投影する
        Vector3 toPlayer = playerTransform.position - transform.position;
        Vector3 flatToPlayer = Vector3.ProjectOnPlane(toPlayer, up);
        float distance = flatToPlayer.magnitude;

        if (distance > minChaseDistance)
        {
            Vector3 moveDir = flatToPlayer.normalized;

            // 進行方向へ向きを合わせる
            Quaternion targetRot = Quaternion.LookRotation(moveDir, up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));

            // 前方へ移動（重力方向の速度は保持する）
            Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, up);
            _rb.linearVelocity = moveDir * chaseSpeed + verticalVel;
        }
        else
        {
            // 近すぎる時は水平方向の速度だけ止める
            Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, up);
            _rb.linearVelocity = verticalVel;
        }

        // 突進間隔タイマー
        _chargeTimer += Time.fixedDeltaTime;

        if (_chargeTimer >= chargeInterval && distance <= chaseRange)
        {
            _chargeTimer = 0f;
            EnterTelegraph();
        }
    }

    // ─────────────────────────────────────────
    // 突進予備動作ステート
    // ─────────────────────────────────────────

    private void EnterTelegraph()
    {
        _state = BossState.Telegraph;
        _stateTimer = 0f;

        // 水平方向の動きを止める（重力方向はそのまま）
        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, transform.up);
        _rb.linearVelocity = verticalVel;
    }

    private void UpdateTelegraph()
    {
        _stateTimer += Time.fixedDeltaTime;

        if (_stateTimer >= telegraphDuration)
        {
            // 突進方向をここで確定する（開始後は追尾しない）
            Vector3 toPlayer = playerTransform.position - transform.position;
            Vector3 flatToPlayer = Vector3.ProjectOnPlane(toPlayer, transform.up);

            _chargeDirection = flatToPlayer.sqrMagnitude > 0.01f
                ? flatToPlayer.normalized
                : transform.forward;

            EnterCharging();
        }
    }

    // ─────────────────────────────────────────
    // 突進ステート
    // ─────────────────────────────────────────

    private void EnterCharging()
    {
        _state = BossState.Charging;
        _stateTimer = 0f;

        Vector3 up = transform.up;
        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, up);
        _rb.linearVelocity = _chargeDirection * chargeSpeed + verticalVel;

        Quaternion targetRot = Quaternion.LookRotation(_chargeDirection, up);
        _rb.MoveRotation(targetRot);
    }

    private void UpdateCharging()
    {
        _stateTimer += Time.fixedDeltaTime;

        Vector3 up = transform.up;

        // 突進方向を現在の重力面（接平面）に再投影して曲面に沿わせる
        Vector3 flatDir = Vector3.ProjectOnPlane(_chargeDirection, up);
        if (flatDir.sqrMagnitude > 0.0001f)
        {
            _chargeDirection = flatDir.normalized;
        }

        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, up);
        _rb.linearVelocity = _chargeDirection * chargeSpeed + verticalVel;

        // 姿勢も毎フレーム up に合わせて更新する
        Quaternion targetRot = Quaternion.LookRotation(_chargeDirection, up);
        _rb.MoveRotation(targetRot);

        if (_stateTimer >= chargeMaxDuration)
        {
            EnterRecovering(recoverDuration);
        }
    }

    // ─────────────────────────────────────────
    // 硬直ステート
    // ─────────────────────────────────────────

    private void EnterRecovering(float duration)
    {
        _state = BossState.Recovering;
        _stateTimer = 0f;

        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, transform.up);
        _rb.linearVelocity = verticalVel;

        StartCoroutine(RecoverCoroutine(duration));
    }

    private IEnumerator RecoverCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (!isDead)
        {
            _state = BossState.Chasing;
            _stateTimer = 0f;
        }
    }

    private void UpdateRecovering()
    {
        // 硬直中は静止（時間経過は RecoverCoroutine が管理）
        Vector3 verticalVel = Vector3.Project(_rb.linearVelocity, transform.up);
        _rb.linearVelocity = verticalVel;
    }

    // ─────────────────────────────────────────
    // 衝突処理（岩への激突判定）
    // ─────────────────────────────────────────

    protected override void OnCollisionEnter(Collision collision)
    {
        // プレイヤー接触処理は EnemyBase 側に任せる
        base.OnCollisionEnter(collision);

        if (isDead) return;
        if (_state != BossState.Charging) return;

        if (collision.gameObject.CompareTag(rockTag))
        {
            // 岩に激突 → ダメージ
            TakeDamage(1);

            EnterRecovering(rockHitStunDuration);
        }
    }
}