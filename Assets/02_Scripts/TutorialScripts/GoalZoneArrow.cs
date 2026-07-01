using UnityEngine;

/// <summary>
/// チュートリアル用のゴール方向矢印
///
/// 主な役割：
/// ・プレイヤーの頭上に追従する
/// ・GoalZone の方向を指す（球面重力対応）
/// ・ボブアニメーション（上下に浮遊）
/// ・GoalZone が非表示になったら自動で破棄
/// </summary>
public class GoalZoneArrow : MonoBehaviour
{
    // ─────────────────────────────────────────
    // Inspector 設定
    // ─────────────────────────────────────────

    [Header("追従設定")]

    // プレイヤーのUp方向からどれだけ離して配置するか
    // 球面重力環境では transform.up がプレイヤーの頭上方向になる
    [CustomLabel("頭上オフセット（プレイヤーのUp方向）"), SerializeField]
    private float headOffset = 2.5f;

    [Header("アニメーション設定")]

    // ボブアニメーションの上下幅
    [CustomLabel("ボブ幅（上下の振れ幅）"), SerializeField]
    private float bobAmplitude = 0.2f;

    // ボブアニメーションの速度
    [CustomLabel("ボブ速度"), SerializeField]
    private float bobSpeed = 2.5f;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    // 追従するプレイヤーの Transform
    private Transform _playerTransform;

    // 目指す GoalZone の Transform
    private Transform _goalZoneTransform;

    // ボブアニメーション用タイマー
    private float _bobTime;

    // ─────────────────────────────────────────
    // 初期化
    // ─────────────────────────────────────────

    /// <summary>
    /// TutorialGoalZone から頭上オフセットを上書きするメソッド
    /// Inspector の値より GoalZone 側の設定を優先したい場合に使う
    /// </summary>
    public void SetHeadOffset(float offset)
    {
        headOffset = offset;
    }

    /// <summary>
    /// TutorialGoalZone から呼ばれる初期化メソッド
    /// プレイヤーと GoalZone の参照をセットしてから動き始める
    /// </summary>
    public void Initialize(Transform playerTransform, Transform goalZoneTransform)
    {
        _playerTransform = playerTransform;
        _goalZoneTransform = goalZoneTransform;
    }

    // ─────────────────────────────────────────
    // 毎フレーム処理
    // ─────────────────────────────────────────

    private void LateUpdate()
    {
        // 参照が切れたら（プレイヤーか GoalZone が消えたら）自身も破棄
        if (_playerTransform == null || _goalZoneTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        UpdatePosition();
        UpdateRotation();
    }

    // ─────────────────────────────────────────
    // 位置更新
    // ─────────────────────────────────────────

    /// <summary>
    /// プレイヤーの頭上にボブしながら追従する
    ///
    /// 球面重力環境では _playerTransform.up が
    /// プレイヤーにとっての「真上」になるため、
    /// それを使うことでどの惑星でも正しく頭上に出る
    /// </summary>
    private void UpdatePosition()
    {
        _bobTime += Time.deltaTime * bobSpeed;

        // sin でオフセットに微小な上下を加える
        float bobOffset = Mathf.Sin(_bobTime) * bobAmplitude;

        // プレイヤーのUp方向（= 惑星からの法線方向）に沿って頭上へ配置
        Vector3 upDir = _playerTransform.up;
        transform.position = _playerTransform.position
                             + upDir * (headOffset + bobOffset);
    }

    // ─────────────────────────────────────────
    // 回転更新
    // ─────────────────────────────────────────

    /// <summary>
    /// GoalZone の方向を指すように回転する
    ///
    /// 「円錐の頂点がゴール方向を指す」ように回転させる。
    /// 球面重力対応のため LookAt は使わず、
    /// プレイヤーのUp を軸にしたローカル平面上で方向を計算する。
    /// </summary>
    private void UpdateRotation()
    {
        Vector3 toGoal = _goalZoneTransform.position - transform.position;

        // ゴール方向がほぼ0のとき（GoalZone と矢印が重なった場合）はスキップ
        if (toGoal.sqrMagnitude < 0.001f) return;

        // 円錐の「先端」を toGoal 方向に向ける
        // ただし円錐の軸方向が Unity のデフォルト（Y上向き）なので、
        // fromDirection として Vector3.up を使い toGoal へ回転させる
        Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, toGoal.normalized);
        transform.rotation = targetRotation;
    }
}