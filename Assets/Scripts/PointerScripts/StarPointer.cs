using UnityEngine;

/// <summary>
/// スターポインター
/// 
/// 主な役割：
/// ・マウス位置から Ray を飛ばす
/// ・コインホバー判定
/// ・コイン引き寄せ開始
/// ・クリックによるコイン発射
/// </summary>
public class StarPointer : MonoBehaviour
{
    [Header("参照")]

    // プレイヤーTransform
    [CustomLabel("プレイヤー Transform"), SerializeField]
    private Transform playerTransform;

    // Ray 発射に使用するカメラ
    // 未設定時は MainCamera を自動使用
    [CustomLabel("使用カメラ（未設定=MainCamera）"), SerializeField]
    private Camera pointerCamera;

    [Header("ポインター設定")]

    // コイン判定用レイヤー
    [CustomLabel("ホバー判定レイヤー"), SerializeField]
    private LayerMask coinLayer = ~0;

    // ポインター最大射程
    [CustomLabel("ポインターの最大射程"), SerializeField]
    private float maxRange = 60f;

    // SphereCast の半径
    // 値を大きくすると狙いやすくなる
    [CustomLabel("コインホバー判定の半径（SphereCast）"), SerializeField]
    private float hoverRadius = 0.4f;

    [Header("コイン発射")]

    // 発射するコインPrefab
    [CustomLabel("発射コイン Prefab"), SerializeField]
    private GameObject launchedCoinPrefab;

    // 発射速度
    [CustomLabel("発射速度"), SerializeField]
    private float launchSpeed = 22f;

    // 発射方向のランダムブレ
    // 0 にすると完全直進
    [CustomLabel("発射方向のブレ幅"), SerializeField]
    private float launchSpread = 0f;

    // カメラ上方向への生成位置オフセット
    [CustomLabel("発射生成位置：カメラ上方向のオフセット"), SerializeField]
    private float spawnUpOffset = 0.5f;

    // 現在ホバー中のコイン
    private Coin hoveredCoin;

    private void Awake()
    {
        // カメラ未設定なら MainCamera を使用
        if (pointerCamera == null)
            pointerCamera = Camera.main;
    }

    private void Update()
    {
        // 使用カメラが存在しない場合は処理しない
        if (pointerCamera == null)
            return;

        // マウス位置から Ray を作成
        Ray ray = pointerCamera.ScreenPointToRay(Input.mousePosition);

        // 現フレームでコインにヒットしたか
        bool hitCoin = false;

        // ─────────────────────────────────────────
        // コイン検出
        // ─────────────────────────────────────────

        // 少し太い Ray（SphereCast）を飛ばすことで
        // 狙いやすさを向上させる
        if (Physics.SphereCast(
                ray,
                hoverRadius,
                out RaycastHit hit,
                maxRange,
                coinLayer))
        {
            // 発射コインは回収対象外
            bool isLaunchedCoin =
                hit.collider.GetComponent<LaunchedCoin>() != null
                || hit.collider.GetComponentInParent<LaunchedCoin>() != null;

            // 通常コインのみ処理
            if (!isLaunchedCoin)
            {
                // 子階層に Collider がある場合も考慮
                Coin coin =
                    hit.collider.GetComponent<Coin>()
                    ?? hit.collider.GetComponentInParent<Coin>();

                // 未回収コインなら
                if (coin != null && coin.State != Coin.CoinState.Collected)
                {
                    hitCoin = true;

                    // ホバー対象が切り替わった時のみ更新
                    if (hoveredCoin != coin)
                    {
                        // 前回ホバーしていたコインを解除
                        hoveredCoin?.SetIdle();

                        // 新しい対象へ更新
                        hoveredCoin = coin;

                        // 即時回収
                        hoveredCoin.Collect();
                    }
                }
            }
        }

        // ─────────────────────────────────────────
        // ホバー解除
        // ─────────────────────────────────────────

        // 今フレームでコインにヒットしなかった場合
        if (!hitCoin && hoveredCoin != null)
        {
            hoveredCoin.SetIdle();
            hoveredCoin = null;
        }

        // ─────────────────────────────────────────
        // 左クリックでコイン発射
        // ─────────────────────────────────────────

        if (Input.GetMouseButtonDown(0))
            TryFireCoin(ray);
    }

    /// <summary>
    /// コイン発射処理
    /// </summary>
    private void TryFireCoin(Ray ray)
    {
        CoinManager manager = CoinManager.Instance;

        // CoinManager が存在しない
        if (manager == null)
            return;

        // 所持コイン不足
        if (manager.CoinCount <= 0)
        {
            return;
        }

        // Prefab 未設定
        if (launchedCoinPrefab == null)
        {
            Debug.LogWarning("[StarPointer] 発射コイン Prefab が未設定です");
            return;
        }

        // コインを1枚消費
        manager.ConsumeCoins(1);

        // ───────── 発射方向計算 ─────────

        Vector3 launchDir = ray.direction;

        // ランダムブレ追加
        if (launchSpread > 0f)
        {
            launchDir += new Vector3(
                Random.Range(-launchSpread, launchSpread),
                Random.Range(-launchSpread, launchSpread),
                Random.Range(-launchSpread, launchSpread)
            );
        }

        // 正規化して方向ベクトル化
        launchDir = launchDir.normalized;

        // ───────── 発射位置計算 ─────────

        // カメラ位置から少し上にずらす
        // プレイヤーや地面との干渉防止目的
        Vector3 spawnPos =
            pointerCamera.transform.position
            + pointerCamera.transform.up * spawnUpOffset;

        // ───────── 発射 ─────────

        GameObject obj = Instantiate(
            launchedCoinPrefab,
            spawnPos,
            Quaternion.LookRotation(launchDir)
        );

        // 発射スクリプト取得
        LaunchedCoin lc = obj.GetComponent<LaunchedCoin>();

        // 発射開始
        if (lc != null)
            lc.Fire(launchDir, launchSpeed);
    }

    /// <summary>
    /// 何もない場所をクリックした時の拡張用
    /// 継承先で override 可能
    /// </summary>
    protected virtual void OnClickEmpty(Ray ray) { }

#if UNITY_EDITOR

    /// <summary>
    /// Sceneビューで射程確認用 Gizmo
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (pointerCamera == null)
            return;

        Ray ray = pointerCamera.ScreenPointToRay(
            new Vector3(
                Screen.width * 0.5f,
                Screen.height * 0.5f
            )
        );

        Gizmos.color = Color.yellow;

        Gizmos.DrawRay(ray.origin, ray.direction * maxRange);
    }

#endif
}