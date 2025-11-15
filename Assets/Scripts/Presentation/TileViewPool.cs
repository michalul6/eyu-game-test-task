using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple object pool for TileView instances.
/// Follows Single Responsibility Principle - only handles pooling logic.
/// </summary>
public class TileViewPool
{
    readonly TileView prefab;
    readonly Transform parent;
    readonly Stack<TileView> availableViews = new Stack<TileView>();
    readonly HashSet<TileView> activeViews = new HashSet<TileView>();

    public TileViewPool(TileView prefab, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;
    }

    /// <summary>
    /// Get a TileView from the pool (reuses existing or creates new).
    /// </summary>
    public TileView Get()
    {
        TileView view;
        
        if (availableViews.Count > 0)
        {
            view = availableViews.Pop();
            view.gameObject.SetActive(true);
        }
        else
        {
            view = Object.Instantiate(prefab, parent);
        }

        activeViews.Add(view);
        return view;
    }

    /// <summary>
    /// Return a TileView to the pool for reuse.
    /// </summary>
    public void Return(TileView view)
    {
        if (view == null) return;
        if (!activeViews.Remove(view)) return; // Not from this pool

        // Reset state before returning to pool
        view.transform.localScale = Vector3.one;
        view.SetHighlight(false);
        view.gameObject.SetActive(false);
        availableViews.Push(view);
    }

    /// <summary>
    /// Destroy all pooled objects (use when cleaning up).
    /// </summary>
    public void Clear()
    {
        // Destroy inactive pooled objects
        while (availableViews.Count > 0)
        {
            var view = availableViews.Pop();
            if (view != null)
            {
                Object.Destroy(view.gameObject);
            }
        }

        // Destroy active objects
        foreach (var view in activeViews)
        {
            if (view != null)
            {
                Object.Destroy(view.gameObject);
            }
        }
        activeViews.Clear();
    }

    /// <summary>
    /// Get count of active views.
    /// </summary>
    public int ActiveCount => activeViews.Count;

    /// <summary>
    /// Get count of available views in pool.
    /// </summary>
    public int AvailableCount => availableViews.Count;
}
