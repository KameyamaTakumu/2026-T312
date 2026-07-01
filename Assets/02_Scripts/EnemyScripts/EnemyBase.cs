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

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    protected virtual void Awake()
    {
        currentHp = maxHp;
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
    public void TakeDamage(int amount)
    {
        // 無敵中・死亡中は無効
        if (isDead || invincible)
            return;

        currentHp -= amount;

        Debug.Log($"{gameObject.name} Current HP: {currentHp}");

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