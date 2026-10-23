using TMPro;
using UnityEngine;

/// <summary>
/// ステージの保存・読込・削除・共有コード・フォルダを開く、のUI操作をまとめたコンポーネント
/// </summary>
public class LevelSaveUI : MonoBehaviour
{
    [SerializeField] private LevelEditor editor;

    [Header("UI")]
    [SerializeField] private TMP_InputField nameInput;       // 保存名
    [SerializeField] private TMP_Dropdown levelDropdown;     // 保存済み一覧
    [SerializeField] private TMP_InputField codeInput;       // 共有コード（表示・貼り付け用）
    [SerializeField] private TMP_Text messageLabel;          // 結果の表示

    // 上書き・削除の確認用（同じ名前でもう一度押したら実行）
    private string overwriteCandidate;
    private string deleteCandidate;

    private void Start()
    {
        nameInput.onValueChanged.AddListener(_ => overwriteCandidate = null);
        levelDropdown.onValueChanged.AddListener(_ => deleteCandidate = null);

        Refresh(LevelStorage.CurrentLevelName);
    }

    /// <summary>一覧を作り直す</summary>
    public void Refresh(string select = null)
    {
        var names = LevelStorage.List();

        levelDropdown.ClearOptions();
        levelDropdown.AddOptions(names);

        if (names.Count == 0) return;

        int index = string.IsNullOrEmpty(select) ? 0 : names.IndexOf(select);
        levelDropdown.SetValueWithoutNotify(Mathf.Max(index, 0));
        levelDropdown.RefreshShownValue();
    }

    private string SelectedName()
    {
        if (levelDropdown.options.Count == 0) return null;
        return levelDropdown.options[levelDropdown.value].text;
    }

    // ─────────── ボタンから呼ぶ ───────────

    public void OnSave()
    {
        string stageName = LevelStorage.Sanitize(nameInput.text);
        if (string.IsNullOrEmpty(stageName))
        {
            Show("ステージ名を入力してください");
            return;
        }

        // 同名のステージがある場合は、もう一度押したときだけ上書きする
        if (LevelStorage.Exists(stageName) && overwriteCandidate != stageName)
        {
            overwriteCandidate = stageName;
            Show($"「{stageName}」は既にあります。上書きするならもう一度押してください");
            return;
        }

        overwriteCandidate = null;
        editor.Save(stageName);
        Refresh(stageName);
        Show($"「{stageName}」を保存しました");
    }

    public void OnLoad()
    {
        string stageName = SelectedName();
        if (stageName == null)
        {
            Show("保存されたステージがありません");
            return;
        }

        LevelData data = LevelStorage.Load(stageName);
        if (data == null)
        {
            Show($"「{stageName}」を読み込めませんでした");
            return;
        }

        editor.ApplyData(data);
        LevelStorage.CurrentLevelName = stageName;
        nameInput.text = stageName;
        overwriteCandidate = null;
        Show($"「{stageName}」を読み込みました");
    }

    public void OnDelete()
    {
        string stageName = SelectedName();
        if (stageName == null) return;

        if (deleteCandidate != stageName)
        {
            deleteCandidate = stageName;
            Show($"「{stageName}」を削除します。よければもう一度押してください");
            return;
        }

        deleteCandidate = null;
        LevelStorage.Delete(stageName);
        Refresh();
        Show($"「{stageName}」を削除しました");
    }

    /// <summary>現在のステージを共有コードにして、表示とコピーをする</summary>
    public void OnCopyCode()
    {
        string code = LevelStorage.ToCode(editor.BuildData());

        codeInput.text = code;
        GUIUtility.systemCopyBuffer = code;
        Show($"共有コードを作成しました（{code.Length}文字）");
    }

    /// <summary>入力欄の共有コードからステージを読み込む</summary>
    public void OnLoadFromCode()
    {
        LevelData data = LevelStorage.FromCode(codeInput.text);
        if (data == null)
        {
            Show("コードが正しくありません");
            return;
        }

        editor.ApplyData(data);
        Show($"コードから読み込みました（{data.items.Count}個）。残したい場合は名前を付けて保存してください");
    }

    /// <summary>ステージファイルのフォルダを開く（ファイルのやり取りに使う）</summary>
    public void OnOpenFolder()
    {
        Application.OpenURL("file://" + LevelStorage.Folder);
    }

    private void Show(string message)
    {
        if (messageLabel != null)
            messageLabel.text = message;
    }
}