using UnityEngine;

/// <summary>
/// 重力源コンポーネント
/// 
/// 惑星などに付与し、GravityBody に対して
/// 独自重力を発生させる
/// </summary>
public class GravityAttractor : MonoBehaviour
{
    // マイナス値にすることで中心方向へ引っ張る
    [CustomLabel("重力の強さ"), SerializeField]
    private float gravity = -9.81f;

    /// <summary>
    /// Rigidbody に重力適用
    /// </summary>
    public void Attract(Rigidbody body)
    {
        // 惑星中心 → オブジェクト方向
        // normalized により長さ1の方向ベクトル化
        Vector3 gravityUp =
            (body.position - transform.position)
            .normalized;

        // ─────────────────────────────────
        // 地面法線へ回転
        // ─────────────────────────────────

        // transform.up を惑星法線方向へ合わせる
        // キャラクターが常に地面に立つようになる
        body.rotation =
            Quaternion.FromToRotation(
                body.transform.up,
                gravityUp
            ) * body.rotation;

        // ─────────────────────────────────
        // 重力適用
        // ─────────────────────────────────

        // gravity が負なので中心方向へ引っ張られる
        body.AddForce(gravityUp * gravity);
    }
}