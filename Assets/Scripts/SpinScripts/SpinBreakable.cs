using System.Collections;
using UnityEngine;

/// <summary>
/// スピン攻撃で破壊できるオブジェクト
/// 
/// 主な役割：
/// ・スピン攻撃受付
/// ・ヒット数管理
/// ・破壊演出
/// ・ドロップ生成
/// ・復活処理
/// ・揺れ演出
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

    // 破壊後に復活するか
    [CustomLabel("復活する"), SerializeField]
    private bool respawns = false;

    // 復活までの待機時間
    [CustomLabel("復活時間（秒）"), SerializeField]
    private float respawnTime = 5f;

    [Header("ドロップ設定")]

    // ドロップ生成 Prefab
    //
    // null の場合は何も生成しない
    [CustomLabel("ドロップPrefab"), SerializeField]
    private GameObject dropPrefab;

    // ドロップ数
    [CustomLabel("ドロップ数"), SerializeField]
    private int dropCount = 1;

    // ドロップ飛散力
    [CustomLabel("ドロップ飛散力"), SerializeField]
    private float dropForce = 4f;

    [Header("演出設定")]

    // 破壊時の縮小演出時間
    [CustomLabel("破壊演出時間（秒）"), SerializeField]
    private float breakAnimDuration = 0.2f;

    // ヒット時の揺れ量
    [CustomLabel("ヒット時の揺れ量"), SerializeField]
    private float hitShakeAmount = 0.1f;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    // 現在ヒット数
    private int currentHits = 0;

    // 破壊済みか
    private bool broken = false;

    // 初期スケール
    // 復活時に使用
    private Vector3 initialScale;

    // 初期ローカル位置
    // 揺れ演出後に戻すため保持
    private Vector3 initialLocalPos;

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    void Awake()
    {
        // 初期状態保存
        initialScale = transform.localScale;
        initialLocalPos = transform.localPosition;
    }

    // ─────────────────────────────────────────
    // 公開API
    // ─────────────────────────────────────────

    /// <summary>
    /// PlayerSpin から呼ばれる
    /// スピン攻撃ヒット処理
    /// </summary>
    /// <param name="attacker">
    /// 攻撃した Transform
    /// </param>
    public void OnSpin(Transform attacker)
    {
        // 既に破壊済みなら無視
        if (broken) return;

        // ヒット数加算
        currentHits++;

        // 必要ヒット数に達した
        if (currentHits >= hitsToBreak)
        {
            Break(attacker);
        }
        else
        {
            // まだ壊れない場合は揺れ演出
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

        // ドロップ生成
        SpawnDrops(attacker);

        // 縮小演出開始
        StartCoroutine(BreakAnimCoroutine());
    }

    /// <summary>
    /// ドロップ生成
    /// </summary>
    private void SpawnDrops(Transform attacker)
    {
        if (dropPrefab == null || dropCount <= 0)
            return;

        for (int i = 0; i < dropCount; i++)
        {
            // 少し上に生成
            Vector3 spawnPos =
                transform.position
                + transform.up * 0.5f;

            GameObject drop =
                Instantiate(
                    dropPrefab,
                    spawnPos,
                    Quaternion.identity
                );

            // Rigidbody がある場合は飛散
            Rigidbody dropRb =
                drop.GetComponent<Rigidbody>();

            if (dropRb != null)
            {
                // 惑星の上方向
                Vector3 up = transform.up;

                // ランダムな水平方向
                Vector3 randomH =
                    Vector3.ProjectOnPlane(
                        Random.insideUnitSphere,
                        up
                    ).normalized;

                // Unity 重力は無効
                // GravityBody 使用前提
                dropRb.useGravity = false;

                // 上方向 + 横方向へ飛散
                dropRb.AddForce(
                    (up + randomH * 0.5f).normalized
                    * dropForce,
                    ForceMode.Impulse
                );
            }
        }
    }

    /// <summary>
    /// 破壊縮小演出
    /// </summary>
    private IEnumerator BreakAnimCoroutine()
    {
        float elapsed = 0f;

        // 現在スケール保存
        Vector3 startScale =
            transform.localScale;

        while (elapsed < breakAnimDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                elapsed / breakAnimDuration;

            // 徐々に縮小
            transform.localScale =
                Vector3.Lerp(
                    startScale,
                    Vector3.zero,
                    t
                );

            yield return null;
        }

        // 完全縮小
        transform.localScale = Vector3.zero;

        // ────────────────────────────────
        // 復活する場合
        // ────────────────────────────────

        if (respawns)
        {
            // 一時非表示
            SetVisible(false);

            // 復活待機
            yield return new WaitForSeconds(
                respawnTime
            );

            Respawn();
        }
        else
        {
            // 完全削除
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ヒット時の揺れ演出
    /// </summary>
    private IEnumerator HitShakeCoroutine()
    {
        float elapsed = 0f;

        // 揺れ演出時間
        float duration = 0.15f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // sin波で左右振動
            float offset =
                Mathf.Sin(
                    elapsed / duration
                    * Mathf.PI * 6f
                ) * hitShakeAmount;

            transform.localPosition =
                initialLocalPos
                + transform.right * offset;

            yield return null;
        }

        // 元位置へ戻す
        transform.localPosition =
            initialLocalPos;
    }

    /// <summary>
    /// 復活処理
    /// </summary>
    private void Respawn()
    {
        // 状態初期化
        currentHits = 0;
        broken = false;

        // スケール復元
        transform.localScale =
            initialScale;

        // 再表示
        SetVisible(true);
    }

    /// <summary>
    /// Renderer / Collider の表示切替
    /// </summary>
    private void SetVisible(bool visible)
    {
        // Renderer 切替
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = visible;
        }

        // Collider 切替
        foreach (var c in GetComponentsInChildren<Collider>())
        {
            c.enabled = visible;
        }
    }
}