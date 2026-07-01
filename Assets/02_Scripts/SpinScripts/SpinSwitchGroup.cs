using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 複数の SpinSwitch をまとめて管理するクラス
/// 
/// 主な役割：
/// ・SpinSwitch の登録
/// ・起動状態の管理
/// ・全スイッチ起動判定
/// ・全起動時イベント発火
/// ・リセット処理
/// </summary>
public class SpinSwitchGroup : MonoBehaviour
{
    // ─────────────────────────────────────────
    // インスペクタ設定
    // ─────────────────────────────────────────

    [Header("グループ設定")]

    // true の場合、
    // 全起動後でも ResetAll() が可能
    [CustomLabel("全起動後にリセットを許可する"), SerializeField]
    private bool allowReset = false;

    [Header("全起動時のイベント")]

    // 全スイッチ起動時に呼ばれるイベント
    // インスペクタから自由に設定可能
    [CustomLabel("全スイッチ起動時に呼ぶ処理")]
    public UnityEvent onAllActivated;

    [Header("状態確認（読み取り専用）")]

    // 登録済みスイッチ数
    [CustomLabel("登録済みスイッチ数"), SerializeField]
    private int registeredCount = 0;

    // 現在起動済みのスイッチ数
    [CustomLabel("起動済みスイッチ数"), SerializeField]
    private int activatedCount = 0;

    // ─────────────────────────────────────────
    // 内部状態
    // ─────────────────────────────────────────

    // 管理対象スイッチ一覧
    private readonly List<SpinSwitch> switches = new List<SpinSwitch>();

    // 全起動済みか
    private bool allActivated = false;

    // ─────────────────────────────────────────
    // 公開 API
    // ─────────────────────────────────────────

    /// <summary>
    /// Group にスイッチを登録する
    /// </summary>
    public void RegisterSwitch(SpinSwitch sw)
    {
        // 重複登録防止
        if (switches.Contains(sw)) return;

        switches.Add(sw);

        // インスペクタ表示用
        registeredCount = switches.Count;

        Debug.Log($"{sw.SwitchId}を登録 （計{registeredCount}個）");
    }

    /// <summary>
    /// SpinSwitch が起動したときに呼ばれる
    /// </summary>
    public void NotifyActivated(SpinSwitch sw)
    {
        // 既に全起動済みでリセット不可設定なら無視
        if (allActivated && !allowReset) return;

        // 現在の起動数を数え直す
        int count = 0;

        foreach (SpinSwitch s in switches)
        {
            if (s.IsActivated)
                count++;
        }

        activatedCount = count;

        Debug.Log($"起動済み {activatedCount} / {registeredCount}");

        // 全スイッチ起動判定
        // 0個で即クリアにならないように登録数 > 0 の条件も追加する
        if (activatedCount >= registeredCount
            && registeredCount > 0)
        {
            OnAllSwitchesActivated();
        }
    }

    /// <summary>
    /// 全スイッチをリセットする
    /// </summary>
    public void ResetAll()
    {
        // 全スイッチへリセット通知
        foreach (SpinSwitch sw in switches)
            sw.ResetSwitch();

        // 状態初期化
        activatedCount = 0;
        allActivated = false;

        Debug.Log($"全スイッチをリセットしました");
    }

    // ─────────────────────────────────────────
    // 内部処理
    // ─────────────────────────────────────────

    /// <summary>
    /// 全スイッチ起動時処理
    /// </summary>
    private void OnAllSwitchesActivated()
    {
        allActivated = true;

        Debug.Log($"全スイッチが起動しました" );

        // UnityEvent 発火
        onAllActivated?.Invoke();
    }
}