using System.Collections;
using UnityEngine;

/// <summary>
/// マリオの土管のような、2地点間を繋ぐワープ装置。
/// このオブジェクトのトリガーにプレイヤーが入ると、
/// connectedPipe（接続先）の出口位置・向きへ瞬間移動させる。
/// </summary>
public class PipeWarp : MonoBehaviour
{
    [Header("接続設定")]

    // ワープ先となる土管。お互いを参照し合う形で1組のペアを構成する。
    [CustomLabel("接続先の土管"), SerializeField]
    private PipeWarp connectedPipe;

    // 出口でどれだけ前方にずらして出すか。
    // 0だと土管の中心に出てしまい、即座にもう一方の入口判定に
    // 触れて連続ワープしてしまう可能性があるため、ある程度離す。
    [CustomLabel("出現位置オフセット（出口の前方距離）"), SerializeField]
    private float exitOffset = 1.5f;

    [Header("ワープ設定")]

    // ワープ直後、プレイヤーの操作を受け付けなくする時間。
    // 着地演出や向き調整の間に暴れて変な方向へ飛び出すのを防ぐ。
    [CustomLabel("ワープ後の入力ロック時間（秒）"), SerializeField]
    private float lockDuration = 0.5f;

    // trueの場合、ワープ直後に速度を0にリセットする。
    // 入った時の勢いをそのまま出口側に持ち越したくない場合に使う。
    [CustomLabel("ワープ後の速度をリセットする"), SerializeField]
    private bool resetVelocityOnExit = true;

    // ─────────────────────────────────────────

    // 直前にワープしてきたばかりかどうかを示すフラグ。
    // 出口側のこのフラグがtrueの間は、出口に出てきたプレイヤーが
    // 自分自身の入口判定に触れても再ワープしないようにする
    // （= 入った瞬間に押し戻されて無限ループするのを防ぐ）。
    private bool _isCoolingDown = false;

    // ─────────────────────────────────────────

    /// <summary>
    /// 自分（入口側）のトリガーにプレイヤーが侵入した時に呼ばれる。
    /// ここから実際のワープ処理を開始する。
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // クールダウン中（直前にここへワープしてきた直後）は無視する
        if (_isCoolingDown) return;

        // プレイヤー以外のオブジェクトは無視する
        if (!other.CompareTag("Player")) return;

        // 接続先が設定されていなければワープできないので警告して終了
        if (connectedPipe == null)
        {
           return;
        }

        // プレイヤー本体、または親オブジェクトからRigidbodyを取得
        // （コライダーが子オブジェクトに付いているケースを考慮）
        Rigidbody playerRb = other.GetComponent<Rigidbody>()
                          ?? other.GetComponentInParent<Rigidbody>();
        if (playerRb == null) return;

        WarpPlayer(playerRb);
    }

    // ─────────────────────────────────────────
    // ワープ処理
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤーを接続先の出口位置へ転送するメイン処理。
    /// </summary>
    private void WarpPlayer(Rigidbody playerRb)
    {
        // ワープ中に物理挙動や入力でおかしな移動をしないよう
        // 一時的にプレイヤー操作スクリプトを無効化する
        PlayerController ctrl = playerRb.GetComponent<PlayerController>();
        if (ctrl != null) ctrl.enabled = false;

        // 接続先の位置・向きを基準に、出口前方の座標を計算する
        Transform exitPoint = connectedPipe.transform;
        Vector3 exitPos = exitPoint.position + exitPoint.forward * exitOffset;

        SE.PipeWarp.Play();

        // Rigidbody.position / rotation を直接書き換えることで
        // 物理演算を介さず瞬間移動させる
        playerRb.position = exitPos;
        playerRb.rotation = exitPoint.rotation;

        // 設定に応じて入った時の慣性（速度）を引き継がせない
        if (resetVelocityOnExit)
            playerRb.linearVelocity = Vector3.zero;

        // 出口側（connectedPipe）に対してクールダウンを仕掛ける。
        // これにより、出てきた瞬間に出口側の入口判定へ触れても
        // 即座に逆方向へ再ワープしてしまうことを防ぐ。
        connectedPipe.StartCoroutine(connectedPipe.CooldownCoroutine());

        // lockDuration経過後に操作を再度有効化するコルーチンを開始
        StartCoroutine(ReenableControlCoroutine(ctrl));
    }

    /// <summary>
    /// ワープ直後、自分自身（出口側）に再度触れて
    /// 即座に逆ワープしてしまうのを防ぐためのクールダウン処理。
    /// lockDuration秒間だけ入口判定を無効化する。
    /// </summary>
    public IEnumerator CooldownCoroutine()
    {
        _isCoolingDown = true;
        yield return new WaitForSeconds(lockDuration);
        _isCoolingDown = false;
    }

    /// <summary>
    /// lockDuration秒待ってからプレイヤー操作スクリプトを再有効化する。
    /// </summary>
    private IEnumerator ReenableControlCoroutine(PlayerController ctrl)
    {
        yield return new WaitForSeconds(lockDuration);
        if (ctrl != null) ctrl.enabled = true;
    }

#if UNITY_EDITOR
    /// <summary>
    /// エディタ上でこのオブジェクトを選択した時に、
    /// 接続関係・出口位置・出口方向をSceneビューに可視化する。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 自分（入口）の位置を緑の球で表示
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.6f);
        Gizmos.DrawSphere(transform.position, 0.4f);

        if (connectedPipe != null)
        {
            // 自分と接続先を結ぶ線
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.8f);
            Gizmos.DrawLine(transform.position, connectedPipe.transform.position);

            // 実際にプレイヤーが出現する座標（出口位置・向き）を黄色の球で表示
            Vector3 exitPos = connectedPipe.transform.position
                             + connectedPipe.transform.forward * exitOffset;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(exitPos, 0.3f);
            Gizmos.DrawLine(connectedPipe.transform.position, exitPos);
        }

        // 自分自身の出口方向（forward）を矢印（水色の線）で表示
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * exitOffset);
    }
#endif
}