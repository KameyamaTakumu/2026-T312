using UnityEngine;

public class TitleManager : MonoBehaviour
{
    void Start()
    {
        // タイトルは「最初から始める」場所なので、前回のリスポーン地点と復帰データを消す
        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.ClearRespawnPoint();
            RespawnManager.Instance.ClearResumePoint();
        }
    }
}
