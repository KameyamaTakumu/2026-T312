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
}