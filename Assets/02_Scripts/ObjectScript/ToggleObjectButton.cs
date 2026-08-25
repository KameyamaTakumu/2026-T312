using UnityEngine;

/// <summary>
/// UIボタンから呼び出して、指定したオブジェクトを
/// 「開く（表示） / 閉じる（非表示）」する汎用クラス
/// 
/// ・任意のGameObjectをInspectorで登録
/// ・ボタンのOnClickから Toggle() を呼ぶだけ
/// ・UIパネル、扉、メニューなど何でも対応可能
/// </summary>
public class ToggleObjectButton : MonoBehaviour
{
    [Header("開閉対象オブジェクト")]
    [SerializeField] private GameObject targetObject;

    /// <summary>
    /// ボタンから呼び出す関数
    /// 現在の状態を反転する
    /// </summary>
    public void Toggle()
    {
        if (targetObject == null)
        {
            return;
        }

        bool isActive = targetObject.activeSelf;
        targetObject.SetActive(!isActive);
    }

    /// <summary>
    /// 強制的に開く
    /// </summary>
    public void Open()
    {
        if (targetObject != null)
            targetObject.SetActive(true);
    }

    /// <summary>
    /// 強制的に閉じる
    /// </summary>
    public void Close()
    {
        if (targetObject != null)
            targetObject.SetActive(false);
    }
}
