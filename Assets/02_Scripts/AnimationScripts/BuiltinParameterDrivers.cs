using System.Collections.Generic;
using UnityEngine;

// ══════════════════════════════════════════════════════════════════════
//  BuiltinParameterDrivers.cs  ─  標準ドライバー実装集
//
//  新しい動きが必要になったら…
//    1. AnimatorParameterDriver を継承したクラスをここに追加
//    2. [System.Serializable] を付ける
//    3. Drive(DriveContext ctx) を実装する
//    4. PlayerAnimatorConfigEditor.cs の DriverTypes に 1 行追加する
//
//  基底クラスのヘルパーを使うと短く書けます：
//    HorizontalVelocity(ctx)        → 水平速度ベクトル
//    VerticalSpeed(ctx)             → 上下速度（float）
//    SetFloatSmooth(ctx, target)    → Float をスムーズにセット
//    SetBool(ctx, value)            → Bool をセット
//    SetTriggerOnRise(ctx, active)  → false→true の瞬間だけ Trigger を送る
// ══════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────
// Horizontal Speed Driver
// ─────────────────────────────────────────────────────

/// <summary>
/// 水平移動速度を Animator の Float パラメータへ送るドライバー。
///
/// 主な用途：
/// ・Idle → Walk → Run の BlendTree 制御
/// ・移動速度に応じたアニメーションブレンド
///
/// プレイヤーの水平速度を取得し、
/// 0～1 の範囲に正規化して Animator へ渡す。
/// </summary>
[System.Serializable]
public class HorizontalSpeedDriver : AnimatorParameterDriver
{
    [Tooltip("この速度に到達すると Animator へ 1.0 を送る")]
    public float MaxSpeed = 5f;

    [Tooltip("この速度未満は停止扱いにする")]
    public float DeadZone = 0.1f;

    /// <summary>
    /// 毎フレーム呼ばれる更新処理。
    ///
    /// 水平速度を取得し、
    /// Animator の Float パラメータへ反映する。
    /// </summary>
    public override void Drive(DriveContext ctx)
    {
        // プレイヤーの水平速度取得
        float speed = HorizontalVelocity(ctx).magnitude;

        // DeadZone 以下なら停止扱い
        float target =
            speed > DeadZone
            ? Mathf.Clamp01(speed / MaxSpeed)
            : 0f;

        // 補間しながら Animator に反映
        SetFloatSmooth(ctx, target);
    }
}


// ─────────────────────────────────────────────────────
// Air State Driver
// ─────────────────────────────────────────────────────

/// <summary>
/// プレイヤーが空中にいるかを判定して
/// Animator の Bool パラメータへ送るドライバー。
///
/// 主な用途：
/// ・Jump アニメーション
/// ・Fall アニメーション
/// ・Landing 遷移
///
/// 上昇・落下速度と接地判定を組み合わせて
/// 安定した空中判定を行う。
/// </summary>
[System.Serializable]
public class AirStateDriver : AnimatorParameterDriver
{
    [Tooltip("この速度以上で上昇中と判定する")]
    public float RiseThreshold = 0.5f;

    [Tooltip("この速度以下で落下中と判定する")]
    public float FallThreshold = -1.5f;

    [Tooltip("接地判定に使用するレイヤー")]
    public LayerMask GroundLayer = ~0;

    [Tooltip("地面との判定距離")]
    public float GroundCheckDistance = 0.15f;

    /// <summary>
    /// 一度空中になったら
    /// 接地するまで維持するフラグ。
    ///
    /// 小さな段差や地形による
    /// 判定ブレを防ぐために使用。
    /// </summary>
    private bool _latchedAir = false;

    /// <summary>
    /// 空中判定更新処理。
    /// </summary>
    public override void Drive(DriveContext ctx)
    {
        // 上下速度取得
        float v = VerticalSpeed(ctx);

        // 上昇または落下しているか
        bool velocityAir =
            v > RiseThreshold ||
            v < FallThreshold;

        // 空中になったらラッチON
        if (velocityAir)
            _latchedAir = true;

        // 地面に着いたらラッチ解除
        if (_latchedAir && IsGrounded(ctx))
            _latchedAir = false;

        // Animator に反映
        SetBool(ctx, _latchedAir || velocityAir);
    }

    /// <summary>
    /// Raycast による接地判定。
    /// </summary>
    bool IsGrounded(DriveContext ctx)
    {
        return Physics.Raycast(
            ctx.Tf.position + ctx.Tf.up * 0.05f,
            -ctx.Tf.up,
            GroundCheckDistance,
            GroundLayer
        );
    }
}


// ── 他コンポーネントの Bool プロパティ → Trigger ─────────────────────

/// <summary>
/// 他コンポーネントの bool プロパティを監視し、
/// false → true になった瞬間だけ
/// Trigger パラメータを送るドライバー。
///
/// 主な用途：
/// ・スピン開始
/// ・攻撃開始
/// ・特殊アクション開始
///
/// リフレクションを利用して
/// 任意コンポーネントを監視できる。
/// </summary>
[System.Serializable]
public class ComponentBoolTriggerDriver : AnimatorParameterDriver
{
    /// <summary>
    /// 監視対象コンポーネント
    /// </summary>
    public string ComponentName = "PlayerSpin";

    /// <summary>
    /// 監視対象の bool プロパティ名
    /// </summary>
    public string PropertyName = "IsSpinning";

    // リフレクションキャッシュ（毎フレーム検索しないよう初回のみ取得）
    private System.Reflection.PropertyInfo _cachedProp;
    private Component _cachedComp;
    private bool _initialized;

    public override void Drive(DriveContext ctx)
    {
        if (!_initialized)
        {
            _initialized = true;
            _cachedComp = ctx.Anim.GetComponent(ComponentName);
            if (_cachedComp != null)
                _cachedProp = _cachedComp.GetType().GetProperty(PropertyName);
        }

        if (_cachedComp == null || _cachedProp == null) return;

        bool nowActive = (bool)_cachedProp.GetValue(_cachedComp);
        SetTriggerOnRise(ctx, nowActive);
    }
}


// ── 速度閾値 Bool（ダッシュ判定など） ───────────────────────────────

/// <summary>
/// 水平速度が一定値を超えているかを
/// Animator の Bool パラメータへ送るドライバー。
///
/// 主な用途：
/// ・ダッシュ判定
/// ・高速移動アニメーション
/// ・ブースト状態判定
/// </summary>
[System.Serializable]
public class SpeedThresholdBoolDriver : AnimatorParameterDriver
{
    /// <summary>
    /// この速度を超えると true になる。
    /// </summary>
    public float Threshold = 8f;

    public override void Drive(DriveContext ctx)
    {
        SetBool(ctx, HorizontalVelocity(ctx).magnitude > Threshold);
    }
}


// ── カスタムドライバーのテンプレート ────────────────────────────────
//
//  以下をコピーしてカスタムドライバーを作ってください。
//  その後 PlayerAnimatorConfigEditor.cs の DriverTypes に 1 行追加すれば
//  インスペクターの「＋ ドライバーを追加」に表示されます。
//
// [System.Serializable]
// public class MyCustomDriver : AnimatorParameterDriver
// {
//     [Tooltip("ここに設定項目を追加できます")]
//     public float MyValue = 1f;
//
//     public override void Drive(DriveContext ctx)
//     {
//         // 例: 計算した値を Float パラメータにセット
//         SetFloatSmooth(ctx, MyValue);
//
//         // 例: 直接 Animator を操作することもできる
//         // ctx.Anim.SetBool(ParameterName, true);
//     }
// }