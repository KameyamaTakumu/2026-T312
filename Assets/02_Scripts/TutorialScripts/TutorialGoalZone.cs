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
/// ・ゴール方向矢印の生成・破棄管理
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
    // 矢印設定
    // ─────────────────────────────────────────

    [Header("矢印設定")]

    // 矢印として使う Prefab（円錐など）
    // 未設定の場合は矢印を表示しない
    [CustomLabel("矢印 Prefab（円錐など、未設定でスキップ）"), SerializeField]
    private GameObject arrowPrefab;

    // プレイヤーの頭上からのオフセット
    // GoalZoneArrow 側のデフォルトを上書きしたい場合に使う
    // ※ GoalZoneArrow の Inspector 設定を直接使う場合は 0 のまま
    [CustomLabel("頭上オフセット上書き（0 で GoalZoneArrow のデフォルト使用）"), SerializeField]
    private float headOffsetOverride = 0f;

    // 自身が持つ Collider
    private Collider col;

    // 生成した矢印 GameObject への参照（到達時に破棄するため保持）
    private GameObject _arrowInstance;

    // ─────────────────────────────────────────

    private void Awake()
    {
        // 名前が未設定なら GameObject 名を使用
        // TutorialStep 側との連携を簡単にするため
        if (string.IsNullOrEmpty(zoneName))
            zoneName = gameObject.name;
    }

    private void Start()
    {
        // isTrigger や Gizmo 描画のため Collider を取得
        col = GetComponent<Collider>();

        // IsTriggerを有効にしておく
        col.isTrigger = true;
    }

    /// <summary>
    /// GameObject が有効化されたとき（TutorialManager から SetActive(true) されたとき）
    /// 矢印を生成してプレイヤーへの道案内を開始する
    /// </summary>
    private void OnEnable()
    {
        SpawnArrow();
    }

    /// <summary>
    /// GameObject が無効化されたとき（到達後 or ステップ完了時）
    /// 矢印を破棄してクリーンアップする
    /// </summary>
    private void OnDisable()
    {
        DestroyArrow();
    }

    // ─────────────────────────────────────────
    // 矢印の生成・破棄
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤーを探して矢印を生成する
    /// プレイヤーが見つからないか Prefab 未設定の場合はスキップ
    /// </summary>
    private void SpawnArrow()
    {
        // Prefab が未設定なら矢印なしで動作する
        if (arrowPrefab == null) return;

        // タグでプレイヤーを検索
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[TutorialGoalZone] Player タグのオブジェクトが見つかりません（矢印スキップ）");
            return;
        }

        // 矢印を生成してプレイヤーの頭上に初期配置
        _arrowInstance = Instantiate(arrowPrefab, player.transform.position, Quaternion.identity);

        // GoalZoneArrow コンポーネントを取得して初期化
        GoalZoneArrow arrow = _arrowInstance.GetComponent<GoalZoneArrow>();
        if (arrow == null)
        {
            Debug.LogWarning("[TutorialGoalZone] arrowPrefab に GoalZoneArrow コンポーネントがありません");
            return;
        }

        // オフセット上書きが指定されていれば反映（0 はデフォルト使用として無視）
        if (headOffsetOverride > 0f)
        {
            // GoalZoneArrow の headOffset フィールドを上書きする
            // ※ GoalZoneArrow 側を [SerializeField] で公開しているためリフレクション不要
            arrow.SetHeadOffset(headOffsetOverride);
        }

        arrow.Initialize(player.transform, transform);

        Debug.Log($"[TutorialGoalZone] '{zoneName}' の矢印を生成");
    }

    /// <summary>
    /// 矢印インスタンスを破棄する
    /// </summary>
    private void DestroyArrow()
    {
        if (_arrowInstance != null)
        {
            Destroy(_arrowInstance);
            _arrowInstance = null;
        }
    }

    // ─────────────────────────────────────────
    // トリガー判定
    // ─────────────────────────────────────────

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

        // 到達後に不要なら非表示化（OnDisable で矢印も一緒に片付く）
        if (hideOnReach)
            gameObject.SetActive(false);
    }

#if UNITY_EDITOR

    /// <summary>
    /// Sceneビューで選択中のみ表示される Gizmo
    /// GoalZone の範囲が視覚的に分かるように描画する
    /// </summary>
    private void OnDrawGizmosSelected()
    {
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