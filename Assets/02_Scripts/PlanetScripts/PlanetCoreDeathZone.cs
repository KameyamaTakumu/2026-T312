using UnityEngine;

/// <summary>
/// 惑星中心の即死判定ゾーン
///
/// 主な役割：
/// ・プレイヤー侵入検知
/// ・即死処理実行
/// ・引力ジャンプ中の除外処理
/// ・エディタ上での可視化
/// </summary>
public class PlanetCoreDeathZone : MonoBehaviour
{
    // ─────────────────────────────────────────
    // インスペクター設定
    // ─────────────────────────────────────────

    [Header("即死ゾーン設定")]

    /// <summary>
    /// 即死判定を有効にするか
    /// OFF の場合はプレイヤーが侵入しても何も起きない
    /// </summary>
    [CustomLabel("即死ゾーンを有効にする"), SerializeField]
    private bool isActive = true;

    /// <summary>
    /// シーンビューでGizmoを表示するか
    /// </summary>
    [CustomLabel("Gizmo表示"), SerializeField]
    private bool showGizmo = true;

    // ─────────────────────────────────────────
    // Triggerイベント
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤーが即死ゾーンへ侵入した時
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // 即死ゾーン無効なら何もしない
        if (!isActive)
            return;

        // Playerタグ以外は無視
        if (!other.CompareTag("Player"))
            return;

        // ─────────────────────
        // PlayerHealth取得
        // ─────────────────────

        // プレイヤー本体または親オブジェクトに
        // PlayerHealthが付いているケースに対応
        //
        PlayerHealth playerHealth =
            other.GetComponent<PlayerHealth>()
            ?? other.GetComponentInParent<PlayerHealth>();

        // PlayerHealthが見つからなければ終了
        if (playerHealth == null)
            return;

        // ─────────────────────
        // GravityBody取得
        // ─────────────────────

        // 引力ジャンプ状態を調べるために使用
        GravityBody gravityBody =
            other.GetComponent<GravityBody>()
            ?? other.GetComponentInParent<GravityBody>();

        // ─────────────────────
        // 引力ジャンプ中判定
        // ─────────────────────

        // GravityJumpZoneによる移動中は
        // 惑星内部を通過する場合がある
        // そのため即死判定を無効化する
        if (gravityBody != null &&
            gravityBody.IsBeingAttracted)
        {
            Debug.Log(
                "[PlanetCoreDeathZone] 引力ジャンプ中のためスキップ");

            return;
        }

        // ─────────────────────
        // 即死処理
        // ─────────────────────

        // PlayerHealth側で
        // 死亡演出やリスポーン処理が行われる。
        Debug.Log(
            $"[PlanetCoreDeathZone] {gameObject.name} の中心核に触れました。即死");

        playerHealth.InstantKill();
    }

#if UNITY_EDITOR

    /// <summary>
    /// オブジェクト選択中のみGizmoを描画
    /// 赤い球体で即死範囲を可視化する
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Gizmo非表示設定なら終了
        if (!showGizmo)
            return;

        // SphereCollider取得
        SphereCollider col =
            GetComponent<SphereCollider>();

        // ワールド空間での実際の半径を計算
        // SphereCollider.radius はローカル値なので
        // Transformのスケールを考慮する必要がある
        //
        float r = col != null
            ? col.radius * Mathf.Max(
                transform.lossyScale.x,
                transform.lossyScale.y,
                transform.lossyScale.z)
            : 1f;

        // 半透明の塗りつぶし球
        Gizmos.color =
            new Color(1f, 0.1f, 0.1f, 0.25f);

        Gizmos.DrawSphere(
            transform.position,
            r);

        // 外周線
        Gizmos.color =
            new Color(1f, 0.1f, 0.1f, 0.9f);

        Gizmos.DrawWireSphere(
            transform.position,
            r);
    }

#endif
}