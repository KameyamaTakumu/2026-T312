using UnityEngine;

/// <summary>
/// リスポーン地点管理。
///
/// 惑星ごとのリスポーン地点をシーンをまたいで保持する。
/// SoundManager等と同じDontDestroyOnLoadシングルトン。
/// </summary>
public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    // 現在有効なリスポーン地点（座標のみ保持。Transform参照はシーンリロードで消えるため不可）
    public Vector3 RespawnPosition { get; private set; }
    public Quaternion RespawnRotation { get; private set; } = Quaternion.identity;

    // まだ一度もリスポーン地点が設定されていないか
    public bool HasRespawnPoint { get; private set; }

    // ─────────────────────────────────────────
    // 復帰データ（チュートリアルなど別シーンから戻る用）
    // ─────────────────────────────────────────

    /// <summary>チュートリアルへ移動する時点のゲーム状態</summary>
    public class ResumeSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public string sceneName;      // 保存した時のシーン（別シーンでの誤復元防止）
        public int coinCount;
        public System.Collections.Generic.List<string> shownIds = new System.Collections.Generic.List<string>();
    }

    private ResumeSnapshot resumeSnapshot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// リスポーン地点を更新する。pointがnull（その惑星に未設定）の場合は何もしない＝現状維持。
    /// </summary>
    public void SetRespawnPoint(Transform point)
    {
        if (point == null)
            return;

        RespawnPosition = point.position;
        RespawnRotation = point.rotation;
        HasRespawnPoint = true;
    }

    /// <summary>復帰データを保存する（チュートリアルへ移動する直前に呼ぶ）</summary>
    public void SaveResume(ResumeSnapshot snapshot)
    {
        resumeSnapshot = snapshot;
    }

    /// <summary>復帰データを削除する（タイトルへ戻る時・リスポーン時に呼ぶ）</summary>
    public void ClearResumePoint()
    {
        resumeSnapshot = null;
    }

    /// <summary>
    /// 現在のシーンに対応する復帰データがあれば取り出す。
    /// 取り出した時点で削除するので、復元は1回だけ行われる。
    /// </summary>
    public bool TryConsumeResume(string currentSceneName, out ResumeSnapshot snapshot)
    {
        snapshot = resumeSnapshot;

        if (snapshot == null || snapshot.sceneName != currentSceneName)
        {
            snapshot = null;
            return false;
        }

        resumeSnapshot = null;
        return true;
    }

    /// <summary>
    /// リスポーン地点の設定を解除する
    /// （ステージメーカーのスタート地点が、通常のゲームに残らないようにするため）
    /// </summary>
    public void ClearRespawnPoint()
    {
        HasRespawnPoint = false;
    }
}