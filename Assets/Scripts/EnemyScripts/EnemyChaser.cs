using UnityEngine;

/// <summary>
/// プレイヤー追跡型の敵AI
/// 
/// 主な役割：
/// ・プレイヤー追跡
/// ・初期位置への帰還
/// ・惑星表面移動
/// ・敵向き制御
/// </summary>
[RequireComponent(typeof(GravityBody))]
public class EnemyChaser : EnemyBase
{
    [Header("移動設定")]

    // 追跡速度
    [CustomLabel("追跡速度"), SerializeField]
    private float chaseSpeed = 4f;

    // 巡回速度
    [CustomLabel("待機時の巡回速度"), SerializeField]
    private float patrolSpeed = 1.5f;

    // プレイヤーへ近づく停止距離
    [CustomLabel("プレイヤーとの停止距離"), SerializeField]
    private float stopDistance = 1.2f;

    // 回転速度
    [CustomLabel("向き変更の滑らかさ"), SerializeField]
    private float rotationSpeed = 8f;

    [Header("巡回設定")]

    // プレイヤーを見失ったら戻るか
    [CustomLabel("範囲外離脱後に元の位置へ戻る"), SerializeField]
    private bool returnToOrigin = true;

    // 原点到達判定距離
    [CustomLabel("原点復帰の許容距離"), SerializeField]
    private float originReachDistance = 0.5f;

    // ─────────────────────────────────────────

    // AI状態
    private enum EnemyState
    {
        Patrol,
        Chase
    }

    private EnemyState state =
        EnemyState.Patrol;

    // プレイヤー参照
    private Transform playerTransform;

    // Rigidbody
    private Rigidbody rb;

    // 初期位置
    private Vector3 originPosition;

    // 初期上方向
    // 惑星法線記録用
    private Vector3 originUp;

    // ─────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        rb = GetComponent<Rigidbody>();

        // 初期位置保存
        originPosition = transform.position;

        // 初期法線方向保存
        originUp = transform.up;
    }

    private void Start()
    {
        // プレイヤー検索
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            playerTransform = player.transform;
    }

    private void FixedUpdate()
    {
        if (isDead || playerTransform == null)
            return;

        // プレイヤー距離
        float distToPlayer =
            Vector3.Distance(
                transform.position,
                playerTransform.position
            );

        // ─────────────────────────────────────
        // 状態遷移
        // ─────────────────────────────────────

        if (distToPlayer <= chaseRange)
            state = EnemyState.Chase;
        else
            state = EnemyState.Patrol;

        // ─────────────────────────────────────
        // 状態別処理
        // ─────────────────────────────────────

        switch (state)
        {
            case EnemyState.Chase:
                UpdateChase(distToPlayer);
                break;

            case EnemyState.Patrol:

                // 原点復帰
                if (returnToOrigin)
                    UpdateReturnToOrigin();

                break;
        }
    }

    // ─────────────────────────────────────────
    // 移動処理
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤー追跡
    /// </summary>
    private void UpdateChase(float distToPlayer)
    {
        // 近すぎたら停止
        if (distToPlayer <= stopDistance)
            return;

        // 惑星上方向
        Vector3 planetUp = transform.up;

        // プレイヤー方向
        // 惑星面へ投影して上下成分除去
        Vector3 targetDir =
            Vector3.ProjectOnPlane(
                playerTransform.position
                - transform.position,
                planetUp
            ).normalized;

        MoveInDirection(targetDir, chaseSpeed);
    }

    /// <summary>
    /// 初期位置へ戻る
    /// </summary>
    private void UpdateReturnToOrigin()
    {
        float distToOrigin =
            Vector3.Distance(
                transform.position,
                originPosition
            );

        // 十分近い
        if (distToOrigin <= originReachDistance)
            return;

        Vector3 planetUp = transform.up;

        // 原点方向
        Vector3 targetDir =
            Vector3.ProjectOnPlane(
                originPosition
                - transform.position,
                planetUp
            ).normalized;

        MoveInDirection(targetDir, patrolSpeed);
    }

    /// <summary>
    /// 惑星面上移動
    /// </summary>
    private void MoveInDirection(
        Vector3 direction,
        float speed)
    {
        // 極小ベクトル防止
        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 planetUp = transform.up;

        // ─────────────────────────────────────
        // 回転
        // ─────────────────────────────────────

        Quaternion targetRot =
            Quaternion.LookRotation(
                direction,
                planetUp
            );

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                targetRot,
                rotationSpeed * Time.fixedDeltaTime
            )
        );

        // ─────────────────────────────────────
        // 移動
        // ─────────────────────────────────────

        // 重力方向速度保持
        Vector3 verticalVel =
            Vector3.Project(
                rb.linearVelocity,
                planetUp
            );

        // 水平移動 + 重力方向速度
        rb.linearVelocity =
            direction * speed
            + verticalVel;
    }

    // ─────────────────────────────────────────
    // EnemyBase フック
    // ─────────────────────────────────────────

    protected override void OnDamaged(int amount)
    {
        // プレイヤー方向から押し返す
        if (playerTransform == null)
            return;

        Vector3 knockDir =
            (
                transform.position
                - playerTransform.position
            ).normalized;

        rb.linearVelocity += knockDir * 4f;
    }

    protected override void OnDeath()
    {
        // 死亡時処理
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // 停止距離
        Gizmos.color =
            new Color(1f, 1f, 0f, 0.4f);

        Gizmos.DrawWireSphere(
            transform.position,
            stopDistance
        );
    }
#endif
}