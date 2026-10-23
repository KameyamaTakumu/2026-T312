using UnityEngine;

/// <summary>
/// Inspectorで選んだBGMを再生する汎用コンポーネント。
///
/// ・シーン開始時に自動再生（playOnStart）
/// ・ボタンのOnClickやUnityEventから Play() / Stop() を呼ぶことも可能
/// ・BGM.None を選ぶと、シーン開始時に現在のBGMを停止する
/// </summary>
public class BGMPlayer : MonoBehaviour
{
    [Header("再生するBGM")]

    [CustomLabel("BGM")]
    [SerializeField] private BGM bgm = BGM.Title;

    [CustomLabel("シーン開始時に自動再生する")]
    [SerializeField] private bool playOnStart = true;

    [CustomLabel("停止時にフェードする")]
    [SerializeField] private bool fadeOnStop = true;

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    /// <summary>設定したBGMを再生する（None の場合は停止）</summary>
    public void Play()
    {
        Play(bgm);
    }

    /// <summary>BGMを指定して再生する（UnityEventからはenumを渡せないため、int版も用意）</summary>
    public void Play(BGM target)
    {
        if (SoundManager.Instance == null)
            return;

        if (target == BGM.None)
        {
            Stop();
            return;
        }

        SoundManager.Instance.PlayBGM(target);
    }

    /// <summary>ボタンのOnClick等から使う用。enumの値(0, 1, 2…)で指定</summary>
    public void PlayByIndex(int index)
    {
        Play((BGM)index);
    }

    /// <summary>BGMを停止する</summary>
    public void Stop()
    {
        if (SoundManager.Instance == null)
            return;

        SoundManager.Instance.StopBGM(fadeOnStop);
    }
}