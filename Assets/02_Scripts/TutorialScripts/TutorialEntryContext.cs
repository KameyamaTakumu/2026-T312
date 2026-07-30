/// <summary>
/// チュートリアルシーンへ入る直前にいたシーンを記憶しておくための入れ物。
///
/// 使い方：
/// ・タイトル／メインゲームの「チュートリアルへ」ボタンの SceneChanger で
///   recordAsEntryPoint = true にしておくと、ボタンを押した瞬間に
///   ここへ「今いたシーン」の SceneObject が記録される。
/// ・チュートリアル側の「終了」ボタンの SceneChanger で
///   useRecordedEntryScene = true にしておくと、ここに記録された
///   SceneObject を遷移先として使う。
///
/// 注意：
/// static なフィールドなので、シーンをロードしても値は保持されるが、
/// アプリの再起動（またはエディタのドメインリロード）でリセットされる。
/// エディタで直接チュートリアルシーンを再生した場合など、
/// 記録が無い状態も起こり得るため、SceneChanger 側でフォールバック先
/// （sceneObject）を必ず設定しておくこと。
/// </summary>
public static class TutorialEntryContext
{
    public static SceneObject EntryScene { get; set; }
}