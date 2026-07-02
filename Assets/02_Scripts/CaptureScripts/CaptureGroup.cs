using UnityEngine;

/// <summary>
/// 複数のEnemyFleerからの捕獲報告を集計し、
/// 指定数に達したら revealTarget を開放するグループ管理クラス。
///
/// EnemyFleer 1体につき requiredCount = 1 のグループを割り当てれば、
/// 従来の「1体捕獲したら即開放」と同じ挙動になる。
/// </summary>
public class CaptureGroup : MonoBehaviour
{
    [Header("捕獲条件")]

    // このグループを開放するために必要な捕獲数
    [CustomLabel("開放に必要な捕獲数"), SerializeField]
    private int requiredCount = 1;

    [Header("開放時の処理")]

    // 条件達成時に表示・カメラ誘導を行うコントローラー
    [CustomLabel("開放するオブジェクト表示制御"), SerializeField]
    private ObjectVisibilityController revealTarget;

    // 現在の捕獲数
    private int currentCount = 0;

    // 二重発火防止フラグ
    private bool isRevealed = false;

    /// <summary>
    /// EnemyFleer側から捕獲時に呼ばれる。
    /// 必要数に達した時点で1回だけ revealTarget.Show() を実行する。
    /// </summary>
    public void NotifyCaught()
    {
        if (isRevealed)
            return;

        currentCount++;

        if (currentCount >= requiredCount)
        {
            isRevealed = true;

            if (revealTarget != null)
                revealTarget.Show();
        }
    }

    // 現在の進捗をUI表示等に使いたい場合に使用
    public int CurrentCount => currentCount;
    public int RequiredCount => requiredCount;
}