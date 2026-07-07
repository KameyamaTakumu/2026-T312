using UnityEngine;
using System.Collections;

/// <summary>
/// 惑星間飛行マネージャー
/// 
/// 主な役割：
/// ・プレイヤーを別惑星へ移動させる
/// ・飛行演出（放物線移動）
/// ・飛行中の操作停止
/// ・着地時の復帰処理
/// ・UI演出連携
/// </summary>
public class PlanetWarpManager : MonoBehaviour
{
    // シングルトンインスタンス
    public static PlanetWarpManager Instance { get; private set; }

    [Header("フェード連携")]

    // 飛行時の画面フェード演出
    [CustomLabel("飛行UI"), SerializeField]
    private PlanetWarpUI travelUI;

    [Header("着地時の挙動")]

    // 着地時に地面方向へ与える速度
    // 0ならその場で静止着地
    [CustomLabel("着地時の衝撃速度"), SerializeField]
    private float landingImpactSpeed = 0f;

    // 着地直後に少し操作不能にする時間
    // 「ズシン」と着地したような演出に使う
    [CustomLabel("着地後の無敵時間（秒）"), SerializeField]
    private float landingInvincibleDuration = 0.5f;

    // 現在飛行中かどうか
    // 外部から参照可能
    public bool IsTraveling { get; private set; }

    private void Awake()
    {
        // シングルトン設定
        // 既に存在する場合は重複破棄
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 惑星間飛行開始
    /// </summary>
    /// <param name="playerRb">プレイヤーの Rigidbody</param>
    /// <param name="landingPos">着地点のワールド座標</param>
    /// <param name="targetPlanet">移動先惑星</param>
    /// <param name="duration">飛行時間</param>
    /// <param name="arcHeight">放物線の高さ</param>
    public void StartTravel(
        Rigidbody playerRb,
        Vector3 landingPos,
        Transform targetPlanet,
        float duration,
        float arcHeight)
    {
        // 多重飛行防止
        if (IsTraveling)
            return;

        StartCoroutine(
            TravelCoroutine(
                playerRb,
                landingPos,
                targetPlanet,
                duration,
                arcHeight
            )
        );
    }

    /// <summary>
    /// 惑星間飛行本体
    /// Coroutine によって時間経過演出を行う
    /// </summary>
    private IEnumerator TravelCoroutine(
        Rigidbody playerRb,
        Vector3 landingPos,
        Transform targetPlanet,
        float duration,
        float arcHeight)
    {
        IsTraveling = true;

        // プレイヤー制御取得
        PlayerController playerCtrl =
            playerRb.GetComponent<PlayerController>();

        // 重力制御取得
        GravityBody gravityBody =
            playerRb.GetComponent<GravityBody>();

        // ─────────────────────────────────────
        // 飛行準備
        // ─────────────────────────────────────

        // プレイヤー操作停止
        if (playerCtrl != null)
            playerCtrl.enabled = false;

        // 独自重力停止
        if (gravityBody != null)
            gravityBody.enabled = false;

        // Unity標準重力も停止
        playerRb.useGravity = false;

        // 現在速度を完全停止
        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;

        // UI演出開始
        if (travelUI != null)
            travelUI.PlayLaunchEffect();

        // 出発位置保存
        Vector3 startPos = playerRb.position;

        float elapsed = 0f;

        // ─────────────────────────────────────
        // 飛行ループ
        // ─────────────────────────────────────

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;

            // 0〜1 の補間値
            float t = Mathf.Clamp01(elapsed / duration);

            // 線形補間
            // startPos → landingPos へ直線移動
            Vector3 linearPos =
                Vector3.Lerp(startPos, landingPos, t);

            // 放物線高さ
            // Sin を使うことで中央が最も高くなる
            float arc =
                Mathf.Sin(Mathf.PI * t) * arcHeight;

            // 放物線方向
            // 出発惑星法線 + 到着惑星法線の中間方向
            Vector3 arcDir =
                (
                    (startPos - GetPlanetCenter(startPos))
                    + (landingPos - targetPlanet.position)
                ).normalized;

            // 最終飛行位置
            Vector3 finalPos =
                linearPos + arcDir * arc;

            // Rigidbody移動
            playerRb.MovePosition(finalPos);

            // ─────────────────────────────────
            // プレイヤー回転
            // ─────────────────────────────────

            // 現在フレーム移動方向
            Vector3 velocity =
                finalPos - playerRb.position;

            // ほぼ停止時は回転しない
            if (velocity.sqrMagnitude > 0.001f)
            {
                // 到着惑星の法線方向
                Vector3 up =
                    (playerRb.position - targetPlanet.position)
                    .normalized;

                // 移動方向へ向ける
                playerRb.MoveRotation(
                    Quaternion.LookRotation(
                        velocity.normalized,
                        up
                    )
                );
            }

            yield return new WaitForFixedUpdate();
        }

        // ─────────────────────────────────────
        // 着地処理
        // ─────────────────────────────────────

        // 最終位置を強制補正
        playerRb.MovePosition(landingPos);

        // 惑星表面法線
        Vector3 landingUp =
            (landingPos - targetPlanet.position).normalized;

        // 惑星表面に沿うよう回転
        playerRb.MoveRotation(
            Quaternion.LookRotation(
                Vector3.ProjectOnPlane(
                    playerRb.transform.forward,
                    landingUp
                ).normalized,
                landingUp
            )
        );

        // 重力再開
        if (gravityBody != null)
            gravityBody.enabled = true;

        // GravityBody が独自重力を使うため
        // Unity標準重力はOFFのまま
        playerRb.useGravity = false;

        // 着地衝撃
        // 法線逆方向へ少し押し込む
        playerRb.linearVelocity =
            landingUp * (-landingImpactSpeed);

        // 少し待機してから操作復帰
        yield return new WaitForSeconds(
            landingInvincibleDuration
        );

        // プレイヤー操作再開
        if (playerCtrl != null)
            playerCtrl.enabled = true;

        // 着地演出
        if (travelUI != null)
            travelUI.PlayLandEffect();

        PlayPlanetBGM(targetPlanet);

        IsTraveling = false;
    }

    /// <summary>
    /// 指定した惑星のBGMを再生
    /// </summary>
    public void PlayPlanetBGM(Transform planet)
    {
        if (planet == null)
            return;

        GravityAttractor attractor =
            planet.GetComponent<GravityAttractor>();

        if (attractor != null)
        {
            attractor.PlayPlanetBGM();
        }
    }

    /// <summary>
    /// 現在位置から最寄り惑星中心を取得
    /// GravityAttractor を検索して最短距離を返す
    /// </summary>
    private Vector3 GetPlanetCenter(Vector3 fromPosition)
    {
        GravityAttractor[] attractors =
            FindObjectsByType<GravityAttractor>(
                FindObjectsSortMode.None
            );

        float minDist = float.MaxValue;

        // デフォルトは原点
        Vector3 center = Vector3.zero;

        foreach (var a in attractors)
        {
            float d =
                Vector3.Distance(
                    fromPosition,
                    a.transform.position
                );

            // 最短更新
            if (d < minDist)
            {
                minDist = d;
                center = a.transform.position;
            }
        }

        return center;
    }
}