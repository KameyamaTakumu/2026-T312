using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// ボタンと再生するSEの組み合わせを定義するクラス
public class ButtonSEBinder : MonoBehaviour
{
    [Header("ボタンと再生するSEの組み合わせ")]
    [SerializeField] private List<ButtonSEPair> buttonSEList = new List<ButtonSEPair>();

    private void Start()
    {
        foreach (var pair in buttonSEList)
        {
            if (pair.button != null)
            {
                pair.button.onClick.AddListener(() => PlaySE(pair.seType, pair.button.name));
            }
        }
    }

    private void PlaySE(SE seType, string buttonName)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(seType);
            Debug.Log($"[ButtonSEBinder] '{buttonName}' ボタンで {seType} を再生しました。");
        }
        else
        {
            Debug.LogWarning("[ButtonSEBinder] SoundManagerが見つかりません。");
        }
    }
}

[System.Serializable]
public struct ButtonSEPair
{
    public Button button;
    public SE seType;
}
