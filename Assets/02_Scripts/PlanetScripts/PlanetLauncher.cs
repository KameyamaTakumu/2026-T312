using UnityEngine;

/// <summary>
/// 惑星間移動トリガー
/// 
/// プレイヤーが Trigger に入ると
/// 指定惑星へ飛行させる
/// </summary>
public class PlanetLauncher : MonoBehaviour
{
    [Header("飛行設定")]

    // 飛行先惑星
    [CustomLabel("目標惑星"), SerializeField]
    private Transform targetPlanet;

    // 惑星表面からどれくらい浮かせて着地させるか
    [CustomLabel("惑星表面への着地オフセット"), SerializeField]
    private float landingOffset = 2.5f;

    // 飛行時間
    [CustomLabel("飛行時間（秒）"), SerializeField]
    private float travelDuration = 2.5f;

    // 放物線高さ
    [CustomLabel("飛行放物線の高さ"), SerializeField]
    private float arcHeight = 20f;

    // 一度だけ使用可能にする
    [CustomLabel("一度だけ使用可"), SerializeField]
    private bool useOnce = true;

    // 使用済みフラグ
    private bool used = false;

    private void OnTriggerEnter(Collider other)
    {
        // 使用済みなら無視
        if (used)
            return;

        // プレイヤー以外無視
        if (!other.CompareTag("Player"))
            return;

        // 目標惑星未設定
        if (targetPlanet == null)
        {
           return;
        }

        PlanetWarpManager manager =
            PlanetWarpManager.Instance;

        // Manager 未存在
        if (manager == null)
        {
            return;
        }

        // ─────────────────────────────────
        // 着地点計算
        // ─────────────────────────────────

        // 惑星中心 → プレイヤー方向
        // これを法線方向として利用
        Vector3 dirToPlayer =
            (
                other.transform.position
                - targetPlanet.position
            ).normalized;

        // 惑星表面位置
        Vector3 landingPosition =
            targetPlanet.position
            + dirToPlayer * landingOffset;

        // 惑星間飛行開始
        manager.StartTravel(
            other.GetComponent<Rigidbody>(),
            landingPosition,
            targetPlanet,
            travelDuration,
            arcHeight
        );

        // 一度きり設定
        if (useOnce)
            used = true;
    }

#if UNITY_EDITOR

    /// <summary>
    /// Sceneビューで着地点を可視化
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (targetPlanet == null)
            return;

        // 現在位置方向を基準に着地点計算
        Vector3 dirToThis =
            (
                transform.position
                - targetPlanet.position
            ).normalized;

        Vector3 landingPosition =
            targetPlanet.position
            + dirToThis * landingOffset;

        // 着地点表示
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(landingPosition, 0.4f);

        // トリガー→着地点ライン
        Gizmos.color =
            new Color(0f, 1f, 1f, 0.4f);

        Gizmos.DrawLine(
            transform.position,
            landingPosition
        );

        // 惑星中心表示
        Gizmos.color =
            new Color(1f, 0.5f, 0f, 0.3f);

        Gizmos.DrawSphere(
            targetPlanet.position,
            0.6f
        );
    }

#endif
}