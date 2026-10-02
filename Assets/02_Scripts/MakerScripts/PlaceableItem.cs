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
}