using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// カタログの内容から、配置アイテムの選択ボタンを自動生成する
/// </summary>
public class PlaceablePalette : MonoBehaviour
{
    [SerializeField] private PlaceableCatalog catalog;
    [SerializeField] private LevelEditor editor;

    // ボタンの雛形（Button + 子に文字。子に「Icon」という名前の Image があればアイコンも表示）
    [SerializeField] private Button buttonPrefab;

    // ボタンを並べる親（Layout Group を付けた Panel）
    [SerializeField] private Transform container;

    private void Start()
    {
        Build();
    }

    /// <summary>ボタンを作り直す（カタログを実行中に変更した場合にも使える）</summary>
    public void Build()
    {
        if (catalog == null || editor == null || buttonPrefab == null || container == null)
            return;

        // 既存のボタンを削除
        foreach (Transform child in container)
            Destroy(child.gameObject);

        foreach (PlaceableItem item in catalog.items)
        {
            if (item == null) continue;

            Button btn = Instantiate(buttonPrefab, container);
            btn.name = "Button_" + item.id;

            SetLabel(btn, string.IsNullOrEmpty(item.displayName) ? item.id : item.displayName);
            SetIcon(btn, item.icon);

            // ループ変数をそのまま使わず、ローカルにコピーして渡す
            PlaceableItem captured = item;
            btn.onClick.AddListener(() => editor.Select(captured));
        }
    }

    private static void SetLabel(Button btn, string text)
    {
        // TextMeshPro を優先し、無ければ通常の Text を使う
        TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = text;
            return;
        }

        Text legacy = btn.GetComponentInChildren<Text>();
        if (legacy != null)
            legacy.text = text;
    }

    private static void SetIcon(Button btn, Sprite icon)
    {
        // 子の「Icon」という名前の Image にだけ設定する
        Transform iconTransform = btn.transform.Find("Icon");
        if (iconTransform == null) return;

        Image img = iconTransform.GetComponent<Image>();
        if (img == null) return;

        // アイコン未設定のアイテムは、Icon の枠ごと隠す
        img.sprite = icon;
        img.gameObject.SetActive(icon != null);
    }
}