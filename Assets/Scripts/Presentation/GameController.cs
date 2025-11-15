using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;

public class GameController : MonoBehaviour
{
    [Header("View")]
    [SerializeField] GridView gridView;
    [Header("Grid Settings")]
    [SerializeField] int width = 6;
    [SerializeField] int height = 6;
    [SerializeField] int randomSeed = 12345;
    [SerializeField]
    [Tooltip("Minimum matched tiles to create a booster (4+ recommended, set very high to disable)")]
    int minTilesForBooster = 4;

#if UNITY_EDITOR
    void OnValidate()
    {
        SyncMinTilesForBooster();
    }
#endif

    void SyncMinTilesForBooster()
    {
        if (gridManager != null)
        {
            gridManager.MinTilesForBooster = minTilesForBooster;
        }
    }

    [Header("Screen->Grid Mapping")]
    [Tooltip("Pixels; below this delta is treated as a tap, otherwise a swipe")]
    [SerializeField] float swipeThresholdPixels = 18f;

    GridManager gridManager;
    InputAction pointAction;
    InputAction clickAction;
    InputAction performanceTestAction;

    Vector2 pressScreenPos;
    GridPosition pressGridPos;
    bool isPointerDown;
    bool isProcessing;

    [Header("Debug")]
    [SerializeField] bool enableLogging = false;

    // Selection highlight
    TileView highlightedView;

    void Log(string message)
    {
        if (enableLogging) Debug.Log(message);
    }
    void LogWarning(string message)
    {
        if (enableLogging) Debug.LogWarning(message);
    }
    void LogError(string message)
    {
        if (enableLogging) Debug.LogError(message);
    }

    Camera mainCam;

    void Awake()
    {
        mainCam = Camera.main;
        gridManager = new GridManager(width, height, randomSeed);
        SyncMinTilesForBooster();

        // Set up Input System actions directly so we don't rely on generated classes
        pointAction = new InputAction(type: InputActionType.PassThrough, binding: "<Pointer>/position");
        clickAction = new InputAction(type: InputActionType.Button, binding: "<Pointer>/press");
        clickAction.started += OnPointerDown;
        clickAction.canceled += OnPointerUp;

        // Performance test action (Press P key)
        performanceTestAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/t");
        performanceTestAction.performed += OnPerformanceTest;

        // Initialize view if assigned
        if (gridView != null)
        {
            gridView.Initialize(gridManager.Grid);
        }
    }

    void OnEnable()
    {
        pointAction?.Enable();
        clickAction?.Enable();
        performanceTestAction?.Enable();
    }

    void OnDisable()
    {
        performanceTestAction?.Disable();
        clickAction?.Disable();
        pointAction?.Disable();
    }

    void OnDestroy()
    {
        if (clickAction != null)
        {
            clickAction.started -= OnPointerDown;
            clickAction.canceled -= OnPointerUp;
        }
        if (performanceTestAction != null)
        {
            performanceTestAction.performed -= OnPerformanceTest;
        }
    }

    void OnPerformanceTest(InputAction.CallbackContext ctx)
    {
        RunPerformanceTest();
    }

    void OnPointerDown(InputAction.CallbackContext callback)
    {
        if (isProcessing) return; // don't allow selection while animating
        pressScreenPos = pointAction.ReadValue<Vector2>();
        pressGridPos = ScreenToGrid(pressScreenPos);
        isPointerDown = true;
        Log($"[GameController] Pointer DOWN at screen {pressScreenPos} → grid {pressGridPos.X},{pressGridPos.Y}");

        // Highlight the pressed tile if valid
        if (pressGridPos.IsValid(gridManager.Width, gridManager.Height))
        {
            SetHighlightAt(pressGridPos, true);
        }
        else
        {
            ClearHighlight();
        }
    }

    void OnPointerUp(InputAction.CallbackContext callback)
    {
        if (!isPointerDown) { ClearHighlight(); return; }
        if (isProcessing) { ClearHighlight(); return; } // Ignore input while animating
        isPointerDown = false;

        Vector2 releaseScreen = pointAction.ReadValue<Vector2>();
        Vector2 delta = releaseScreen - pressScreenPos;

        Log($"[GameController] Pointer UP at screen {releaseScreen}, delta {delta.magnitude:F1} px (threshold={swipeThresholdPixels})");

        // Clear highlight on release
        ClearHighlight();

        if (delta.magnitude < swipeThresholdPixels)
        {
            // Treat as tap: try activating booster at pressed cell
            Log($"[GameController] TAP detected at grid {pressGridPos.X},{pressGridPos.Y}");
            TryTapBooster(pressGridPos);
        }
        else
        {
            // Treat as swipe: map to cardinal direction and attempt swap
            var dir = GetSwipeDirection(delta);
            var target = new GridPosition(pressGridPos.X + dir.x, pressGridPos.Y + dir.y);
            Log($"[GameController] SWIPE detected: {pressGridPos.X},{pressGridPos.Y} → {target.X},{target.Y}");
            TrySwapAnimated(pressGridPos, target).Forget();
        }
    }

    void SetHighlightAt(GridPosition pos, bool highlighted)
    {
        // Clear previous
        if (highlightedView != null)
        {
            highlightedView.SetHighlight(false);
            highlightedView = null;
        }
        if (!highlighted) return;

        if (!pos.IsValid(gridManager.Width, gridManager.Height)) return;
        var tile = gridManager.Grid[pos.X, pos.Y];
        if (tile == null || gridView == null) return;
        var view = gridView.GetViewForTile(tile);
        if (view == null) return;
        view.SetHighlight(true);
        highlightedView = view;
    }

    void ClearHighlight()
    {
        if (highlightedView != null)
        {
            highlightedView.SetHighlight(false);
            highlightedView = null;
        }
    }

    void TryTapBooster(GridPosition pos)
    {
        Log($"[GameController] TryTapBooster at {pos.X},{pos.Y}, valid={pos.IsValid(gridManager.Width, gridManager.Height)}");
        if (!pos.IsValid(gridManager.Width, gridManager.Height))
        {
            LogWarning($"[GameController] Position {pos.X},{pos.Y} is out of bounds (grid size {gridManager.Width}x{gridManager.Height})");
            return;
        }
        var tile = gridManager.Grid[pos.X, pos.Y];
        string coloredTileInfo = GetColoredTileLogString(tile, pos);
        Log($"[GameController] Tile at {pos.X},{pos.Y}: {coloredTileInfo}");

        // Run animated booster activation
        ActivateBoosterAnimated(pos).Forget();
    }

    async UniTask ActivateBoosterAnimated(GridPosition pos)
    {
        if (isProcessing) return;
        isProcessing = true;

        try
        {
            var affected = gridManager.GetBoosterAffectedTiles(pos);
            if (affected == null || affected.Count == 0)
            {
                Log($"[GameController] No booster effect at {pos.X},{pos.Y}.");
                return;
            }

            // Animate removal of affected tiles
            if (gridView != null)
            {
                await gridView.AnimateClear(affected);
            }

            // Clear from grid using a synthetic match so booster expansion logic applies consistently
            var syntheticMatch = new Match(new List<Tile>(affected), MatchType.Special);
            gridManager.Clear(new List<Match> { syntheticMatch });

            // Capture positions before gravity to compute movements
            var beforeGravity = new Dictionary<Tile, GridPosition>();
            for (int y = 0; y < gridManager.Height; y++)
            {
                for (int x = 0; x < gridManager.Width; x++)
                {
                    var t = gridManager.Grid[x, y];
                    if (t != null)
                        beforeGravity[t] = t.Position;
                }
            }

            // Apply gravity
            gridManager.ApplyGravity();

            // Detect movements for animation
            var movements = new Dictionary<Tile, GridPosition>();
            for (int y = 0; y < gridManager.Height; y++)
            {
                for (int x = 0; x < gridManager.Width; x++)
                {
                    var t = gridManager.Grid[x, y];
                    if (t != null && beforeGravity.TryGetValue(t, out var prev) && !prev.Equals(t.Position))
                    {
                        movements[t] = t.Position;
                    }
                }
            }

            // Animate gravity
            if (gridView != null && movements.Count > 0)
            {
                await gridView.AnimateGravity(movements);
            }

            // Refill and update view
            gridManager.Refill();
            if (gridView != null)
            {
                gridView.UpdateView(gridManager.Grid);
            }

            // Resolve cascades as usual
            await ProcessMatchesAnimated();
        }
        finally
        {
            isProcessing = false;
        }
    }

    // Removed legacy non-animated TrySwap in favor of TrySwapAnimated

    async UniTaskVoid TrySwapAnimated(GridPosition from, GridPosition to)
    {
        Log($"[GameController] TrySwapAnimated from {from.X},{from.Y} to {to.X},{to.Y}");
        isProcessing = true;

        if (!from.IsValid(gridManager.Width, gridManager.Height) || !to.IsValid(gridManager.Width, gridManager.Height))
        {
            LogWarning($"[GameController] Invalid positions for swap");
            isProcessing = false;
            return;
        }

        // Get the original tiles before any swap
        var a = gridManager.Grid[from.X, from.Y];
        var b = gridManager.Grid[to.X, to.Y];

        if (a == null || b == null)
        {
            LogError($"[GameController] One or both tiles are null at ({from.X},{from.Y}) or ({to.X},{to.Y})");
            isProcessing = false;
            return;
        }

        Log($"[GameController] TileA: {a.Type} at ({from.X},{from.Y}), TileB: {b.Type} at ({to.X},{to.Y})");

        // Store original positions and world positions BEFORE any swap or view animation
        var origPosA = new GridPosition(from.X, from.Y);
        var origPosB = new GridPosition(to.X, to.Y);
        var origWorldPosA = gridView.GetWorldPositionPublic(origPosA);
        var origWorldPosB = gridView.GetWorldPositionPublic(origPosB);

        // Perform logical swap (this updates tile positions but doesn't process cascades)
        bool createsMatch = gridManager.Swap(a, b);

        // Animate the swap forward
        await gridView.AnimateSwap(a, b);

        if (createsMatch)
        {
            Log($"[GameController] Valid swap; processing matches");

            // Process cascading matches
            await ProcessMatchesAnimated();
        }
        else
        {
            // Invalid swap - animate revert, then revert logically
            Log($"[GameController] Invalid swap; animating revert");

            // Get the views while tiles are still in swapped positions
            var viewA = gridView.GetViewForTile(a);
            var viewB = gridView.GetViewForTile(b);

            if (viewA != null && viewB != null)
            {
                Log($"[GameController] ViewA: {viewA.name}, ViewB: {viewB.name}");
                Log($"[GameController] ViewA current pos: {viewA.transform.position}");
                Log($"[GameController] ViewB current pos: {viewB.transform.position}");
                Log($"[GameController] Target pos A: {origWorldPosA}, Target pos B: {origWorldPosB}");
                Log($"[GameController] Starting revert animations...");

                // Start both animations in parallel
                await UniTask.WhenAll(
                    viewA.AnimateMoveTo(origWorldPosA, 0.15f),
                    viewB.AnimateMoveTo(origWorldPosB, 0.15f)
                );

                Log($"[GameController] Revert animation complete");
                Log($"[GameController] ViewA final pos: {viewA.transform.position}");
                Log($"[GameController] ViewB final pos: {viewB.transform.position}");
            }
            else
            {
                LogError($"[GameController] Could not find views for tiles! ViewA null: {viewA == null}, ViewB null: {viewB == null}, gridView null: {gridView == null}");
            }

            // Now revert the logical swap
            gridManager.Swap(a, b);
        }

        isProcessing = false;
    }

    async UniTask ProcessMatchesAnimated()
    {
        while (true)
        {
            var matches = gridManager.FindMatches();
            if (matches == null || matches.Count == 0) break;

            // Collect all tiles to clear and exclude one tile per 4+ match to convert into a booster
            var tilesToClear = new List<Tile>();
            var boosterSurvivors = new HashSet<Tile>();
            foreach (var match in matches)
            {
                if (match.Tiles != null && match.Tiles.Count >= gridManager.MinTilesForBooster)
                {
                    int idx = match.Tiles.Count / 2; // deterministic: middle of the run
                    var survivor = match.Tiles[idx];
                    if (survivor != null)
                        boosterSurvivors.Add(survivor);
                }

                foreach (var t in match.Tiles)
                {
                    if (t != null && !boosterSurvivors.Contains(t))
                        tilesToClear.Add(t);
                }
            }

            // Animate removal
            if (gridView != null)
            {
                await gridView.AnimateClear(tilesToClear);
            }

            // Clear from grid
            gridManager.Clear(matches);

            // Capture tile movements for gravity animation
            var beforeGravity = new Dictionary<Tile, GridPosition>();
            for (int y = 0; y < gridManager.Height; y++)
            {
                for (int x = 0; x < gridManager.Width; x++)
                {
                    var tile = gridManager.Grid[x, y];
                    if (tile != null)
                    {
                        beforeGravity[tile] = tile.Position;
                    }
                }
            }

            // Apply gravity
            gridManager.ApplyGravity();

            // Detect movements
            var movements = new Dictionary<Tile, GridPosition>();
            for (int y = 0; y < gridManager.Height; y++)
            {
                for (int x = 0; x < gridManager.Width; x++)
                {
                    var tile = gridManager.Grid[x, y];
                    if (tile != null && beforeGravity.ContainsKey(tile))
                    {
                        if (!beforeGravity[tile].Equals(tile.Position))
                        {
                            movements[tile] = tile.Position;
                        }
                    }
                }
            }

            // Animate gravity
            if (gridView != null && movements.Count > 0)
            {
                await gridView.AnimateGravity(movements);
            }

            // Refill
            gridManager.Refill();

            // Update view for new tiles
            if (gridView != null)
            {
                gridView.UpdateView(gridManager.Grid);
            }

            await UniTask.Delay(100); // Small delay before checking for cascades (100ms)
        }
    }

    (int x, int y) GetSwipeDirection(Vector2 delta)
    {
        // Map to cardinal direction with largest absolute component
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return (delta.x > 0 ? 1 : -1, 0);
        }
        return (0, delta.y > 0 ? 1 : -1);
    }

    GridPosition ScreenToGrid(Vector2 screenPos)
    {
        if (mainCam == null) mainCam = Camera.main;
        Vector3 world = screenPos;
        if (mainCam != null)
        {
            world = mainCam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(mainCam.transform.position.z)));
        }

        // Offset by half cell because sprites are centered on their positions
        float localX = world.x + 0.5f;
        float localY = world.y + 0.5f;

        // Check if click is actually within grid bounds in world space
        float maxX = width;
        float maxY = height;
        if (localX < 0 || localX >= maxX || localY < 0 || localY >= maxY)
        {
            Log($"[GameController] ScreenToGrid: screen {screenPos} → world {world} → local {localX},{localY} → OUT OF BOUNDS (grid world bounds: 0-{maxX}, 0-{maxY})");
            return new GridPosition(-1, -1); // Return invalid position
        }

        int gx = Mathf.FloorToInt(localX / Mathf.Max(0.0001f, 1F));
        int gy = Mathf.FloorToInt(localY / Mathf.Max(0.0001f, 1F));
        Log($"[GameController] ScreenToGrid: screen {screenPos} → world {world} → local (offset) {localX},{localY} → grid {gx},{gy}");
        return new GridPosition(gx, gy);
    }

    string GetColoredTileLogString(Tile tile, GridPosition pos)
    {
        if (tile == null) return "NULL";

        string colorHex = GetTileColorHex(tile.Type);
        return $"<color={colorHex}>{tile.Type}</color>";
    }

    string GetTileColorHex(TileType type)
    {
        switch (type)
        {
            case TileType.Red: return "#FF0000";
            case TileType.Blue: return "#0000FF";
            case TileType.Green: return "#00FF00";
            case TileType.Yellow: return "#FFFF00";
            case TileType.Purple: return "#9932CC";
            case TileType.Orange: return "#FF8800";
            case TileType.RowBooster: return "#FFFFFF";
            default: return "#808080";
        }
    }

    /// <summary>
    /// Performance test for match-clear-gravity-refill cycle.
    /// Press 'P' key in Play mode to run this test.
    /// Target: < 2ms per cycle
    /// </summary>
    void RunPerformanceTest()
    {
        Debug.Log("===== PERFORMANCE TEST START =====");

        // Run 100 test iterations
        const int iterations = 100;
        var times = new List<double>();

        for (int i = 0; i < iterations; i++)
        {
            // Create a fresh test grid
            var testManager = new GridManager(6, 6, randomSeed + i);

            // Force some matches by manually creating a line
            testManager.Grid[0, 0] = new Tile(TileType.Red, new GridPosition(0, 0));
            testManager.Grid[1, 0] = new Tile(TileType.Red, new GridPosition(1, 0));
            testManager.Grid[2, 0] = new Tile(TileType.Red, new GridPosition(2, 0));

            // Measure one complete cycle: FindMatches → Clear → Gravity → Refill
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var matches = testManager.FindMatches();
            testManager.Clear(matches);
            testManager.ApplyGravity();
            testManager.Refill();

            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Calculate statistics
        times.Sort();
        double min = times[0];
        double max = times[times.Count - 1];
        double avg = 0;
        foreach (var t in times) avg += t;
        avg /= times.Count;

        Debug.Log($"[Performance Test] Iterations: {iterations}");
        Debug.Log($"[Performance Test] Min: {min:F4}ms");
        Debug.Log($"[Performance Test] Max: {max:F4}ms");
        Debug.Log($"[Performance Test] Avg: {avg:F4}ms");

        if (max < 2.0)
        {
            Debug.Log("[Performance Test] ✅ PASS - All cycles under 2ms target!");
        }
        else
        {
            Debug.LogWarning($"[Performance Test] ⚠️ FAIL - Max time {max:F4}ms exceeds 2ms target");
        }

        Debug.Log("===== PERFORMANCE TEST END =====");
    }
}
