using UnityEngine;

/// <summary>
/// チュートリアル用のゴール到達判定ゾーン。
///
/// 主な役割：
/// ・プレイヤーの到達判定とTutorialManagerへの通知
/// ・GoalZone識別名の管理
/// ・到達後の非表示処理
/// ・Sceneビューでの範囲可視化
/// ・ゴール方向矢印の生成・破棄管理
/// </summary>
public class TutorialGoalZone : MonoBehaviour
{
    [Header("設定")]

    // 未入力の場合はGameObject名が自動使用される
    [CustomLabel("ゾーン名（TutorialStepのgoalZoneObjectNameと一致させる）"), SerializeField]
    private string zoneName = "";

    [CustomLabel("Gizmoの色"), SerializeField]
    private Color gizmoColor = new Color(0.2f, 1f, 0.4f, 0.25f);

    [CustomLabel("到達後に自動で非表示にする"), SerializeField]
    private bool hideOnReach = true;

    [Header("矢印設定")]

    // 未設定の場合は矢印を表示しない
    [CustomLabel("矢印Prefab（円錐など、未設定でスキップ）"), SerializeField]
    private GameObject arrowPrefab;

    // GoalZoneArrow側のデフォルトオフセットを上書きしたい場合に使う（0のままならデフォルト使用）
    [CustomLabel("頭上オフセット上書き（0でGoalZoneArrowのデフォルト使用）"), SerializeField]
    private float headOffsetOverride = 0f;

    private Collider col;

    // 生成した矢印インスタンス（到達時に破棄するため保持）
    private GameObject arrowInstance;

    private void Awake()
    {
        // 未設定ならGameObject名を使用し、TutorialStep側との連携を簡単にする
        if (string.IsNullOrEmpty(zoneName))
            zoneName = gameObject.name;
    }

    private void Start()
    {
        col = GetComponent<Collider>();
        if (col == null)
        {
           return;
        }

        col.isTrigger = true;
    }

    /// <summary>有効化時（TutorialManagerからSetActive(true)されたとき）に矢印を生成する</summary>
    private void OnEnable()
    {
        SpawnArrow();
    }

    /// <summary>無効化時（到達後 or ステップ完了時）に矢印を破棄してクリーンアップする</summary>
    private void OnDisable()
    {
        DestroyArrow();
    }

    // ─────────────────────────────────────────
    // 矢印の生成・破棄
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤーを探して矢印を生成する。プレイヤーが見つからないかPrefab未設定の場合はスキップする。
    /// </summary>
    private void SpawnArrow()
    {
        if (arrowPrefab == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return;
        }

        arrowInstance = Instantiate(arrowPrefab, player.transform.position, Quaternion.identity);

        GoalZoneArrow arrow = arrowInstance.GetComponent<GoalZoneArrow>();
        if (arrow == null)
        {
            return;
        }

        // 0は「GoalZoneArrow側のデフォルトを使う」の意味なので無視する
        if (headOffsetOverride > 0f)
            arrow.SetHeadOffset(headOffsetOverride);

        arrow.Initialize(player.transform, transform);
    }

    private void DestroyArrow()
    {
        if (arrowInstance != null)
        {
            Destroy(arrowInstance);
            arrowInstance = null;
        }
    }

    // ─────────────────────────────────────────
    // トリガー判定
    // ─────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        TutorialManager.Instance?.NotifyReachedGoalZone(zoneName);

        // 到達後に不要なら非表示化する（OnDisableで矢印も一緒に片付く）
        if (hideOnReach)
            gameObject.SetActive(false);
    }

#if UNITY_EDITOR

    /// <summary>Sceneビューで選択中のみ表示されるGizmo。GoalZoneの範囲を視覚的に示す。</summary>
    private void OnDrawGizmosSelected()
    {
        if (col == null) return;

        Gizmos.color = gizmoColor;

        if (col is SphereCollider sphereCollider)
        {
            float radius = sphereCollider.radius * Mathf.Max(
                transform.lossyScale.x,
                transform.lossyScale.y,
                transform.lossyScale.z);

            Gizmos.DrawSphere(transform.position + sphereCollider.center, radius);

            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
            Gizmos.DrawWireSphere(transform.position + sphereCollider.center, radius);
        }
        else if (col is BoxCollider boxCollider)
        {
            Matrix4x4 originalMatrix = Gizmos.matrix;

            // 回転・スケール込みで描画するため、GameObjectのTRSに合わせて行列を切り替える
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

            Gizmos.DrawCube(boxCollider.center, boxCollider.size);

            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);

            Gizmos.matrix = originalMatrix;
        }

        // 複数GoalZoneが存在する場合に判別しやすいよう、識別ラベルを表示する
        UnityEditor.Handles.Label(
            transform.position,
            $"GoalZone\n{zoneName}",
            new GUIStyle { normal = { textColor = Color.green }, fontSize = 11 });
    }
#endif
}