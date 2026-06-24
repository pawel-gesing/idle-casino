using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DistilleryDiscovery
{
    public sealed class VisualCatalog
    {
        private const int PlaceholderSize = 32;
        private readonly Dictionary<string, VisualDefinition> definitions;
        private readonly Dictionary<string, Sprite> sprites = new();

        public VisualCatalog(GameConfig config)
        {
            definitions = (config?.Visuals ?? new List<VisualDefinition>())
                .Where(x => !string.IsNullOrWhiteSpace(x.id))
                .GroupBy(x => x.id)
                .ToDictionary(x => x.Key, x => x.First());
        }

        public bool Contains(string visualId) => !string.IsNullOrEmpty(visualId) && definitions.ContainsKey(visualId);

        public Sprite Sprite(string visualId)
        {
            if (string.IsNullOrEmpty(visualId)) return null;
            if (sprites.TryGetValue(visualId, out var cached)) return cached;
            if (!definitions.TryGetValue(visualId, out var definition)) return null;

            var sprite = string.IsNullOrWhiteSpace(definition.spriteResource)
                ? null
                : Resources.Load<Sprite>(definition.spriteResource);
            sprite ??= CreatePlaceholderSprite(visualId, Tint(visualId, Color.white));
            sprites[visualId] = sprite;
            return sprite;
        }

        public Color Tint(string visualId, Color fallback)
        {
            if (!definitions.TryGetValue(visualId, out var definition) || string.IsNullOrWhiteSpace(definition.tintHex))
                return fallback;
            return ColorUtility.TryParseHtmlString(definition.tintHex, out var color) ? color : fallback;
        }

        private static Sprite CreatePlaceholderSprite(string visualId, Color tint)
        {
            var texture = new Texture2D(PlaceholderSize, PlaceholderSize, TextureFormat.RGBA32, false)
            {
                name = $"Placeholder.{visualId}",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point
            };
            var border = new Color(Mathf.Min(1f, tint.r + .18f), Mathf.Min(1f, tint.g + .18f), Mathf.Min(1f, tint.b + .18f), .95f);
            var inner = new Color(tint.r * .58f, tint.g * .58f, tint.b * .58f, .95f);
            for (var y = 0; y < PlaceholderSize; y++)
            {
                for (var x = 0; x < PlaceholderSize; x++)
                {
                    var edge = x < 3 || y < 3 || x >= PlaceholderSize - 3 || y >= PlaceholderSize - 3;
                    texture.SetPixel(x, y, edge ? border : inner);
                }
            }
            texture.Apply(false, true);
            return UnityEngine.Sprite.Create(texture, new Rect(0, 0, PlaceholderSize, PlaceholderSize), new Vector2(.5f, .5f), PlaceholderSize);
        }
    }
}
