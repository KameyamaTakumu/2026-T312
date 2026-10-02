using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ボタン等から呼び出してシーン遷移を行うコンポーネント。
///
/// 通常は指定した sceneObject へ遷移するだけだが、チュートリアルの
/// 入口／出口ボタンとして使う場合は以下のオプションと組み合わせる。
/// ・recordAsEntryPoint : このボタンが押された時点のシーンを
///   TutorialEntryContext に記録する（チュートリアルへの入口用）
/// ・useRecordedEntryScene : 記録されたシーンへ遷移する
///   （チュートリアルからの戻りボタン用）。記録が無い場合は
///   sceneObject をフォールバック先として使用する
/// </summary>
public class SceneChanger : MonoBehaviour
{
    [Header("遷移先")]

    // 通常時の遷移先。useRecordedEntryScene有効時は記録が無い場合のフォールバックにもなる
    [SerializeField] private SceneObject sceneObject;

    [Header("チュートリアル遷移用（任意）")]

    [CustomLabel("チュートリアル入口として記録する")]
    [SerializeField] private bool recordAsEntryPoint = false;

    // recordAsEntryPoint有効時、TutorialEntryContextへ記録する「今いるシーン」の情報
    [SerializeField] private SceneObject selfSceneObject;

    [Header("チュートリアル遷移用（戻り先自動判定）")]

    [CustomLabel("記録された遷移元シーンへ戻る")]
    [SerializeField] private bool useRecordedEntryScene = false;

    [Header("復帰地点")]

    [CustomLabel("遷移時に復帰地点を削除する（タイトルへ戻るボタン用）")]
    [SerializeField] private bool clearResumePoint = false;

    /// <summary>ボタンのOnClickから呼び出すシーン遷移処理</summary>
    public void ButtonChangeScene()
    {
        // このボタンがチュートリアルへの入口として使われる場合、今いたシーンを記録しておく
        if (recordAsEntryPoint && selfSceneObject != null)
        {
            TutorialEntryContext.EntryScene = selfSceneObject;
            SaveResumePointIfPlaying();
        }

        // タイトルへ戻る場合などは、保存済みの復帰地点を削除する
        if (clearResumePoint && RespawnManager.Instance != null)
            RespawnManager.Instance.ClearResumePoint();

        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeOut(LoadScene);
        else
            LoadScene();
    }

    /// <summary>
    /// ゲーム中（プレイヤーがいるシーン）なら、現在の座標と向きを保存する。
    /// タイトルなどプレイヤーがいないシーンでは何もしない。
    /// </summary>
    private void SaveResumePointIfPlaying()
    {
        if (RespawnManager.Instance == null)
            return;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null)
            return;

        var snapshot = new RespawnManager.ResumeSnapshot
        {
            position = player.transform.position,
            rotation = player.transform.rotation,
            sceneName = SceneManager.GetActiveScene().name,
            coinCount = CoinManager.Instance != null ? CoinManager.Instance.CoinCount : 0
        };

        // 表示中（解放済み）のオブジェクトIDを記録
        foreach (var v in FindObjectsByType<ObjectVisibilityController>())
        {
            if (v.IsShown)
                snapshot.shownIds.Add(v.SaveId);
        }

        RespawnManager.Instance.SaveResume(snapshot);
    }

    private void LoadScene()
    {
        SceneObject destination = sceneObject;

        if (useRecordedEntryScene)
        {
            if (TutorialEntryContext.EntryScene != null)
            {
                destination = TutorialEntryContext.EntryScene;
            }
        }

        if (destination != null)
            destination.Load();
    }
}