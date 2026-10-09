using UnityEngine;

[CreateAssetMenu(menuName = "Editor/Placeable Item")]
public class PlaceableItem : ScriptableObject
{
    public string id;                 // 保存用ID（重複しない名前）
    public string displayName;
    public Sprite icon;
    public GameObject prefab;

    // true=地面に吸着して置く / false=空間に自由に置く（惑星など）
    public bool alignToSurface = true;

    public float defaultScale = 1f;
    public bool allowScale = false;   // 惑星だけ ON など

    // 地面に吸着して置くとき、面の法線方向へ浮かせる高さ
    public float surfaceOffset = 0f;

    // true の場合、ステージに1つだけ置ける（もう一度置くと前のものが消えて移動する）
    public bool unique = false;

    // true の場合、2回置くごとに1組のペアになる
    public bool pairedPlacement = false;

    // true の場合、置いた順にゾーンがつながる（引力ジャンプゾーン用）
    public bool chainedPlacement = false;

    // 面に当たれば吸着、当たらなければ空間に置く（Align To Surface も ON にして使う）
    public bool surfaceOrFree = false;

    // true の場合、置いたあとに行き先の惑星をクリックして指定する（ランチャー用）
    public bool needsTarget = false;
}