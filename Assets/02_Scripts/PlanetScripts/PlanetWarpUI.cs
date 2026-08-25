using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 惑星間飛行UI演出
/// 
/// 主な役割：
/// ・画面フェード
/// ・飛行開始演出
/// ・着地演出
/// </summary>
public class PlanetWarpUI : MonoBehaviour
{
    [Header("フェード設定")]

    // フェード用 Image
    // 全画面UI Image を想定
    [CustomLabel("フェード用 Image"), SerializeField]
    private Image fadeImage;

    // 暗転時間
    [CustomLabel("フェードアウト時間（秒）"), SerializeField]
    private float fadeOutDuration = 0.3f;

    // 明転時間
    [CustomLabel("フェードイン時間（秒）"), SerializeField]
    private float fadeInDuration = 0.5f;

    // 最大不透明度
    [CustomLabel("最大不透明度"), SerializeField, Range(0f, 1f)]
    private float maxAlpha = 1.0f;

    // 実行中 Coroutine
    private Coroutine currentCoroutine;

    private void Awake()
    {
        if (fadeImage == null)
        {
            return;
        }

        // 起動時は透明
        SetAlpha(0f);
    }

    /// <summary>
    /// 飛行開始演出
    /// 画面を暗転させる
    /// </summary>
    public void PlayLaunchEffect()
    {
        // 多重 Coroutine 防止
        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        currentCoroutine =
            StartCoroutine(
                FadeCoroutine(
                    0f,
                    maxAlpha,
                    fadeOutDuration,
                    Color.black
                )
            );
    }

    /// <summary>
    /// 着地演出
    /// 画面を明転させる
    /// </summary>
    public void PlayLandEffect()
    {
        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        currentCoroutine =
            StartCoroutine(LandSequence());
    }

    /// <summary>
    /// 着地シーケンス
    /// </summary>
    private IEnumerator LandSequence()
    {
        // 黒画面 → 透明
        yield return FadeCoroutine(
            maxAlpha,
            0f,
            fadeInDuration,
            Color.black
        );
    }

    /// <summary>
    /// フェード処理 Coroutine
    /// </summary>
    private IEnumerator FadeCoroutine(
        float fromAlpha,
        float toAlpha,
        float duration,
        Color baseColor)
    {
        if (fadeImage == null)
            yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // アルファ補間
            float alpha =
                Mathf.Lerp(
                    fromAlpha,
                    toAlpha,
                    elapsed / duration
                );

            fadeImage.color =
                new Color(
                    baseColor.r,
                    baseColor.g,
                    baseColor.b,
                    alpha
                );

            yield return null;
        }

        // 最終値補正
        fadeImage.color =
            new Color(
                baseColor.r,
                baseColor.g,
                baseColor.b,
                toAlpha
            );
    }

    /// <summary>
    /// フェード透明度直接設定
    /// </summary>
    private void SetAlpha(float alpha)
    {
        if (fadeImage == null)
            return;

        Color c = fadeImage.color;

        fadeImage.color =
            new Color(
                c.r,
                c.g,
                c.b,
                alpha
            );
    }
}