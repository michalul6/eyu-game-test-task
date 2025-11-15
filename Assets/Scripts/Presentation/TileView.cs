using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class TileView : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;
    [Header("Highlighting")]
    [Tooltip("Multiply color by this factor when highlighted (use < 1 to darken).")]
    [SerializeField] float highlightDarkenFactor = 0.7f;

    static Sprite defaultWhiteSprite;
    Color baseColor = Color.white;
    bool isHighlighted;

    public Tile BoundTile { get; private set; }

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureDefaultSprite();
    }

    public void Bind(Tile tile)
    {
        BoundTile = tile;
    }

    public void SetColor(Color color)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureDefaultSprite();
        baseColor = color;
        spriteRenderer.color = isHighlighted ? GetHighlightedColor(color) : color;
    }

    public void SetHighlight(bool highlighted)
    {
        isHighlighted = highlighted;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = highlighted ? GetHighlightedColor(baseColor) : baseColor;
        }
    }

    public void SetPosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;
    }

    public async UniTask AnimateMoveTo(Vector3 targetPosition, float duration, CancellationToken ct = default)
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, targetPosition, t);
            await UniTask.Yield(ct);
        }
        transform.position = targetPosition;
    }

    public async UniTask AnimateScaleTo(Vector3 targetScale, float duration, CancellationToken ct = default)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            await UniTask.Yield(ct);
        }
        transform.localScale = targetScale;
    }

    public async UniTask AnimateRemoval(float duration, CancellationToken ct = default)
    {
        await AnimateScaleTo(Vector3.zero, duration, ct);
    }

    void EnsureDefaultSprite()
    {
        if (spriteRenderer == null) return;
        if (spriteRenderer.sprite == null)
        {
            if (defaultWhiteSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                defaultWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            }
            spriteRenderer.sprite = defaultWhiteSprite;
        }
    }

    Color GetHighlightedColor(Color color)
    {
        float f = Mathf.Clamp01(highlightDarkenFactor);
        return new Color(color.r * f, color.g * f, color.b * f, color.a);
    }
}
