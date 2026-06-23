using System.Collections;
using UnityEngine;

/// <summary>
/// スピン攻撃で破壊できるオブジェクト
///
/// 主な役割：
/// ・スピン攻撃の受付
/// ・ヒット数の管理
/// ・破壊演出の実行
/// ・ドロップアイテムの生成
/// ・一定時間後の復活処理
/// ・被弾時の揺れ演出
/// </summary>
public class SpinBreakable : MonoBehaviour
{
    // ─────────────────────────────────────────
    // インスペクタ設定
    // ─────────────────────────────────────────

    [Header("破壊設定")]

    // 破壊に必要なヒット数
    [CustomLabel("破壊に必要なヒット数"), SerializeField]
    private int hitsToBreak = 1;

    // 破壊後に自動で復活するか
    [CustomLabel("復活する"), SerializeField]
    private bool respawns = false;

    // 復活までの待機時間
    [CustomLabel("復活時間（秒）"), SerializeField]
    private float respawnTime = 5f;

    [Header("ドロップ設定")]

    // 生成するドロップPrefab（未設定なら生成しない）
    [CustomLabel("ドロップPrefab"), SerializeField]
    private GameObject dropPrefab;

    // 生成するドロップ数
    [CustomLabel("ドロップ数"), SerializeField]
    private int dropCount = 1;

    // Coin 以外のドロップに適用する飛散力
    [CustomLabel("ドロップ飛散力"), SerializeField]
    private float dropForce = 4f;

    [Header("演出設定")]

    // 破壊時の縮小アニメーション時間
    [CustomLabel("破壊演出時間（秒）"), SerializeField]
    private float breakAnimDuration = 0.2f;

    // ヒット時の揺れ幅
    [CustomLabel("ヒット時の揺れ量"), SerializeField]
    private float hitShakeAmount = 0.1f;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    // 現在のヒット数
    private int currentHits = 0;

    // 破壊済みフラグ
    private bool broken = false;

    // 復活時に戻すための初期スケール
    private Vector3 initialScale;

    // 揺れ演出後に戻すための初期ローカル座標
    private Vector3 initialLocalPos;

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    void Awake()
    {
        // 初期状態を保存
        initialScale = transform.localScale;
        initialLocalPos = transform.localPosition;
    }

    // ─────────────────────────────────────────
    // 公開API
    // ─────────────────────────────────────────

    /// <summary>
    /// スピン攻撃を受けた際の処理
    /// </summary>
    /// <param name="attacker">攻撃したオブジェクトの Transform</param>
    public void OnSpin(Transform attacker)
    {
        // 破壊済みなら無視
        if (broken) return;

        currentHits++;

        // 必要ヒット数に達したら破壊
        if (currentHits >= hitsToBreak)
        {
            Break(attacker);
        }
        else
        {
            // 途中段階では揺れ演出のみ再生
            StopAllCoroutines();
            StartCoroutine(HitShakeCoroutine());
        }
    }

    // ─────────────────────────────────────────
    // 内部処理
    // ─────────────────────────────────────────

    /// <summary>
    /// 破壊処理
    /// </summary>
    private void Break(Transform attacker)
    {
        broken = true;

        // ドロップ生成後、破壊演出を開始
        SpawnDrops(attacker);
        StartCoroutine(BreakAnimCoroutine());
    }

    /// <summary>
    /// ドロップアイテムを生成する
    ///
    /// Coin コンポーネントを持つ場合は
    /// Coin 側の初期化処理に移動方向を渡し、
    /// それ以外は Rigidbody に直接力を加える。
    /// </summary>
    private void SpawnDrops(Transform attacker)
    {
        if (dropPrefab == null || dropCount <= 0)
            return;

        // 惑星の法線方向を上方向として利用
        Vector3 up = transform.up;

        for (int i = 0; i < dropCount; i++)
        {
            // 少し浮かせた位置に生成
            Vector3 spawnPos =
                transform.position + up * 0.5f;

            GameObject drop =
                Instantiate(
                    dropPrefab,
                    spawnPos,
                    Quaternion.identity
                );

            // Coin の場合は専用初期化を行う
            Coin coin = drop.GetComponent<Coin>();
            if (coin != null)
            {
                Vector3 randomH =
                    Vector3.ProjectOnPlane(
                        Random.insideUnitSphere,
                        up
                    ).normalized;

                // 上方向をベースにランダム性を加えた跳ね方向
                Vector3 bounceDir =
                    (up + randomH * 0.5f).normalized;

                coin.InitAsDropCoin(bounceDir);

                // 力の適用は Coin 側で行う
                continue;
            }

            // Coin 以外は Rigidbody に直接飛散力を与える
            Rigidbody dropRb =
                drop.GetComponent<Rigidbody>();

            if (dropRb != null)
            {
                Vector3 randomH =
                    Vector3.ProjectOnPlane(
                        Random.insideUnitSphere,
                        up
                    ).normalized;

                dropRb.useGravity = false;

                dropRb.AddForce(
                    (up + randomH * 0.5f).normalized
                    * dropForce,
                    ForceMode.Impulse
                );
            }
        }
    }

    /// <summary>
    /// 縮小しながら破壊演出を行う
    /// </summary>
    private IEnumerator BreakAnimCoroutine()
    {
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < breakAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / breakAnimDuration;

            transform.localScale =
                Vector3.Lerp(startScale, Vector3.zero, t);

            yield return null;
        }

        transform.localScale = Vector3.zero;

        if (respawns)
        {
            // 非表示状態で待機し、その後復活
            SetVisible(false);

            yield return new WaitForSeconds(respawnTime);

            Respawn();
        }
        else
        {
            // 復活しない場合は削除
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ヒット時に左右へ小さく揺らす演出
    /// </summary>
    private IEnumerator HitShakeCoroutine()
    {
        float elapsed = 0f;
        float duration = 0.15f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float offset =
                Mathf.Sin(
                    elapsed / duration * Mathf.PI * 6f
                ) * hitShakeAmount;

            transform.localPosition =
                initialLocalPos
                + transform.right * offset;

            yield return null;
        }

        // 元の位置へ戻す
        transform.localPosition = initialLocalPos;
    }

    /// <summary>
    /// オブジェクトを初期状態に戻して復活させる
    /// </summary>
    private void Respawn()
    {
        currentHits = 0;
        broken = false;
        transform.localScale = initialScale;
        SetVisible(true);
    }

    /// <summary>
    /// Renderer と Collider の有効・無効を切り替える
    /// </summary>
    private void SetVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = visible;

        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = visible;
    }
}