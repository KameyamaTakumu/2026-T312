using UnityEngine;
using System.Collections;

/// <summary>
/// プレイヤーが所持コインを発射した時の挙動を管理する
/// 
/// 主な役割：
/// ・直進移動
/// ・衝突判定
/// ・敵ヒット処理
/// ・地面着地後のコイン生成
/// </summary>
public class LaunchedCoin : MonoBehaviour
{
    [Header("飛行設定")]

    // この距離を超えたら自動消滅
    [CustomLabel("最大飛行距離"), SerializeField]
    private float maxDistance = 40f;

    // 自転速度
    [CustomLabel("自転速度（deg/s）"), SerializeField]
    private float spinSpeed = 720f;

    // Raycast に使用するレイヤー
    // 自分自身のレイヤーを除外推奨
    [CustomLabel("衝突チェック用レイヤー（自分自身を除外すること）"), SerializeField]
    private LayerMask hitLayer = ~0;

    [Header("着地後の挙動")]

    // 地面に当たった後、通常コインとして残すか
    [CustomLabel("着地後にコインとして残す"), SerializeField]
    private bool spawnCoinOnHit = true;

    // 地面に残るコインPrefab
    [CustomLabel("残すコイン Prefab（未設定=消滅のみ）"), SerializeField]
    private GameObject coinPrefab;

    // 強制消滅までの時間
    [CustomLabel("生存時間（秒）"), SerializeField]
    private float lifetime = 5f;

    // 発射方向（normalized 済み）
    private Vector3 direction;

    // 移動速度
    private float speed;

    // 発射開始位置
    private Vector3 startPos;

    // 発射済みか
    private bool fired = false;

    // 衝突済みか
    // 二重衝突防止用
    private bool hit = false;

    // ─────────────────────────────────────────
    // 公開 API
    // ─────────────────────────────────────────

    /// <summary>
    /// 発射開始
    /// StarPointer から呼ばれる
    /// </summary>
    public void Fire(Vector3 direction, float speed)
    {
        // 方向を正規化して保存
        this.direction = direction.normalized;

        this.speed = speed;

        // 飛距離計算用
        startPos = transform.position;

        fired = true;

        // 発射方向へ向きを合わせる
        if (this.direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(this.direction);

        // 一定時間後に自動消滅
        StartCoroutine(LifetimeCoroutine());
    }

    private void Update()
    {
        if (!fired || hit)
            return;

        // 見た目用の自転
        // forward 軸回転なのでコインが回転して飛んでいるように見える
        transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);

        // 最大飛距離を超えたら消滅処理
        if (Vector3.Distance(startPos, transform.position) >= maxDistance)
            HandleHit(transform.position, -direction, null);
    }

    private void FixedUpdate()
    {
        if (!fired || hit)
            return;

        // この FixedUpdate で進む距離
        float stepDist = speed * Time.fixedDeltaTime;

        // 高速移動時のすり抜け対策として Raycast を使用
        // stepDist + 0.1f は少し余裕を持たせるため
        if (Physics.Raycast(
                transform.position,
                direction,
                out RaycastHit hitInfo,
                stepDist + 0.1f,
                hitLayer,
                QueryTriggerInteraction.Ignore))
        {
            HandleHit(hitInfo.point, hitInfo.normal, hitInfo.collider);
            return;
        }

        // 実際の移動
        transform.position += direction * stepDist;
    }

    /// <summary>
    /// Trigger Collider による衝突検知
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!fired || hit)
            return;

        // 発射コイン同士の接触は無視
        if (other.GetComponent<LaunchedCoin>() != null)
            return;

        HandleHit(transform.position, -direction, other);
    }

    /// <summary>
    /// 衝突共通処理
    /// </summary>
    private void HandleHit(Vector3 point, Vector3 normal, Collider other)
    {
        // 二重実行防止
        if (hit)
            return;

        hit = true;
        fired = false;

        // ───────── 敵ヒット ─────────

        if (other != null && other.CompareTag("Enemy"))
        {
            OnHitEnemy(other, point);
        }

        // ───────── 地形ヒット ─────────
        // 通常コインを生成して残す

        else if (spawnCoinOnHit && coinPrefab != null)
        {
            // 地面法線方向
            Vector3 up = normal.normalized;

            // 地面に沿った forward ベクトルを作る
            Vector3 forward = Vector3.ProjectOnPlane(direction, up);

            // 真上・真下に近い場合の補正
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(Vector3.up, up);

            Instantiate(
                coinPrefab,

                // 少し浮かせて埋まり防止
                point + up * 0.3f,

                Quaternion.LookRotation(forward.normalized, up)
            );
        }

        // 発射コイン削除
        Destroy(gameObject);
    }

    /// <summary>
    /// 敵ヒット時の拡張ポイント
    /// 継承先で override 可能
    /// </summary>
    protected virtual void OnHitEnemy(Collider enemy, Vector3 hitPoint)
    {
        // EnemyBase 経由でコイン専用ダメージを通知
        // 敵側の「コイン発射でダメージを受ける」設定が OFF の場合は無効になる
        EnemyBase enemyBase = enemy.GetComponent<EnemyBase>()
                           ?? enemy.GetComponentInParent<EnemyBase>();
        if (enemyBase != null)
        {
            enemyBase.TakeDamageFromCoin(1);

            //// コインのダメージで実際に倒した場合のみチュートリアル通知
            //if (enemyBase.IsDead)
            //    TutorialManager.Instance?.NotifyCoinHitEnemy();
        }
        else
            Debug.Log($"[LaunchedCoin] 敵に命中（EnemyBase なし）: {enemy.name}");
    }

    /// <summary>
    /// 一定時間後に自動消滅させる Coroutine
    /// </summary>
    private IEnumerator LifetimeCoroutine()
    {
        yield return new WaitForSeconds(lifetime);

        // まだ衝突していなければ強制終了
        if (!hit)
            HandleHit(transform.position, Vector3.up, null);
    }
}