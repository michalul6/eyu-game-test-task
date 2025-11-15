using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TileColors", menuName = "Match3/Tile Colors")]
public class TileColors : ScriptableObject
{
    [System.Serializable]
    public class TileColorMapping
    {
        public TileType type;
        public Color color = Color.white;
    }

    [SerializeField] List<TileColorMapping> mappings = new List<TileColorMapping>();

    Dictionary<TileType, Color> lookup;

    void OnEnable()
    {
        BuildLookup();
    }

    void BuildLookup()
    {
        lookup = new Dictionary<TileType, Color>(mappings != null ? mappings.Count : 0);
        if (mappings == null) return;
        for (int i = 0; i < mappings.Count; i++)
        {
            var m = mappings[i];
            if (!lookup.ContainsKey(m.type))
            {
                lookup.Add(m.type, m.color);
            }
        }
    }

    public Color GetColor(TileType type)
    {
        if (lookup == null) BuildLookup();
        if (lookup != null && lookup.TryGetValue(type, out var c))
            return c;
        // Provide sensible defaults if not mapped
        switch (type)
        {
            case TileType.Red: return Color.red;
            case TileType.Blue: return Color.blue;
            case TileType.Green: return Color.green;
            case TileType.Yellow: return Color.yellow;
            case TileType.Purple: return new Color(0.6f, 0.2f, 0.8f);
            case TileType.Orange: return new Color(1f, 0.5f, 0f);
            case TileType.RowBooster: return Color.white;
            default: return Color.clear;
        }
    }
}
