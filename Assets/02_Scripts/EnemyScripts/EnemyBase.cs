using System.Collections;
using UnityEngine;

/// <summary>
/// 敵共通ベースクラス
/// 
/// 主な役割：
/// ・HP管理
/// ・ダメージ処理
/// ・死亡処理
/// ・プレイヤー接触判定
/// ・踏みつけ判定
/// ・無敵時間管理
/// 
/// EnemyChaser などの敵クラスは
/// このクラスを継承して使用する
/// </summary>
public abstract class EnemyBase : MonoBehaviour
{
    // ─────────────────────────────────────────
    // インスペクタ設定
    // ─────────────────────────────────────────

    [Header("HP設定")]

    // 最大HP
    [CustomLabel("最大HP"), SerializeField]
    protected int maxHp = 3;

    [Header("被ダメージ設定")]

    // コイン攻撃でダメージを受けるか
    [CustomLabel("コイン発射でダメージを受ける"), SerializeField]
    private bool vulnerableToCoin = true;

    // 踏みつけダメージを受けるか
    [CustomLabel("踏みつけでダメージを受ける"), SerializeField]
    private bool vulnerableToStomp = true;

    [Header("踏みつけ設定")]

    // プレイヤーバウンド力
    [CustomLabel("踏みつけ後のプレイヤーバウンド力"), SerializeField]
    private float stompBounceForce = 10f;

    [Header("プレイヤーへのダメージ設定")]

    // 接触ダメージ有効
    [CustomLabel("接触でプレイヤーにダメージを与える"), SerializeField]
    private bool damagesPlayerOnContact = true;

    // 踏まれた時でもプレイヤーへダメージを与えるか
    [CustomLabel("踏みつけ時もプレイヤーにダメージを与える"), SerializeField]
    private bool damagesPlayerOnStomp = false;

    // プレイヤーへ与えるダメージ量
    [CustomLabel("プレイヤーに与えるダメージ量"), SerializeField]
    private int damageToPlayer = 1;

    [Header("追跡設定")]

    // プレイヤー検知範囲
    [CustomLabel("追跡範囲の半径"), SerializeField]
    protected float chaseRange = 10f;

    // Sceneビューで範囲表示
    [CustomLabel("追跡範囲を可視化"), SerializeField]
    private bool showGizmo = true;

    [Header("徘徊設定")]

    // 未検知時にランダム徘徊するか
    // falseの場合は継承先の実装次第（その場で静止するなど）
    [CustomLabel("未検知時にランダム徘徊するか"), SerializeField]
    protected bool enableWander = true;

    // 徘徊できる原点（初期位置）からの半径
    [CustomLabel("徘徊可能な原点からの半径"), SerializeField]
    protected float wanderRadius = 3f;

    // 徘徊目標を選び直す間隔（秒）
    // 目標地点に到達した場合はこの時間を待たずに更新する
    [CustomLabel("徘徊目標の更新間隔（秒）"), SerializeField]
    protected float wanderInterval = 2.5f;

    // 徘徊目標への到達とみなす許容距離
    [CustomLabel("徘徊目標への到達許容距離"), SerializeField]
    protected float wanderArriveDistance = 0.3f;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    // 現在HP
    protected int currentHp;

    // 死亡済みか
    protected bool isDead = false;

    // ダメージ直後の無敵状態
    // 多段ヒット防止用
    private bool invincible = false;

    // 踏みつけ処理中フラグ
    // OnCollisionEnter が同フレームで横接触と誤判定しないようにする
    private bool isBeingStomped = false;

    [CustomLabel("被ダメージ後の無敵時間（秒）"), SerializeField]
    private float invincibleDuration = 0.5f;

    // 初期位置（徘徊範囲の中心として使用）
    // サブクラスから参照できるよう protected にしてある
    protected Vector3 originPosition;

    // 現在の徘徊目標地点（ワールド座標）
    private Vector3 wanderTarget;

    // 徘徊目標を選んでからの経過時間
    private float wanderTimer;

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    protected virtual void Awake()
    {
        currentHp = maxHp;

        // 徘徊・原点復帰の基準点として、スポーン時の位置を記録する
        originPosition = transform.position;
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (isDead) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        // 踏みつけ処理中は OnCollisionEnter を無視する
        // （StompTrigger と本体 Collider が同時に反応するため）
        if (isBeingStomped) return;

        PlayerHealth playerHealth =
            collision.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        // 横接触ダメージのみここで処理する
        // 踏みつけ判定は StompTrigger.cs が担当する
        HandleSideContact(playerHealth);
    }

    /// <summary>
    /// StompTrigger から呼ばれる踏みつけ処理
    /// </summary>
    public void OnStomped(Rigidbody playerRb, PlayerHealth playerHealth)
    {
        if (isDead) return;

        // OnCollisionEnter の横接触判定を一時的に無効化する
        isBeingStomped = true;

        // 踏みつけ処理
        HandleStomp(playerHealth, playerRb);

        // 同フレーム内で OnCollisionEnter が呼ばれた後にリセットするため
        // 次フレーム先頭まで待つ
        StartCoroutine(ResetStompFlag());
    }

    /// <summary>
    /// 踏みつけ処理フラグリセットコルーチン
    /// </summary>
    /// <returns></returns>
    private IEnumerator ResetStompFlag()
    {
        yield return null; // 1フレーム待つ
        isBeingStomped = false;
    }

    // ─────────────────────────────────────────
    // 接触処理
    // ─────────────────────────────────────────

    /// <summary>
    /// 上から踏まれた時
    /// </summary>
    private void HandleStomp(
        PlayerHealth playerHealth,
        Rigidbody playerRb)
    {
        // 敵へダメージ
        if (vulnerableToStomp)
            TakeDamage(1);

        // プレイヤーにもダメージ
        if (damagesPlayerOnStomp)
            playerHealth.TakeDamage(damageToPlayer);

        // プレイヤーバウンド
        if (playerRb != null
            && stompBounceForce > 0f)
        {
            // 惑星法線方向
            Vector3 bounceDir =
                playerRb.transform.up;

            Vector3 vel =
                playerRb.linearVelocity;

            // 水平成分抽出
            Vector3 horizontal =
                vel - Vector3.Project(vel, bounceDir);

            // 上方向へバウンド速度付与
            playerRb.linearVelocity =
                horizontal
                + bounceDir * stompBounceForce;
        }
    }

    /// <summary>
    /// 横接触
    /// </summary>
    private void HandleSideContact(
        PlayerHealth playerHealth)
    {
        if (!damagesPlayerOnContact)
            return;

        playerHealth.TakeDamage(damageToPlayer);
    }

    // ─────────────────────────────────────────
    // ダメージ受付
    // ─────────────────────────────────────────

    /// <summary>
    /// コイン攻撃ダメージ
    /// </summary>
    public void TakeDamageFromCoin(int amount)
    {
        if (!vulnerableToCoin)
            return;

        TakeDamage(amount);
    }

    /// <summary>
    /// 汎用ダメージ処理
    /// </summary>
    public virtual void TakeDamage(int amount)
    {
        // 無敵中・死亡中は無効
        if (isDead || invincible)
            return;

        currentHp -= amount;

        // 継承先フック
        OnDamaged(amount);

        // 死亡判定
        if (currentHp <= 0)
            Die();
        else
            StartCoroutine(InvincibleCoroutine());
    }

    /// <summary>
    /// 即死処理
    /// </summary>
    public void InstantKill()
    {
        if (isDead)
            return;

        currentHp = 0;

        Die();
    }

    // ─────────────────────────────────────────
    // 内部処理
    // ─────────────────────────────────────────

    /// <summary>
    /// 死亡処理
    /// </summary>
    private void Die()
    {
        isDead = true;

        // 継承先演出
        OnDeath();

        // 少し待って削除
        // 演出表示時間確保用
        Destroy(gameObject, 0.1f);
    }

    /// <summary>
    /// ダメージ無敵時間
    /// </summary>
    private IEnumerator InvincibleCoroutine()
    {
        invincible = true;

        yield return new WaitForSeconds(invincibleDuration);

        invincible = false;
    }

    // ─────────────────────────────────────────
    // 徘徊
    // ─────────────────────────────────────────

    /// <summary>
    /// 徘徊目標へ向かう方向（惑星面に沿った水平方向、正規化済み）を返す。
    ///
    /// 呼び出し側（サブクラス）は、未検知状態のときにこれを呼び、
    /// 得られた方向を自前の移動処理（MoveInDirection等）へ渡すだけでよい。
    /// タイマー管理・目標地点の再抽選はこのメソッド内で自動的に行う。
    ///
    /// 目標とほぼ同じ位置にいる場合は Vector3.zero を返すので、
    /// 呼び出し側は sqrMagnitude が十分小さければ移動をスキップしてよい。
    /// </summary>
    protected Vector3 GetWanderDirection()
    {
        wanderTimer += Time.fixedDeltaTime;

        float distToTarget =
            Vector3.Distance(transform.position, wanderTarget);

        // 目標へ到達済み、または更新間隔が来たら次の目標を選ぶ
        if (distToTarget <= wanderArriveDistance
            || wanderTimer >= wanderInterval)
        {
            PickNewWanderTarget();
            wanderTimer = 0f;
        }

        Vector3 planetUp = transform.up;

        // 現在地から徘徊目標への方向を、惑星面に沿って計算
        return Vector3.ProjectOnPlane(
            wanderTarget - transform.position,
            planetUp
        ).normalized;
    }

    /// <summary>
    /// 原点を中心とした円内からランダムに徘徊目標を選ぶ
    /// （惑星の接平面上に投影して求める）
    /// </summary>
    private void PickNewWanderTarget()
    {
        Vector3 planetUp = transform.up;

        // 接平面（惑星表面）上の基準軸（tangent）を1つ作るための仮の参照ベクトル。
        // planetUpとほぼ平行（内積が0.9超）な場合、ProjectOnPlaneの結果が
        // ほぼゼロベクトルになり不安定になるため、その場合は別軸にフォールバックする
        Vector3 reference = Vector3.forward;
        if (Mathf.Abs(Vector3.Dot(reference, planetUp)) > 0.9f)
            reference = Vector3.right;

        // referenceを接平面に投影して正規化したものを軸1（tangent）とする
        Vector3 tangent =
            Vector3.ProjectOnPlane(reference, planetUp).normalized;
        // planetUpとtangentの外積で、接平面上で直交するもう1つの軸（bitangent）を得る
        Vector3 bitangent =
            Vector3.Cross(planetUp, tangent).normalized;

        // 半径wanderRadius以内のランダムな2次元座標を取得
        Vector2 rand = Random.insideUnitCircle * wanderRadius;

        // tangent・bitangentの2軸を使い、接平面上のランダムな点をワールド座標に変換
        wanderTarget =
            originPosition
            + tangent * rand.x
            + bitangent * rand.y;
    }

    // ─────────────────────────────────────────
    // 継承先フック
    // ─────────────────────────────────────────

    /// <summary>
    /// ダメージ直後
    /// 
    /// ノックバック・SE・点滅など
    /// </summary>
    protected virtual void OnDamaged(int amount) { }

    /// <summary>
    /// 死亡直後
    /// 
    /// 撃破演出・ドロップなど
    /// </summary>
    protected virtual void OnDeath() { }

    // ─────────────────────────────────────────
    // Gizmo
    // ─────────────────────────────────────────

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        if (!showGizmo)
            return;

        // 追跡範囲
        Gizmos.color =
            new Color(1f, 0.2f, 0.2f, 0.15f);

        Gizmos.DrawSphere(
            transform.position,
            chaseRange
        );

        Gizmos.color =
            new Color(1f, 0.2f, 0.2f, 0.7f);

        Gizmos.DrawWireSphere(
            transform.position,
            chaseRange
        );
    }
#endif
}