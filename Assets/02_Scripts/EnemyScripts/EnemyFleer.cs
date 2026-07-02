using UnityEngine;

/// <summary>
/// 逃げ回る敵AI
///
/// 主な役割：
/// ・プレイヤーが近づくと逃げる
/// ・未検知時は待機、またはその場周辺をランダム徘徊
/// ・プレイヤーと接触すると捕獲され、
///   指定の ObjectVisibilityController を Show() して消滅する
/// </summary>
[RequireComponent(typeof(GravityBody))]
public class EnemyFleer : MonoBehaviour
{
    [Header("索敵設定")]

    // プレイヤーを検知して逃走を開始する範囲
    [CustomLabel("逃走を開始する検知範囲"), SerializeField]
    private float detectionRange = 6f;

    // この距離より離れたら逃走をやめて待機状態に戻る
    // detectionRange より大きい値にしておくことで
    // 境界付近での状態のチラつきを防ぐ（ヒステリシス）
    [CustomLabel("逃走をやめる距離"), SerializeField]
    private float safeDistance = 9f;

    [Header("移動設定")]

    // 逃走速度
    [CustomLabel("逃走速度"), SerializeField]
    private float fleeSpeed = 5f;

    // 待機時の徘徊速度
    [CustomLabel("待機時の徘徊速度"), SerializeField]
    private float wanderSpeed = 1f;

    // 向き変更の滑らかさ（大きいほど素早く目標方向を向く）
    [CustomLabel("向き変更の滑らかさ"), SerializeField]
    private float rotationSpeed = 10f;

    [Header("徘徊設定")]

    // 待機中にランダム徘徊させるか
    // falseの場合は検知するまでその場で静止する
    [CustomLabel("待機時にランダム徘徊するか"), SerializeField]
    private bool enableWander = true;

    // 徘徊できる原点（初期位置）からの半径
    [CustomLabel("徘徊可能な原点からの半径"), SerializeField]
    private float wanderRadius = 3f;

    // 徘徊目標を選び直す間隔（秒）
    // 目標地点に到達した場合はこの時間を待たずに更新する
    [CustomLabel("徘徊目標の更新間隔（秒）"), SerializeField]
    private float wanderInterval = 2.5f;

    // 徘徊目標への到達とみなす許容距離
    // これより目標との距離が近づいたら到達済みとみなし、次の目標を選び直す
    [CustomLabel("徘徊目標への到達許容距離"), SerializeField]
    private float wanderArriveDistance = 0.3f;

    [Header("捕獲時の処理")]

    // 捕獲時に報告するグループ
    [CustomLabel("所属する捕獲グループ"), SerializeField]
    private CaptureGroup captureGroup;

    [Header("Gizmo")]

    // Sceneビューで範囲表示
    [CustomLabel("検知範囲などを可視化"), SerializeField]
    private bool showGizmo = true;

    // ─────────────────────────────────────────
    // AI状態
    // ─────────────────────────────────────────

    // この敵が取りうる行動状態
    private enum CreatureState
    {
        Idle,   // 待機（または徘徊）中
        Flee    // プレイヤーから逃走中
    }

    private CreatureState state = CreatureState.Idle;

    // ─────────────────────────────────────────
    // 内部参照・状態
    // ─────────────────────────────────────────

    private Rigidbody rb;

    // プレイヤー参照（"Player"タグから自動取得）
    private Transform playerTransform;

    // 初期位置（徘徊範囲の中心として使用）
    private Vector3 originPosition;

    // 捕獲済みフラグ。Destroy()が呼ばれるまでの間に
    // 多重に衝突判定が走っても二重処理されないようにする
    private bool isCaught = false;

    // 現在の徘徊目標地点（ワールド座標）
    private Vector3 wanderTarget;

    // 徘徊目標を選んでからの経過時間
    private float wanderTimer;

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // 配置された初期位置を徘徊の中心として記憶しておく
        originPosition = transform.position;
        wanderTarget = originPosition;
    }

    private void Start()
    {
        // シーン上の"Player"タグが付いたオブジェクトを検索して参照を保持
        // （毎フレームFindするとコストが高いため、ここで一度だけ取得）
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            playerTransform = player.transform;
    }

    private void FixedUpdate()
    {
        // 捕獲済み、またはプレイヤー未検出（シーンにいない等）の場合は何もしない
        if (isCaught || playerTransform == null)
            return;

        float distToPlayer =
            Vector3.Distance(
                transform.position,
                playerTransform.position
            );

        // ─────────────────────────────────────
        // 状態遷移
        // detectionRange（逃走開始）と safeDistance（逃走解除）に
        // 差を持たせるヒステリシスを設けて境界でのチラつきを防止
        // ─────────────────────────────────────

        switch (state)
        {
            case CreatureState.Idle:
                // 検知範囲内に入ったら逃走状態へ
                if (distToPlayer <= detectionRange)
                    state = CreatureState.Flee;
                break;

            case CreatureState.Flee:
                // 十分離れたら待機状態へ戻る
                if (distToPlayer >= safeDistance)
                    state = CreatureState.Idle;
                break;
        }

        // ─────────────────────────────────────
        // 状態別処理
        // 上の遷移判定の直後に、現在の状態に応じた移動処理を実行する
        // ─────────────────────────────────────

        switch (state)
        {
            case CreatureState.Flee:
                UpdateFlee();
                break;

            case CreatureState.Idle:
                if (enableWander)
                    UpdateWander();
                break;
        }
    }

    /// <summary>
    /// プレイヤーと物理的に衝突した時に呼ばれる。
    /// 捕獲判定はここ（OnCollisionEnter）で行う点に注意
    /// （トリガーではなく実体としての衝突で判定している）
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (isCaught) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        Catch();
    }

    // ─────────────────────────────────────────
    // 逃走処理
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤーと反対方向へ逃げる
    /// </summary>
    private void UpdateFlee()
    {
        // 球状の惑星等、重力方向が一定でない地形を想定し、
        // transform.up（その場の「上」）を地面の法線として扱う
        Vector3 planetUp = transform.up;

        // プレイヤーから自分への方向 = 逃げる方向
        // 惑星面（接平面）へ投影することで上下成分を除去し、
        // 地表に沿った水平方向の逃走ベクトルだけを取り出す
        Vector3 fleeDir =
            Vector3.ProjectOnPlane(
                transform.position
                - playerTransform.position,
                planetUp
            ).normalized;

        MoveInDirection(fleeDir, fleeSpeed);
    }

    // ─────────────────────────────────────────
    // 待機・徘徊処理
    // ─────────────────────────────────────────

    /// <summary>
    /// 原点周辺をランダムに徘徊する
    /// </summary>
    private void UpdateWander()
    {
        wanderTimer += Time.fixedDeltaTime;

        float distToTarget =
            Vector3.Distance(transform.position, wanderTarget);

        // 目標へ到達済み、または更新間隔が来たら次の目標を選ぶ
        if (distToTarget <= wanderArriveDistance
            || wanderTimer >= wanderInterval)
        {
            PickNewWanderTarget();
            wanderTimer = 0f;
        }

        Vector3 planetUp = transform.up;

        // 現在地から徘徊目標への方向を、惑星面に沿って計算
        Vector3 dir =
            Vector3.ProjectOnPlane(
                wanderTarget - transform.position,
                planetUp
            ).normalized;

        // 目標地点とほぼ同じ位置（方向ベクトルがほぼ0）なら
        // 不要な回転・移動処理を避けて早期return
        if (dir.sqrMagnitude < 0.001f)
            return;

        MoveInDirection(dir, wanderSpeed);
    }

    /// <summary>
    /// 原点を中心とした円内からランダムに徘徊目標を選ぶ
    /// （惑星の接平面上に投影して求める）
    /// </summary>
    private void PickNewWanderTarget()
    {
        Vector3 planetUp = transform.up;

        // 接平面（惑星表面）上の基準軸（tangent）を1つ作るための仮の参照ベクトル。
        // planetUpとほぼ平行（内積が0.9超）な場合、ProjectOnPlaneの結果が
        // ほぼゼロベクトルになり不安定になるため、その場合は別軸にフォールバックする
        Vector3 reference = Vector3.forward;
        if (Mathf.Abs(Vector3.Dot(reference, planetUp)) > 0.9f)
            reference = Vector3.right;

        // referenceを接平面に投影して正規化したものを軸1（tangent）とする
        Vector3 tangent =
            Vector3.ProjectOnPlane(reference, planetUp).normalized;
        // planetUpとtangentの外積で、接平面上で直交するもう1つの軸（bitangent）を得る
        Vector3 bitangent =
            Vector3.Cross(planetUp, tangent).normalized;

        // 半径wanderRadius以内のランダムな2次元座標を取得
        Vector2 rand = Random.insideUnitCircle * wanderRadius;

        // tangent・bitangentの2軸を使い、接平面上のランダムな点をワールド座標に変換
        wanderTarget =
            originPosition
            + tangent * rand.x
            + bitangent * rand.y;
    }

    // ─────────────────────────────────────────
    // 惑星面上移動（EnemyChaser と同様の処理）
    // ─────────────────────────────────────────

    /// <summary>
    /// 指定した方向（惑星面に沿った水平方向）へ、
    /// 重力方向の速度成分を保ったまま移動・回転させる共通処理。
    /// </summary>
    private void MoveInDirection(
        Vector3 direction,
        float speed)
    {
        // 極小ベクトル（ほぼゼロ）の場合は回転・移動計算が
        // 不安定になるため処理しない
        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 planetUp = transform.up;

        // ─────────────────────────────────────
        // 回転
        // 進行方向を向きつつ、惑星表面の「上」を維持するように
        // 目標回転を求め、Slerpで滑らかに近づける
        // ─────────────────────────────────────

        Quaternion targetRot =
            Quaternion.LookRotation(
                direction,
                planetUp
            );

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                targetRot,
                rotationSpeed * Time.fixedDeltaTime
            )
        );

        // ─────────────────────────────────────
        // 移動
        // ─────────────────────────────────────

        // 現在の速度のうち、惑星の「上」方向（重力・落下/ジャンプ等）の
        // 成分だけを取り出して保持する。これにより、水平移動の速度を
        // 上書きしても落下や跳ね返り等の上下方向の動きを壊さない
        Vector3 verticalVel =
            Vector3.Project(
                rb.linearVelocity,
                planetUp
            );

        // 水平方向の速度を指定方向・速度で上書きし、
        // 上下方向の速度成分のみ元の値を足し合わせる
        rb.linearVelocity =
            direction * speed
            + verticalVel;
    }

    // ─────────────────────────────────────────
    // 捕獲処理
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤーに捕獲された時の処理
    /// </summary>
    private void Catch()
    {
        // 以降の衝突・更新処理を無効化するためのフラグを立てる
        isCaught = true;

        // 捕獲をグループに報告するだけ。
        // 「何体で開放するか」「何を開放するか」はCaptureGroup側で決める
        if (captureGroup != null)
            captureGroup.NotifyCaught();

        // 敵オブジェクト自体を破棄
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────
    // Gizmo
    // ─────────────────────────────────────────

#if UNITY_EDITOR
    /// <summary>
    /// エディタ上でこのオブジェクトを選択した時に、
    /// 検知範囲・逃走解除距離・徘徊範囲をSceneビューに可視化する。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showGizmo)
            return;

        // 検知範囲（プレイヤーがここに入ると逃走開始）：水色の半透明球＋輪郭
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, detectionRange);

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 逃走をやめる距離：オレンジの輪郭球
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, safeDistance);

        // 徘徊範囲：緑の輪郭球
        // 再生中はoriginPosition（実際の徘徊中心）を、
        // 編集中はtransform.position（現在の配置位置）を中心として表示する
        if (enableWander)
        {
            Vector3 center =
                Application.isPlaying
                    ? originPosition
                    : transform.position;

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(center, wanderRadius);
        }
    }
#endif
}