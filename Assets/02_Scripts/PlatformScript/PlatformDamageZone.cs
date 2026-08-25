using UnityEngine;

/// <summary>
/// プレイヤー接触時にダメージを与えるゾーン
///
/// 主な役割：
/// ・プレイヤー侵入検知
/// ・ダメージ処理
/// ・継続接触時のダメージ判定
/// ・エディタ上での可視化
/// </summary>
public class PlatformDamageZone : MonoBehaviour
{
    // ─────────────────────────────────────────
    // インスペクター設定
    // ─────────────────────────────────────────

    [Header("ダメージ設定")]

    /// <summary>
    /// プレイヤーに与えるダメージ量
    /// </summary>
    [CustomLabel("与えるダメージ量"), SerializeField]
    private int damage = 1;

    // ─────────────────────────────────────────
    // Triggerイベント
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤーがゾーンへ侵入した時
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Playerタグ以外は無視
        if (!other.CompareTag("Player"))
            return;

        // ─────────────────────
        // PlayerHealth取得
        // ─────────────────────

        // プレイヤー本体に付いている場合と、
        // 親オブジェクトに付いている場合の両方に対応
        PlayerHealth playerHealth =
            other.GetComponent<PlayerHealth>()
            ?? other.GetComponentInParent<PlayerHealth>();

        // PlayerHealth が見つからなければ終了
        if (playerHealth == null)
            return;

        // ダメージを与える
        playerHealth.TakeDamage(damage);
    }

    /// <summary>
    /// プレイヤーがゾーン内に居続けている間
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        // ─────────────────────
        // 継続ダメージ
        // ─────────────────────

        // プレイヤーが押しつぶされたり、
        // ダメージゾーンから抜けられない場合でもダメージ判定を継続する

        // 実際にダメージが入るかどうかは
        // PlayerHealth 側の無敵時間に依存する
        OnTriggerEnter(other);
    }

#if UNITY_EDITOR

    /// <summary>
    /// シーンビューで選択中のみGizmoを表示
    /// ダメージ範囲をオレンジ色で可視化する。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();

        // Collider が無ければ描画できない
        if (col == null)
            return;

        // 半透明の塗りつぶし
        Gizmos.color =
            new Color(1f, 0.2f, 0f, 0.4f);

        Gizmos.DrawCube(
            col.bounds.center,
            col.bounds.size);

        // 外枠
        Gizmos.color =
            new Color(1f, 0.2f, 0f, 0.9f);

        Gizmos.DrawWireCube(
            col.bounds.center,
            col.bounds.size);
    }
#endif
}