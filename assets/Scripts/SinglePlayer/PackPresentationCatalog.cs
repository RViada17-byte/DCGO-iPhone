using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[CreateAssetMenu(menuName = "Single Player/Pack Presentation Catalog", fileName = "PackPresentationCatalog")]
public class PackPresentationCatalog : ScriptableObject
{
    [SerializeField] private PackPresentationTheme defaultTheme = PackPresentationTheme.CreateDefault();
    [SerializeField] private List<PackPresentationEntry> entries = new List<PackPresentationEntry>();

    public PackPresentationTheme Resolve(string productId, string setId)
    {
        string normalizedProductId = NormalizeKey(productId);
        string normalizedSetId = NormalizeKey(setId);

        for (int index = 0; index < entries.Count; index++)
        {
            PackPresentationEntry entry = entries[index];
            if (entry == null || !entry.Matches(normalizedProductId, normalizedSetId))
            {
                continue;
            }

            return entry.Theme != null
                ? entry.Theme.CloneWithFallback(setId)
                : PackPresentationTheme.CreateFallback(setId);
        }

        return defaultTheme != null
            ? defaultTheme.CloneWithFallback(setId)
            : PackPresentationTheme.CreateFallback(setId);
    }

    public static PackPresentationCatalog LoadDefault()
    {
        return Resources.Load<PackPresentationCatalog>("PackPresentationCatalog");
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    [Serializable]
    private sealed class PackPresentationEntry
    {
        public PackPresentationMatchType matchType = PackPresentationMatchType.SetId;
        public string key;
        public PackPresentationTheme Theme = PackPresentationTheme.CreateDefault();

        public bool Matches(string normalizedProductId, string normalizedSetId)
        {
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                return false;
            }

            switch (matchType)
            {
                case PackPresentationMatchType.ProductId:
                    return string.Equals(normalizedProductId, normalizedKey, StringComparison.Ordinal);

                case PackPresentationMatchType.SetId:
                default:
                    return string.Equals(normalizedSetId, normalizedKey, StringComparison.Ordinal);
            }
        }
    }
}

public enum PackPresentationMatchType
{
    ProductId = 0,
    SetId = 1,
}

[Serializable]
public class PackPresentationTheme
{
    public Sprite packArt;
    public Sprite setLogoSprite;
    public Color overlayColor = new Color(0.04f, 0.07f, 0.12f, 0.94f);
    public Color secondaryOverlayColor = new Color(0.01f, 0.02f, 0.05f, 0.92f);
    public Color backgroundTintColor = new Color(0f, 0f, 0f, 0f);
    public Color accentColor = new Color(0.29f, 0.72f, 1f, 1f);
    public Color glowColor = new Color(0.3f, 0.78f, 1f, 0.72f);
    public Color packTint = new Color(0.1f, 0.17f, 0.24f, 1f);
    public Color cardBackColor = new Color(0.08f, 0.12f, 0.18f, 1f);
    public Color summaryPanelColor = new Color(0.08f, 0.11f, 0.16f, 0.96f);
    public Color summaryTextColor = new Color(0.93f, 0.97f, 1f, 1f);
    public Color commonColor = new Color(0.83f, 0.88f, 0.94f, 1f);
    public Color uncommonColor = new Color(0.61f, 0.9f, 0.72f, 1f);
    public Color rareColor = new Color(0.36f, 0.78f, 1f, 1f);
    public Color superRareColor = new Color(0.92f, 0.56f, 0.21f, 1f);
    public Color secretColor = new Color(0.99f, 0.86f, 0.33f, 1f);
    public Color promoColor = new Color(0.89f, 0.52f, 0.95f, 1f);
    public string setLogoText;
    public string introSfxId;
    public string openSfxId;
    public string revealSfxId;
    public string rareRevealSfxId;
    public string summaryConfirmSfxId;

    public PackPresentationTheme Clone()
    {
        return new PackPresentationTheme
        {
            packArt = packArt,
            setLogoSprite = setLogoSprite,
            overlayColor = overlayColor,
            secondaryOverlayColor = secondaryOverlayColor,
            backgroundTintColor = backgroundTintColor,
            accentColor = accentColor,
            glowColor = glowColor,
            packTint = packTint,
            cardBackColor = cardBackColor,
            summaryPanelColor = summaryPanelColor,
            summaryTextColor = summaryTextColor,
            commonColor = commonColor,
            uncommonColor = uncommonColor,
            rareColor = rareColor,
            superRareColor = superRareColor,
            secretColor = secretColor,
            promoColor = promoColor,
            setLogoText = setLogoText,
            introSfxId = introSfxId,
            openSfxId = openSfxId,
            revealSfxId = revealSfxId,
            rareRevealSfxId = rareRevealSfxId,
            summaryConfirmSfxId = summaryConfirmSfxId,
        };
    }

    public PackPresentationTheme CloneWithFallback(string setId)
    {
        PackPresentationTheme clone = Clone();
        PackPresentationTheme fallback = CreateFallback(setId);

        if (clone.packArt == null)
        {
            clone.packArt = fallback.packArt;
        }

        if (clone.setLogoSprite == null)
        {
            clone.setLogoSprite = fallback.setLogoSprite;
        }

        if (clone.overlayColor.a <= 0f)
        {
            clone.overlayColor = fallback.overlayColor;
        }

        if (clone.secondaryOverlayColor.a <= 0f)
        {
            clone.secondaryOverlayColor = fallback.secondaryOverlayColor;
        }

        if (clone.backgroundTintColor.a <= 0f)
        {
            clone.backgroundTintColor = fallback.backgroundTintColor;
        }

        if (clone.accentColor.a <= 0f)
        {
            clone.accentColor = fallback.accentColor;
        }

        if (clone.glowColor.a <= 0f)
        {
            clone.glowColor = fallback.glowColor;
        }

        if (clone.packTint.a <= 0f)
        {
            clone.packTint = fallback.packTint;
        }

        if (clone.cardBackColor.a <= 0f)
        {
            clone.cardBackColor = fallback.cardBackColor;
        }

        if (clone.summaryPanelColor.a <= 0f)
        {
            clone.summaryPanelColor = fallback.summaryPanelColor;
        }

        if (clone.summaryTextColor.a <= 0f)
        {
            clone.summaryTextColor = fallback.summaryTextColor;
        }

        if (string.IsNullOrWhiteSpace(clone.setLogoText))
        {
            clone.setLogoText = fallback.setLogoText;
        }

        return clone;
    }

    public Color GetRarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.U:
                return uncommonColor;

            case Rarity.R:
                return rareColor;

            case Rarity.SR:
                return superRareColor;

            case Rarity.SEC:
                return secretColor;

            case Rarity.P:
                return promoColor;

            case Rarity.C:
            case Rarity.None:
            default:
                return commonColor;
        }
    }

    public bool IsBurstRarity(Rarity rarity)
    {
        return IsBurstRarityStatic(rarity);
    }

    public static bool IsBurstRarityStatic(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.R:
            case Rarity.SR:
            case Rarity.SEC:
            case Rarity.P:
                return true;

            default:
                return false;
        }
    }

    public static PackPresentationTheme CreateDefault()
    {
        return CreateFallback(string.Empty);
    }

    public static PackPresentationTheme CreateFallback(string setId)
    {
        string prefix = NormalizePrefix(setId);
        Color accent;
        Color glow;
        Color packTint;

        switch (prefix)
        {
            case "EX":
                accent = new Color(1f, 0.56f, 0.36f, 1f);
                glow = new Color(1f, 0.67f, 0.36f, 0.8f);
                packTint = new Color(0.24f, 0.11f, 0.12f, 1f);
                break;

            case "ST":
                accent = new Color(0.44f, 0.93f, 0.51f, 1f);
                glow = new Color(0.42f, 0.96f, 0.68f, 0.78f);
                packTint = new Color(0.08f, 0.19f, 0.12f, 1f);
                break;

            case "PR":
            case "P":
                accent = new Color(0.92f, 0.53f, 0.98f, 1f);
                glow = new Color(0.96f, 0.62f, 1f, 0.78f);
                packTint = new Color(0.18f, 0.09f, 0.22f, 1f);
                break;

            case "RB":
                accent = new Color(1f, 0.79f, 0.34f, 1f);
                glow = new Color(1f, 0.88f, 0.47f, 0.8f);
                packTint = new Color(0.2f, 0.16f, 0.08f, 1f);
                break;

            case "BT":
            default:
                accent = new Color(0.31f, 0.78f, 1f, 1f);
                glow = new Color(0.32f, 0.87f, 1f, 0.78f);
                packTint = new Color(0.08f, 0.15f, 0.22f, 1f);
                break;
        }

        return new PackPresentationTheme
        {
            packArt = PackPresentationArtCache.Load(setId),
            setLogoText = NormalizeSetLabel(setId),
            overlayColor = new Color(packTint.r * 0.55f, packTint.g * 0.55f, packTint.b * 0.55f, 0.95f),
            secondaryOverlayColor = new Color(0.01f, 0.02f, 0.05f, 0.94f),
            backgroundTintColor = new Color(0f, 0f, 0f, 0f),
            accentColor = accent,
            glowColor = glow,
            packTint = packTint,
            cardBackColor = Color.Lerp(packTint, Color.black, 0.35f),
            summaryPanelColor = new Color(packTint.r * 0.8f, packTint.g * 0.8f, packTint.b * 0.8f, 0.96f),
            summaryTextColor = new Color(0.95f, 0.97f, 1f, 1f),
            commonColor = new Color(0.84f, 0.88f, 0.94f, 1f),
            uncommonColor = new Color(0.59f, 0.91f, 0.72f, 1f),
            rareColor = accent,
            superRareColor = new Color(0.97f, 0.61f, 0.24f, 1f),
            secretColor = new Color(1f, 0.89f, 0.35f, 1f),
            promoColor = new Color(0.92f, 0.53f, 0.98f, 1f),
        };
    }

    private static string NormalizePrefix(string setId)
    {
        if (string.IsNullOrWhiteSpace(setId))
        {
            return string.Empty;
        }

        string trimmed = setId.Trim().ToUpperInvariant();
        return trimmed.Length >= 2
            ? trimmed.Substring(0, 2)
            : trimmed;
    }

    private static string NormalizeSetLabel(string setId)
    {
        if (string.IsNullOrWhiteSpace(setId))
        {
            return string.Empty;
        }

        string trimmed = setId.Trim().ToUpperInvariant();
        Match match = PackPresentationArtCache.SetCodeRegex.Match(trimmed);
        if (!match.Success)
        {
            return trimmed;
        }

        string prefix = match.Groups["prefix"].Value;
        int number = Mathf.Max(0, int.Parse(match.Groups["number"].Value));
        return $"{prefix}-{number:00}";
    }
}

internal static class PackPresentationArtCache
{
    private static readonly Dictionary<string, Sprite> CachedSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MissingSprites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    internal static readonly Regex SetCodeRegex = new Regex(@"^(?<prefix>[A-Z]+)[\s\-_]?(?<number>\d+)$", RegexOptions.Compiled);
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

    public static Sprite Load(string setId)
    {
        string[] candidates = BuildCandidates(setId);
        for (int index = 0; index < candidates.Length; index++)
        {
            string key = candidates[index];
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (CachedSprites.TryGetValue(key, out Sprite cachedSprite))
            {
                return cachedSprite;
            }

            Sprite loadedSprite = LoadFromDisk(key);
            if (loadedSprite != null)
            {
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    string candidateKey = candidates[candidateIndex];
                    if (!string.IsNullOrEmpty(candidateKey))
                    {
                        CachedSprites[candidateKey] = loadedSprite;
                    }
                }

                return loadedSprite;
            }
        }

        string normalized = NormalizeSetCode(setId);
        if (!string.IsNullOrEmpty(normalized) && MissingSprites.Add(normalized))
        {
            Debug.Log($"{nameof(PackPresentationArtCache)} missing local pack art for {normalized}.");
        }

        return null;
    }

    private static Sprite LoadFromDisk(string normalizedKey)
    {
        string artDirectory = StreamingAssetsUtility.GetStreamingAssetPath("PackArt", false);
        if (string.IsNullOrWhiteSpace(artDirectory) || !Directory.Exists(artDirectory))
        {
            return null;
        }

        for (int extensionIndex = 0; extensionIndex < ImageExtensions.Length; extensionIndex++)
        {
            string filePath = Path.Combine(artDirectory, normalizedKey + ImageExtensions[extensionIndex]);
            if (!File.Exists(filePath))
            {
                continue;
            }

            byte[] bytes = File.ReadAllBytes(filePath);
            Texture2D texture = StreamingAssetsUtility.BinaryToTexture(bytes);
            if (texture == null)
            {
                continue;
            }

            texture.name = normalizedKey;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        return null;
    }

    private static string[] BuildCandidates(string setId)
    {
        string normalized = NormalizeSetCode(setId);
        if (string.IsNullOrEmpty(normalized))
        {
            return Array.Empty<string>();
        }

        string compact = normalized.Replace("-", string.Empty);
        if (string.Equals(compact, normalized, StringComparison.Ordinal))
        {
            return new[] { normalized };
        }

        return new[] { normalized, compact };
    }

    private static string NormalizeSetCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim().ToUpperInvariant();
        Match match = SetCodeRegex.Match(trimmed);
        if (!match.Success)
        {
            return trimmed;
        }

        string prefix = match.Groups["prefix"].Value;
        int number = Mathf.Max(0, int.Parse(match.Groups["number"].Value));
        return $"{prefix}-{number:00}";
    }
}
