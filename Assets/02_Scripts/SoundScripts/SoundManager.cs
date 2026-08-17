/*========================================================
 * サウンド管理クラス
 * 
 * 概要
 * ・BGM（背景音楽）
 * ・SE（効果音）
 * を一括管理するシングルトンマネージャ
 * 
 * -------------------------------------------------------
 * 使い方
 * 
 * 1. enum に音を追加
 * 
 * public enum BGM
 * {
 *     Title,
 *     Battle,
 * }
 * 
 * public enum SE
 * {
 *     Jump,
 *     Coin,
 * }
 * 
 * 2. SoundManager の Inspector に
 * enum の順番通り AudioClip を設定
 * 
 * 3. スクリプトから再生
 * 
 * BGM.Title.Play();
 * SE.Coin.Play();
 * 
 * -------------------------------------------------------
 * 特徴
 * 
 * ・BGMフェード切り替え対応
 * ・SE同時再生対応
 * ・シーンを跨いで保持（DontDestroyOnLoad）
 * ・enum管理なので文字列ミスが起きない
 *========================================================
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region BGM Enum

/// <summary>
/// BGM 一覧
/// enum の順番と
/// Inspector の bgmList の順番が対応する
/// </summary>
public enum BGM
{
    None = -1,

    Planet1,
    Planet2,
    Planet3,
    Tutorial,
    Title
}

#endregion

#region SE Enum

/// <summary>
/// SE 一覧
/// enum の順番と
/// Inspector の seList の順番が対応する
/// </summary>
public enum SE
{
    None = -1,

    Jump,
    CoinGet,
    Spin,
    Damage_Player,
    Damage_Enemy,
    EnemyDie,
    Button,
    Warp,
    Fade,
    PipeWarp,
    RabitCatch
}

#endregion

/// <summary>
/// サウンド管理クラス
/// BGM / SE の再生を一括管理する
/// シングルトンマネージャ
/// </summary>
public class SoundManager : MonoBehaviour
{
    // ─────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────

    /// <summary>
    /// グローバルアクセス用インスタンス
    /// どこからでも
    /// SoundManager.Instance
    /// でアクセス可能
    /// </summary>
    public static SoundManager Instance;

    // ─────────────────────────────────────
    // AudioSource
    // ─────────────────────────────────────

    [Header("BGM用AudioSource")]

    /// <summary>
    /// BGM専用 AudioSource
    /// ・ループ再生想定
    /// ・フェード制御対象
    /// </summary>
    public AudioSource bgmSource;

    [Header("SE用AudioSource")]

    /// <summary>
    /// SE専用 AudioSource
    /// PlayOneShot 用
    /// </summary>
    public AudioSource seSource;

    // ─────────────────────────────────────
    // AudioClip List
    // ─────────────────────────────────────

    [Header("BGMやSEをそれぞれ対応する場所に入れてください。")]

    /// <summary>
    /// BGMクリップ一覧
    /// enum の順番と一致させる必要がある
    /// </summary>
    [CustomLabel("BGM Clips")]
    public List<AudioClip> bgmList =
        new List<AudioClip>();

    /// <summary>
    /// SEクリップ一覧
    /// enum の順番と一致させる必要がある
    /// </summary>
    [CustomLabel("SE Clips")]
    public List<AudioClip> seList =
        new List<AudioClip>();

    // ─────────────────────────────────────
    // Fade Setting
    // ─────────────────────────────────────

    [Header("フェード設定")]

    /// <summary>
    /// フェード時間
    /// BGM切り替え時の
    /// フェードアウト / フェードインに使用
    /// </summary>
    [Range(0f, 5f)]
    public float fadeTime = 1f;

    // ─────────────────────────────────────
    // Internal State
    // ─────────────────────────────────────

    /// <summary>
    /// 現在実行中のフェードコルーチン
    /// 新しいBGM再生時に
    /// 古いフェード処理を停止するために使う
    /// </summary>
    private Coroutine fadeCoroutine;

    /// <summary>
    /// 現在再生中のBGM index
    /// 同じBGMの重複再生防止用
    /// </summary>
    private int? currentBGMIndex = null;

    /// <summary>
    /// ユーザー設定BGM音量
    /// フェード後に戻す最大音量
    /// </summary>
    private float bgmMaxVolume = 1f;

    // ─────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────

    /// <summary>
    /// 初期化処理
    /// </summary>
    private void Awake()
    {
        // ─────────────────────────────
        // シングルトン重複防止
        // ─────────────────────────────

        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // シーン切り替え時に破棄しない
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────
    // BGM
    // ─────────────────────────────────────

    /// <summary>
    /// BGM再生
    /// </summary>
    /// <param name="index">
    /// 再生するBGM enum
    /// </param>
    public void PlayBGM(BGM index)
    {
        int idx = (int)index;

        // 現在再生されているBGM
        Debug.Log($"Current BGM Index: {currentBGMIndex}, Requested BGM Index: {idx}");

        // 同じBGM再生中なら何もしない
        if (currentBGMIndex == idx &&
            bgmSource.isPlaying)
        {
            return;
        }

        // 古いフェード処理停止
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // フェード切り替え開始
        fadeCoroutine =
            StartCoroutine(FadeBGM(idx));
    }

    /// <summary>
    /// BGMフェード切り替え処理
    /// 1. 現在BGMフェードアウト
    /// 2. 新BGM再生
    /// 3. フェードイン
    /// </summary>
    /// <param name="newIndex">
    /// 新しく再生するBGM index
    /// </param>
    private IEnumerator FadeBGM(int newIndex)
    {
        float t = 0f;

        // ─────────────────────────────
        // フェードアウト
        // ─────────────────────────────

        float initialVolume =
            bgmSource.volume;

        while (t < fadeTime)
        {
            bgmSource.volume =
                Mathf.Lerp(
                    initialVolume,
                    0f,
                    t / fadeTime
                );

            // TimeScale 無視
            // ポーズ中でもフェード可能
            t += Time.unscaledDeltaTime;

            yield return null;
        }

        bgmSource.volume = 0f;

        bgmSource.Stop();

        // ─────────────────────────────
        // 新BGM再生
        // ─────────────────────────────

        if (newIndex < bgmList.Count &&
            bgmList[newIndex] != null)
        {
            bgmSource.clip =
                bgmList[newIndex];

            bgmSource.volume = 0f;

            bgmSource.Play();

            currentBGMIndex = newIndex;
        }
        else
        {
            // Clip 未設定
            fadeCoroutine = null;
            yield break;
        }

        // ─────────────────────────────
        // フェードイン
        // ─────────────────────────────

        t = 0f;

        while (t < fadeTime)
        {
            bgmSource.volume =
                Mathf.Lerp(
                    0f,
                    bgmMaxVolume,
                    t / fadeTime
                );

            t += Time.unscaledDeltaTime;

            yield return null;
        }

        bgmSource.volume = bgmMaxVolume;

        fadeCoroutine = null;
    }

    // ─────────────────────────────────────
    // SE
    // ─────────────────────────────────────

    /// <summary>
    /// SE再生
    /// </summary>
    /// <param name="index">
    /// 再生するSE enum
    /// </param>
    public void PlaySE(SE index)
    {
        int idx = (int)index;

        // 範囲チェック + nullチェック
        if (idx < seList.Count &&
            seList[idx] != null)
        {
            // PlayOneShot は同時再生可能
            seSource.PlayOneShot(
                seList[idx]
            );
        }
    }

    // ─────────────────────────────────────
    // Stop
    // ─────────────────────────────────────

    /// <summary>
    /// BGM停止
    /// </summary>
    /// <param name="fade">
    /// true  = フェード停止
    /// false = 即停止
    /// </param>
    public void StopBGM(bool fade = true)
    {
        // 実行中フェード停止
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        if (fade)
        {
            StartCoroutine(FadeOut());
        }
        else
        {
            bgmSource.Stop();
        }

        currentBGMIndex = null;
    }

    /// <summary>
    /// BGMフェードアウト停止
    /// </summary>
    private IEnumerator FadeOut()
    {
        float startVolume =
            bgmSource.volume;

        float t = 0f;

        while (t < fadeTime)
        {
            bgmSource.volume =
                Mathf.Lerp(
                    startVolume,
                    0f,
                    t / fadeTime
                );

            t += Time.unscaledDeltaTime;

            yield return null;
        }

        bgmSource.Stop();

        // 元音量へ戻す
        // 次回再生用
        bgmSource.volume = startVolume;
    }

    // ─────────────────────────────────────
    // Volume
    // ─────────────────────────────────────

    /// <summary>
    /// BGM音量設定
    /// </summary>
    /// <param name="volume">
    /// 設定する音量（0～1）
    /// </param>
    public void SetBGMVolume(float volume)
    {
        // 0～1 に制限
        bgmMaxVolume =
            Mathf.Clamp01(volume);

        // 再生中なら即反映
        if (bgmSource.isPlaying)
        {
            bgmSource.volume =
                bgmMaxVolume;
        }
    }

    /// <summary>
    /// SE音量設定
    /// </summary>
    /// <param name="volume">
    /// 設定する音量（0～1）
    /// </param>
    public void SetSEVolume(float volume)
    {
        seSource.volume =
            Mathf.Clamp01(volume);
    }

    /// <summary>
    /// 現在のBGM音量取得
    /// </summary>
    public float GetBGMVolume()
    {
        return bgmSource.volume;
    }

    /// <summary>
    /// 現在のSE音量取得
    /// </summary>
    public float GetSEVolume()
    {
        return seSource.volume;
    }
}