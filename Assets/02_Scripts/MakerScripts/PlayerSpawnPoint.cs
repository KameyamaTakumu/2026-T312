using UnityEngine;

/// <summary>
/// プレイヤーのスタート地点の目印。
/// エディットでは見えるが、プレイ開始時には見た目と当たり判定を消す
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    public void HideForPlay()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        // エディットで右クリック削除できるようコライダーを付けておくが、
        // プレイ中はプレイヤーの邪魔になるので無効にする
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;
    }
}