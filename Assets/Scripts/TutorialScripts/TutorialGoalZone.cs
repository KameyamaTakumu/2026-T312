using UnityEngine;

/// <summary>
/// チュートリアル用のゴール到達判定ゾーン
/// 
/// 主な役割：
/// ・プレイヤーの到達判定
/// ・TutorialManagerへの通知
/// ・GoalZone識別名の管理
/// ・到達後の非表示処理
/// ・Sceneビューでの範囲可視化
/// </summary>
public class TutorialGoalZone : MonoBehaviour
{
    [Header("設定")]

    // GoalZone の識別名
    // 未入力の場合は GameObject 名が自動使用される
    [CustomLabel("ゾーン名（TutorialStep の goalZoneObjectName と一致させる）"), SerializeField]
    private string zoneName = "";

    // Sceneビューで表示する Gizmo の色
    [CustomLabel("Gizmo の色"), SerializeField]
    private Color gizmoColor = new Color(0.2f, 1f, 0.4f, 0.25f);

    // 到達後に自動で無効化するか
    [CustomLabel("到達後に自動で非表示にする"), SerializeField]
    private bool hideOnReach = true;

    // ─────────────────────────────────────────

    private void Awake()
    {
        // 名前が未設定なら GameObject 名を使用
        // TutorialStep 側との連携を簡単にするため
        if (string.IsNullOrEmpty(zoneName))
            zoneName = gameObject.name;
    }

    /// <summary>
    /// プレイヤーがゾーンへ侵入したとき
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // プレイヤー以外は無視
        if (!other.CompareTag("Player")) return;

        Debug.Log($"[TutorialGoalZone] '{zoneName}' に到達");

        // TutorialManager に到達を通知
        TutorialManager.Instance?.NotifyReachedGoalZone(zoneName);

        // 到達後に不要なら非表示化
        if (hideOnReach)
            gameObject.SetActive(false);
    }

#if UNITY_EDITOR

    /// <summary>
    /// Sceneビューで選択中のみ表示される Gizmo
    ///
    /// GoalZone の範囲が視覚的に分かるように描画する
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();

        // Collider が無ければ描画しない
        if (col == null) return;

        Gizmos.color = gizmoColor;

        // SphereCollider の場合
        if (col is SphereCollider sc)
        {
            // Transform のスケールを考慮した半径
            float r = sc.radius * Mathf.Max(
                transform.lossyScale.x,
                transform.lossyScale.y,
                transform.lossyScale.z);

            // 半透明の塗りつぶし
            Gizmos.DrawSphere(transform.position + sc.center, r);

            // 輪郭線
            Gizmos.color = new Color(
                gizmoColor.r,
                gizmoColor.g,
                gizmoColor.b,
                0.9f);

            Gizmos.DrawWireSphere(transform.position + sc.center, r);
        }

        // BoxCollider の場合
        else if (col is BoxCollider bc)
        {
            Matrix4x4 old = Gizmos.matrix;

            // 回転・スケール込みで描画
            Gizmos.matrix = Matrix4x4.TRS(
                transform.position,
                transform.rotation,
                transform.lossyScale);

            // 半透明の箱
            Gizmos.DrawCube(bc.center, bc.size);

            // 輪郭線
            Gizmos.color = new Color(
                gizmoColor.r,
                gizmoColor.g,
                gizmoColor.b,
                0.9f);

            Gizmos.DrawWireCube(bc.center, bc.size);

            // 元の Gizmo 行列へ戻す
            Gizmos.matrix = old;
        }

        // シーン上に識別ラベルを表示
        // GoalZone が複数ある場合に判別しやすくなる
        UnityEditor.Handles.Label(
            transform.position,
            $"GoalZone\n{zoneName}",
            new GUIStyle
            {
                normal = { textColor = Color.green },
                fontSize = 11
            }
        );
    }
#endif
}