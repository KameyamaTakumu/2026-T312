using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// ステージの保存・読込・一覧・共有コード変換をまとめた静的クラス
/// </summary>
public static class LevelStorage
{
    private const string CodePrefix = "SPL1-";   // 共有コードの先頭（形式の目印）
    private const int MaxNameLength = 40;
    private const int MaxCodeLength = 500000;    // 異常に長いコードは受け付けない
    private const int MaxItems = 5000;

    /// <summary>
    /// 最後に保存・読込したステージ名。プレイ用シーンはこの名前を読み込む。
    /// PlayerPrefs に残すので、プレイ用シーンを直接再生しても同じ名前が使われる
    /// </summary>
    public static string CurrentLevelName
    {
        get => PlayerPrefs.GetString("LastLevelName", "");
        set
        {
            PlayerPrefs.SetString("LastLevelName", value ?? "");
            PlayerPrefs.Save();
        }
    }

    /// <summary>ステージファイルの保存先フォルダ</summary>
    public static string Folder
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, "Levels");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>ファイル名に使えない文字を取り除く</summary>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";

        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "");

        name = name.Trim();
        if (name.Length > MaxNameLength)
            name = name.Substring(0, MaxNameLength);

        return name;
    }

    private static string PathOf(string name)
        => Path.Combine(Folder, name + ".json");

    public static bool Exists(string name)
    {
        name = Sanitize(name);
        return !string.IsNullOrEmpty(name) && File.Exists(PathOf(name));
    }

    public static bool Save(string name, LevelData data)
    {
        name = Sanitize(name);
        if (string.IsNullOrEmpty(name) || data == null) return false;

        try
        {
            File.WriteAllText(PathOf(name), JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LevelStorage] 保存に失敗: {e.Message}");
            return false;
        }
    }

    public static LevelData Load(string name)
    {
        name = Sanitize(name);
        if (string.IsNullOrEmpty(name)) return null;

        string path = PathOf(name);
        if (!File.Exists(path)) return null;

        try
        {
            return Validate(JsonUtility.FromJson<LevelData>(File.ReadAllText(path)));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LevelStorage] 読込に失敗: {e.Message}");
            return null;
        }
    }

    public static bool Delete(string name)
    {
        name = Sanitize(name);
        if (string.IsNullOrEmpty(name)) return false;

        string path = PathOf(name);
        if (!File.Exists(path)) return false;

        File.Delete(path);

        if (CurrentLevelName == name)
            CurrentLevelName = "";

        return true;
    }

    /// <summary>保存済みステージの名前一覧（新しく保存した順）</summary>
    public static List<string> List()
    {
        var result = new List<string>();
        FileInfo[] files = new DirectoryInfo(Folder).GetFiles("*.json");

        Array.Sort(files, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));

        foreach (var f in files)
            result.Add(Path.GetFileNameWithoutExtension(f.Name));

        return result;
    }

    // ─────────── 共有コード ───────────

    /// <summary>ステージを、コピーして渡せる文字列（共有コード）にする</summary>
    public static string ToCode(LevelData data)
    {
        byte[] raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));

        using (var ms = new MemoryStream())
        {
            using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal))
                gz.Write(raw, 0, raw.Length);

            return CodePrefix + Convert.ToBase64String(ms.ToArray());
        }
    }

    /// <summary>共有コードからステージを復元する。不正なコードなら null</summary>
    public static LevelData FromCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;

        // 貼り付け時に混ざる改行・空白を取り除く
        code = Regex.Replace(code, @"\s+", "");

        if (code.Length > MaxCodeLength || !code.StartsWith(CodePrefix))
            return null;

        try
        {
            byte[] compressed = Convert.FromBase64String(code.Substring(CodePrefix.Length));

            using (var input = new MemoryStream(compressed))
            using (var gz = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gz.CopyTo(output);
                string json = Encoding.UTF8.GetString(output.ToArray());
                return Validate(JsonUtility.FromJson<LevelData>(json));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LevelStorage] コードの読込に失敗: {e.Message}");
            return null;
        }
    }

    private static LevelData Validate(LevelData data)
    {
        if (data == null || data.items == null || data.items.Count > MaxItems)
            return null;

        return data;
    }
}