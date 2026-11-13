using System;
using System.Runtime.InteropServices;

/// <summary>
/// WebGL でテキストを入出力するための、ブラウザ標準ダイアログ呼び出し。
/// クリップボード API が使えない環境でも、ダイアログ内のコピー・貼り付けは動く
/// </summary>
public static class WebPrompt
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern IntPtr WebPrompt_Show(string message, string defaultValue);

    [DllImport("__Internal")]
    private static extern void WebPrompt_Free(IntPtr ptr);
#endif

    /// <summary>ダイアログを開く。キャンセルまたは WebGL 以外では null を返す</summary>
    public static string Show(string message, string defaultValue = "")
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        IntPtr ptr = WebPrompt_Show(message, defaultValue);
        if (ptr == IntPtr.Zero) return null;

        string result = Marshal.PtrToStringUTF8(ptr);
        WebPrompt_Free(ptr);
        return result;
#else
        return null;
#endif
    }
}