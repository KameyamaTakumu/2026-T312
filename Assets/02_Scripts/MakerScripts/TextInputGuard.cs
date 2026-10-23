using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// テキスト入力欄を編集中かどうか。
/// 編集中は、WASD・Q/E・R/F などのキー操作を無効にするために使う
/// </summary>
public static class TextInputGuard
{
    public static bool IsTyping
    {
        get
        {
            EventSystem es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null)
                return false;

            var field = es.currentSelectedGameObject.GetComponent<TMP_InputField>();
            return field != null && field.isFocused;
        }
    }
}