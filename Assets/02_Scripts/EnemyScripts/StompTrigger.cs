using UnityEngine;

/// <summary>
/// 敵の「踏みつけ専用判定」を行うトリガー
/// 
/// 主な役割：
/// ・プレイヤーが敵の上に乗ったことを検知
/// ・EnemyBase に踏みつけ通知を送る
/// ・踏みつけ判定を本体コライダーと分離する
/// </summary>
public class StompTrigger : MonoBehaviour
{
    [CustomLabel("Gizmo表示"), SerializeField]
    private bool showGizmo = true;

    // 親に存在する EnemyBase を保持
    // 毎回 GetComponentInParent を呼ばないようキャッシュする
    private EnemyBase enemyBase;

    private void Awake()
    {
        // 親オブジェクトから EnemyBase を取得
        // StompTrigger は敵本体の子に付ける前提なので、
        // GetComponentInParent を使用している
        enemyBase = GetComponentInParent<EnemyBase>();
    }

    /// <summary>
    /// Trigger 接触時
    /// </summary>
    /// <param name="other">
    /// 接触したコライダー
    /// </param>
    private void OnTriggerEnter(Collider other)
    {
        // EnemyBase が存在しないなら処理不可
        if (enemyBase == null) return;

        // プレイヤー以外は無視
        if (!other.CompareTag("Player")) return;

        // プレイヤーの HP コンポーネント取得
        PlayerHealth playerHealth =
            other.GetComponent<PlayerHealth>()
            ?? other.GetComponentInParent<PlayerHealth>();

        // Rigidbody を取得
        // プレイヤー本体ではなく
        // 子オブジェクト側に Collider がある場合もあるため、
        // GetComponentInParent も試している
        Rigidbody playerRb =
            other.GetComponent<Rigidbody>()
            ?? other.GetComponentInParent<Rigidbody>();

        // EnemyBase 側へ
        // 「踏みつけられた」ことを通知
        // ダメージ処理やバウンド処理などは
        // EnemyBase 側で実装する
        enemyBase.OnStomped(playerRb, playerHealth);
    }

#if UNITY_EDITOR

    /// <summary>
    /// SceneView 上で踏みつけ判定を可視化
    /// 
    /// 選択中のみ表示される
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;

        // 半透明の緑色
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.35f);

        // このオブジェクトの Collider を取得
        Collider col = GetComponent<Collider>();

        if (col != null)
        {
            // Collider サイズに合わせて球表示
            //
            // bounds はワールド座標基準
            // extents は半径サイズ
            Gizmos.DrawSphere(
                col.bounds.center,
                col.bounds.extents.magnitude * 0.5f
            );
        }
        else
        {
            // Collider が無い場合は仮サイズ表示
            Gizmos.DrawSphere(transform.position, 0.5f);
        }
    }

#endif
}