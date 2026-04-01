using UnityEngine;

[CreateAssetMenu(menuName = "Items/Item Database")]
public class ItemDatabase : ScriptableObject {
    public ItemBlueprint[] items;
}