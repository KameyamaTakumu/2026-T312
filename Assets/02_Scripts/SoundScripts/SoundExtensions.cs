/*========================================================
 * SoundExtensions.cs
 *
 * 概要
 * -------------------------------------------------------
 * BGM / SE enum に対する拡張メソッドを定義するクラス
 *
 * この拡張メソッドを使用すると
 *
 * BGM.Title.Play();
 * SE.Coin.Play();
 *
 * のように簡潔に書けるようになる
 *
 * -------------------------------------------------------
 * 使い方
 *
 * // BGM再生
 * BGM.Test.Play();
 *
 * // BGM停止
 * BGM.Test.Stop();
 *
 * // 即停止
 * BGM.Test.Stop(false);
 *
 * // SE再生
 * SE.Test.Play();
 *
 * -------------------------------------------------------
 * 
 *========================================================
*/

/// <summary>
/// BGM / SE の拡張メソッド
/// enum から直接サウンド操作を行えるようにする
/// </summary>
public static class SoundExtensions
{
    // ─────────────────────────────────────
    // BGM 関連
    // ─────────────────────────────────────

    /// <summary>
    /// BGMを再生する
    /// 内部的にはSoundManager.Instance.PlayBGM(...)を呼び出している
    /// </summary>
    /// <param name="bgm">
    /// 再生するBGM enum
    public static void Play(this BGM bgm)
        => SoundManager.Instance?.PlayBGM(bgm);

    /// <summary>
    /// 現在再生中のBGMを停止する
    /// </summary>
    /// <param name="bgm">
    /// 呼び出し元のBGM enum
    /// </param>
    /// <param name="fade">
    /// true  : フェードアウトして停止
    /// false : 即停止
    /// </param>
    public static void Stop(this BGM bgm, bool fade = true)
        => SoundManager.Instance?.StopBGM(fade);

    // ─────────────────────────────────────
    // SE 関連
    // ─────────────────────────────────────

    /// <summary>
    /// SE（効果音）を再生する
    /// 内部的にはSoundManager.Instance.PlaySE(...)を呼び出している
    /// </summary>
    /// <param name="se">
    /// 再生するSE enum
    /// </param>
    public static void Play(this SE se)
        => SoundManager.Instance?.PlaySE(se);
}