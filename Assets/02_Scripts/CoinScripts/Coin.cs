using System.Collections;
using UnityEngine;

/// <summary>
/// コインコンポーネント
/// プレイヤーが回収できるコインの挙動を管理する
///
/// 主な役割：
/// ・自転演出
/// ・プレイヤーへの引き寄せ
/// ・回収判定
/// ・CoinManager への加算通知
/// ・[ドロップ時] はね演出・点滅・時間切れ消滅
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Coin : MonoBehaviour
{
    // ─────────────────────────────────────────
    // インスペクター設定
    // ─────────────────────────────────────────

    [Header("コイン設定")]

    /// <summary>このコインを回収したときに加算されるコイン枚数</summary>
    [CustomLabel("コイン価値"), SerializeField]
    private int value = 1;

    /// <summary>
    /// Attracting 状態（引き寄せ中）でプレイヤーへ向かう移動速度（Units/s）
    /// 大きいほど素早く吸い込まれる
    /// </summary>
    [CustomLabel("ホバー引き寄せ速度"), SerializeField]
    private float attractSpeed = 12f;

    /// <summary>
    /// プレイヤーとのこの距離以内に入ったとき Collect() を呼んで回収する（Units）
    /// StartAttracting → FixedUpdate_Attracting 内で毎フレーム判定する
    /// </summary>
    [CustomLabel("回収判定距離（プレイヤーとの）"), SerializeField]
    private float collectDistance = 0.8f;

    /// <summary>毎秒の自転量（度）。Update で transform.Rotate に渡す</summary>
    [CustomLabel("自転速度（deg/s）"), SerializeField]
    private float spinSpeed = 180f;

    // ─────────────────────────────────────────
    // ドロップコイン設定
    // SpinBreakable から生成された場合のみ有効
    // ─────────────────────────────────────────

    [Header("ドロップコイン設定（SpinBreakableドロップ時のみ）")]

    /// <summary>ドロップコインが生成されてから自動消滅するまでの時間（秒）</summary>
    [CustomLabel("消滅時間（秒）"), SerializeField]
    private float dropLifetime = 8f;

    /// <summary>消滅の何秒前から点滅演出を開始するか</summary>
    [CustomLabel("点滅開始（消滅の何秒前から）"), SerializeField]
    private float blinkStartBefore = 3f;

    /// <summary>点滅の表示・非表示を切り替える間隔（秒）</summary>
    [CustomLabel("点滅間隔（秒）"), SerializeField]
    private float blinkInterval = 0.15f;

    /// <summary>
    /// ドロップ直後に planetUp 方向へ与える初速（m/s）
    /// InitAsDropCoin() に渡された bounceDir を上方向として使用する
    /// </summary>
    [CustomLabel("ドロップ時はね初速（m/s）"), SerializeField]
    private float dropBounceSpeed = 6f;

    /// <summary>
    /// バウンス時に下向き速度成分を何倍に減衰させるか（0〜1）
    /// 0.5 なら地面に当たるたびに上方向速度が半分になる
    /// </summary>
    [CustomLabel("バウンス減衰率"), SerializeField]
    private float bounceDecay = 0.5f;

    /// <summary>
    /// この速度（m/s）を下回ったらバウンスを停止して地面上で静止させる
    /// 微小なバウンスが永久に続くのを防ぐための閾値
    /// </summary>
    [CustomLabel("バウンス最低速度（m/s）"), SerializeField]
    private float minBounceSpeed = 1.5f;

    /// <summary>
    /// 地面接近を検出するレイキャストの長さ（Units）
    /// コインの見た目半径より少し大きい値に設定する
    /// </summary>
    [CustomLabel("地面判定距離（Units）"), SerializeField]
    private float groundCheckDist = 0.4f;

    // ─────────────────────────────────────────
    // 公開プロパティ
    // ─────────────────────────────────────────

    /// <summary>コインの現在の振る舞い状態</summary>
    public enum CoinState
    {
        /// <summary>
        /// 待機中
        /// 重力の影響を受けて床に静止している（または落下中）
        /// </summary>
        Idle,
        /// <summary>プレイヤーに向かって飛んでいる最中</summary>
        Attracting,
        /// <summary>回収済み。このフレーム以降は何もしない</summary>
        Collected
    }

    /// <summary>現在の状態。外部からは読み取り専用</summary>
    public CoinState State { get; private set; } = CoinState.Idle;

    // ─────────────────────────────────────────
    // 内部フィールド
    // ─────────────────────────────────────────

    /// <summary>引き寄せ先のプレイヤー Transform。Attracting 中のみ有効</summary>
    private Transform playerTransform;

    private Rigidbody rb;
    private GravityBody gravityBody;

    /// <summary>このコインが DropCoin として初期化されたかどうか</summary>
    private bool isDropCoin = false;

    /// <summary>
    /// バウンス判定を有効化するフラグ
    /// </summary>
    private bool bounceEnabled = false;

    /// <summary>
    /// 最寄り惑星の「上」方向（惑星中心→コイン）
    /// UpdatePlanetUp() で毎 FixedUpdate 更新される
    /// バウンス計算・ドロップ初速の基準方向として使用
    /// </summary>
    private Vector3 planetUp = Vector3.up;

    /// <summary>
    /// 最寄り惑星の Transform
    /// </summary>
    private Transform nearestPlanetTransform;

    /// <summary>
    /// FixedUpdate で1回だけ初速を与えるためのフラグ
    /// InitAsDropCoin() → true にセットし、FixedUpdate で消費する
    /// </summary>
    private bool hasPendingBounce = false;

    /// <summary>hasPendingBounce が true のときに AddForce する方向</summary>
    private Vector3 pendingBounceDir = Vector3.zero;

    /// <summary>点滅演出で表示・非表示を切り替えるレンダラー一覧</summary>
    private Renderer[] renderers;

    // ─────────────────────────────────────────
    // Unity ライフサイクル
    // ─────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        gravityBody = GetComponent<GravityBody>();
        // 子オブジェクトを含む全 Renderer を取得（メッシュが分割されている場合でも対応）
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void Update()
    {
        // コインを自転させる（演出のみ、physics と無関係なので Update で回す）
        transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);
    }

    private void FixedUpdate()
    {
        // ── 惑星上方向を毎フレーム更新 ────────────────────────────────
        // Attracting 中は GravityBody が無効化されているため自前で計算する
        UpdatePlanetUp();

        // ── ドロップ初速付与（InitAsDropCoin 呼び出し後の最初の 1 フレームのみ）─
        // FixedUpdate で実行することで物理演算タイミングに合わせて確実に力を与える
        if (hasPendingBounce)
        {
            hasPendingBounce = false;
            rb.linearVelocity = Vector3.zero;               // 既存速度をリセットして意図しない合算を防ぐ
            rb.AddForce(pendingBounceDir * dropBounceSpeed, ForceMode.VelocityChange);
        }

        // ── バウンス判定（ドロップコインかつ bounceEnabled のときだけ）────────
        if (isDropCoin && bounceEnabled)
            CheckAndBounce();

        // ── プレイヤーへの引き寄せ移動 ─────────────────────────────────
        if (State == CoinState.Attracting)
            FixedUpdate_Attracting();
    }

    // ─────────────────────────────────────────
    // 内部メソッド
    // ─────────────────────────────────────────

    /// <summary>
    /// シーン内の全 GravityAttractor から最寄りを探し、
    /// 「惑星中心 → コイン」の方向を planetUp に保存する。
    /// GravityBody が無効になっている場合でも正しく動作するよう自前で計算している。
    /// </summary>
    private void UpdatePlanetUp()
    {
        GravityAttractor[] attractors =
            Object.FindObjectsByType<GravityAttractor>(FindObjectsSortMode.None);

        if (attractors == null || attractors.Length == 0)
            return;

        float minDist = float.MaxValue;
        GravityAttractor nearest = null;

        foreach (var a in attractors)
        {
            float d = Vector3.Distance(rb.position, a.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = a;
            }
        }

        if (nearest == null) return;

        nearestPlanetTransform = nearest.transform;

        // 惑星中心からコインへ向かうベクトルを正規化 → 「コインから見た上方向」
        planetUp = (rb.position - nearest.transform.position).normalized;
    }

    /// <summary>
    /// レイキャストで地面への接近を検出し、条件を満たしたらバウンスを発生させる。
    /// コインの Collider が IsTrigger である場合や、
    /// フレーム間で貫通が起きる場合に OnCollisionEnter が呼ばれないため、
    /// レイキャストで毎フレーム接地判定を行っている
    /// </summary>
    private void CheckAndBounce()
    {
        // 惑星中心方向（コインにとっての「真下」）へレイを飛ばす
        Vector3 downDir = -planetUp;
        Ray ray = new Ray(rb.position, downDir);

        if (!Physics.Raycast(ray, out RaycastHit hit, groundCheckDist))
            return;     // 地面が近くにない → バウンス不要

        // 自身の Collider にヒットした場合は誤判定なので無視
        if (hit.collider.gameObject == gameObject)
            return;

        // Trigger コライダーは地面とみなさない
        if (hit.collider.isTrigger)
            return;

        // 下方向への速度成分を取得（正の値ほど地面に向かって落下中）
        float downwardSpeed = Vector3.Dot(rb.linearVelocity, downDir);

        // 上昇中（地面から離れている最中）はバウンスしない
        if (downwardSpeed <= 0f)
            return;

        // 速度が閾値以下になったらバウンスを終了して静止させる
        if (downwardSpeed < minBounceSpeed)
        {
            bounceEnabled = false;
            return;
        }

        // ── バウンス計算 ────────────────────────────────────────────────
        // 下向き速度成分を除去することで「地面に潜り込む力」を取り消し、
        // 上方向に減衰した速度を与えることで跳ね返りを再現する。
        // 水平方向の速度（惑星表面に沿った動き）はそのまま維持する。

        // 「現在の速度」から「下方向成分ベクトル」を引いて水平成分だけを残す
        Vector3 horizontalVelocity =
            rb.linearVelocity - Vector3.Dot(rb.linearVelocity, downDir) * downDir;

        rb.linearVelocity =
            horizontalVelocity
            + planetUp * (downwardSpeed * bounceDecay);
        // planetUp 方向への速度 = 落下速度 × 減衰率
    }

    /// <summary>
    /// Attracting 状態のときに毎 FixedUpdate で呼ばれる移動処理
    /// プレイヤーへ向かって直線移動し、回収判定距離に入ったら Collect() を呼ぶ
    /// プレイヤーが突然消えた場合（死亡など）は Idle に戻す
    /// </summary>
    private void FixedUpdate_Attracting()
    {
        if (playerTransform == null)
        {
            SetIdle();
            return;
        }

        Vector3 dir = playerTransform.position - rb.position;
        float dist = dir.magnitude;

        if (dist < collectDistance)
        {
            Collect();  // 回収判定距離以内 → 即座に回収
            return;
        }

        // Rigidbody の速度を直接書き換えてプレイヤーへ向かわせる
        // ForceMode を使わないことで毎フレーム速度を正確に制御できる
        rb.linearVelocity = dir.normalized * attractSpeed;
    }

    // ─────────────────────────────────────────
    // 公開メソッド
    // ─────────────────────────────────────────

    /// <summary>
    /// StarPointer（コインレーダー）などから呼ばれ、引き寄せを開始する
    /// GravityBody を無効化して惑星引力の影響を切り、コインがプレイヤーへ直進できるようにする
    /// </summary>
    /// <param name="playerTransform">引き寄せ先のプレイヤー Transform</param>
    public void StartAttracting(Transform playerTransform)
    {
        if (State == CoinState.Collected) return;

        this.playerTransform = playerTransform;
        State = CoinState.Attracting;
        bounceEnabled = false;  // 引き寄せ中はバウンス不要

        if (gravityBody != null)
            gravityBody.enabled = false;    // 惑星引力を切る

        rb.linearVelocity = Vector3.zero;   // 既存の速度をリセット
    }

    /// <summary>
    /// 引き寄せをキャンセルして待機状態へ戻す
    /// プレイヤーが照準を外した場合などに呼ぶ
    /// GravityBody を再有効化して惑星引力を復活させる
    /// </summary>
    public void SetIdle()
    {
        if (State == CoinState.Collected) return;

        State = CoinState.Idle;
        playerTransform = null;

        if (gravityBody != null)
            gravityBody.enabled = true;     // 惑星引力を復活させる
    }

    /// <summary>
    /// コインを回収する。CoinManager に加算通知を送り、このオブジェクトを破棄する
    /// 二重回収を防ぐため、Collected 状態では何もしない
    /// </summary>
    public void Collect()
    {
        if (State == CoinState.Collected) return;

        State = CoinState.Collected;
        CoinManager.Instance?.AddCoins(value);

        TutorialManager.Instance?.NotifyCoinCollected();  // ★追加
        Destroy(gameObject);
    }

    /// <summary>このコインの価値を返す（インスペクター設定値）</summary>
    public int GetValue() => value;

    /// <summary>
    /// SpinBreakable が破壊されたときに呼ばれ、このコインをドロップコインとして初期化する
    /// 二重呼び出しを防ぐため isDropCoin フラグを確認している
    /// 呼び出し後の最初の FixedUpdate で hasPendingBounce フラグを消費して初速を与える
    /// </summary>
    /// <param name="bounceDir">
    /// コインを弾き飛ばす方向（通常は惑星中心 → コインの方向 = 惑星の上方向）
    /// </param>
    public void InitAsDropCoin(Vector3 bounceDir)
    {
        if (isDropCoin) return;     // 二重初期化を防止
        isDropCoin = true;

        // bounceDir を惑星の上方向としてキャッシュ
        // UpdatePlanetUp が走る前にバウンス計算が必要になる場合に備えて手動設定する
        planetUp = bounceDir.normalized;
        pendingBounceDir = planetUp;
        hasPendingBounce = true;    // 次の FixedUpdate で一度だけ初速を与える
        bounceEnabled = true;       // 以降のフレームでバウンス判定を有効化

        StartCoroutine(DropLifetimeCoroutine());
    }

    // ─────────────────────────────────────────
    // コライダーイベント
    // ─────────────────────────────────────────

    /// <summary>
    /// 通常コイン用（IsTrigger = true のコライダーで機能）
    /// プレイヤーが触れた瞬間に即座に回収する
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (State == CoinState.Collected) return;

        if (other.CompareTag("Player"))
            Collect();
    }

    /// <summary>
    /// ドロップコイン用（IsTrigger = false の物理コライダーで機能）
    /// プレイヤーに接触したときだけ回収する
    /// 地面・壁との衝突はバウンス（CheckAndBounce）で処理するため、ここでは何もしない
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (State == CoinState.Collected) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            Collect();
            return;
        }
    }

    // ─────────────────────────────────────────
    // ドロップコイン演出（コルーチン）
    // ─────────────────────────────────────────

    /// <summary>
    /// ドロップコインの生存期間を管理するコルーチン
    /// dropLifetime 秒後に消滅するが、消滅前 blinkStartBefore 秒間は点滅演出を行う
    /// </summary>
    private IEnumerator DropLifetimeCoroutine()
    {
        // 点滅開始までの待ち時間（点滅演出を引いた残り時間）
        float waitTime = dropLifetime - blinkStartBefore;
        if (waitTime > 0f)
            yield return new WaitForSeconds(waitTime);

        // まだ回収されていなければ点滅開始
        if (State != CoinState.Collected)
            yield return BlinkCoroutine();

        // 点滅終了後もまだ回収されていなければ時間切れ消滅
        if (State != CoinState.Collected)
            Destroy(gameObject);
    }

    /// <summary>
    /// blinkStartBefore 秒間、blinkInterval ごとに表示・非表示を繰り返すコルーチン
    /// 回収されたら途中で抜ける。演出終了時に必ず表示状態に戻す
    /// </summary>
    private IEnumerator BlinkCoroutine()
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < blinkStartBefore)
        {
            if (State == CoinState.Collected)
                yield break;    // 回収済みなら演出を即終了

            elapsed += blinkInterval;
            visible = !visible;                     // 表示フラグをトグル
            SetRenderersEnabled(visible);

            yield return new WaitForSeconds(blinkInterval);
        }

        // 点滅終了後は必ず表示状態に戻す
        SetRenderersEnabled(true);
    }

    /// <summary>
    /// 全 Renderer の enabled を一括で切り替えるヘルパー
    /// </summary>
    private void SetRenderersEnabled(bool visible)
    {
        foreach (var r in renderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }
}