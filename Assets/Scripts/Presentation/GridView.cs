using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class GridView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TileView tilePrefab;
    [SerializeField] Transform gridRoot;
    [SerializeField] TileColors tileColors;

    [Header("Layout")]
    [SerializeField] Vector3 gridOriginWorld = Vector3.zero;
    [SerializeField] float cellSize = 1f;

    [Header("Animation")]
    [SerializeField] float swapDuration = 0.15f;
    [SerializeField] float clearDuration = 0.2f;
    [SerializeField] float fallDuration = 0.25f;

    TileView[,] tileViews;
    Dictionary<Tile, TileView> viewMap = new Dictionary<Tile, TileView>();
    int width;
    int height;
    TileViewPool pool;

    public void Initialize(Tile[,] grid)
    {
        ClearAll();
        if (grid == null) return;

        // Initialize pool if needed
        if (pool == null)
        {
            var parent = gridRoot != null ? gridRoot : transform;
            pool = new TileViewPool(tilePrefab, parent);
        }

        width = grid.GetLength(0);
        height = grid.GetLength(1);
        tileViews = new TileView[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = grid[x, y];
                if (tile != null)
                {
                    var view = CreateTileView(tile);
                    tileViews[x, y] = view;
                    viewMap[tile] = view;
                }
            }
        }
    }

    public void UpdateView(Tile[,] grid)
    {
        if (grid == null) return;
        int gw = grid.GetLength(0);
        int gh = grid.GetLength(1);
        if (gw != width || gh != height)
        {
            // Reinitialize if size changed
            Initialize(grid);
            return;
        }

        // Track seen tiles to remove stale views
        var seen = new HashSet<Tile>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = grid[x, y];
                if (tile == null)
                {
                    var existing = tileViews[x, y];
                    if (existing != null)
                    {
                        DestroyTileView(existing);
                        tileViews[x, y] = null;
                    }
                    continue;
                }

                TileView view;
                if (!viewMap.TryGetValue(tile, out view) || view == null)
                {
                    view = CreateTileView(tile);
                    viewMap[tile] = view;
                }

                tileViews[x, y] = view;
                seen.Add(tile);

                // Ensure sprite and position are up to date
                var color = tileColors != null ? tileColors.GetColor(tile.Type) : Color.white;
                view.SetColor(color);
                view.SetPosition(GetWorldPosition(tile.Position));
            }
        }

        // Remove any views whose tiles are no longer present
        var toRemove = new List<Tile>();
        foreach (var kvp in viewMap)
        {
            if (!seen.Contains(kvp.Key))
            {
                DestroyTileView(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            viewMap.Remove(toRemove[i]);
        }
    }

    TileView CreateTileView(Tile tile)
    {
        if (tilePrefab == null)
        {
            Debug.LogError("GridView: tilePrefab is not assigned.");
            return null;
        }

        // Get from pool instead of instantiating
        var view = pool.Get();
        view.Bind(tile);
        var color = tileColors != null ? tileColors.GetColor(tile.Type) : Color.white;
        view.SetColor(color);
        view.SetPosition(GetWorldPosition(tile.Position));
        return view;
    }

    void DestroyTileView(TileView view)
    {
        if (view == null) return;

        // Return to pool instead of destroying
        if (Application.isPlaying && pool != null)
        {
            pool.Return(view);
        }
        else
        {
            DestroyImmediate(view.gameObject);
        }
    }

    void ClearAll()
    {
        if (tileViews != null)
        {
            for (int y = 0; y < tileViews.GetLength(1); y++)
            {
                for (int x = 0; x < tileViews.GetLength(0); x++)
                {
                    if (tileViews[x, y] != null)
                    {
                        DestroyTileView(tileViews[x, y]);
                        tileViews[x, y] = null;
                    }
                }
            }
        }
        if (viewMap != null)
        {
            foreach (var kvp in viewMap)
            {
                DestroyTileView(kvp.Value);
            }
            viewMap.Clear();
        }
    }

    void OnDestroy()
    {
        // Clean up pool when GridView is destroyed
        pool?.Clear();
    }

    Vector3 GetWorldPosition(GridPosition pos)
    {
        return gridOriginWorld + new Vector3(pos.X * cellSize, pos.Y * cellSize, 0f);
    }

    public Vector3 GetWorldPositionPublic(GridPosition pos)
    {
        return GetWorldPosition(pos);
    }

    public TileView GetViewForTile(Tile tile)
    {
        if (tile == null) return null;
        viewMap.TryGetValue(tile, out var view);
        return view;
    }

    public TileView GetViewAt(GridPosition pos)
    {
        if (pos.X < 0 || pos.X >= width || pos.Y < 0 || pos.Y >= height) return null;
        return tileViews[pos.X, pos.Y];
    }

    public async UniTask AnimateSwap(Tile a, Tile b, CancellationToken ct = default)
    {
        var viewA = GetViewForTile(a);
        var viewB = GetViewForTile(b);
        if (viewA == null || viewB == null)
        {
            return;
        }

        // At this point, tiles have already been logically swapped,
        // so a.Position is the NEW position, not the old one
        // We want viewA to move to a.Position (where it should now be)
        // and viewB to move to b.Position (where it should now be)
        var targetPosA = GetWorldPosition(a.Position);
        var targetPosB = GetWorldPosition(b.Position);

        // Start both animations in parallel
        await UniTask.WhenAll(
            viewA.AnimateMoveTo(targetPosA, swapDuration, ct),
            viewB.AnimateMoveTo(targetPosB, swapDuration, ct)
        );
    }

    public async UniTask AnimateClear(List<Tile> tiles, CancellationToken ct = default)
    {
        if (tiles == null || tiles.Count == 0) return;

        var tasks = new List<UniTask>();
        foreach (var tile in tiles)
        {
            var view = GetViewForTile(tile);
            if (view != null)
            {
                tasks.Add(view.AnimateRemoval(clearDuration, ct));
            }
        }

        await UniTask.WhenAll(tasks);
    }

    public async UniTask AnimateGravity(Dictionary<Tile, GridPosition> moves, CancellationToken ct = default)
    {
        if (moves == null || moves.Count == 0) return;

        var tasks = new List<UniTask>();
        foreach (var kvp in moves)
        {
            var tile = kvp.Key;
            var targetPos = kvp.Value;
            var view = GetViewForTile(tile);
            if (view != null)
            {
                var worldPos = GetWorldPosition(targetPos);
                tasks.Add(view.AnimateMoveTo(worldPos, fallDuration, ct));
            }
        }

        await UniTask.WhenAll(tasks);
    }
}
