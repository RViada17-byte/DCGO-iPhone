using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

public class StreamingAssetsUtility
{
    static Sprite _cachedPlaceholderCardSprite;
    static readonly string[] CardImageExtensions = { ".png", ".jpg", ".webp" };
    static readonly Regex ParallelSuffixRegex = new Regex(@"([_-])P\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex ErrataSuffixRegex = new Regex(@"([_-])ERRATA$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly HashSet<string> LoggedMissingCardImageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static async Task<byte[]> ReadFile(string path)
    {
        using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            var resultBytes = new byte[fileStream.Length];
            await fileStream.ReadAsync(resultBytes, 0, (int)fileStream.Length);
            return resultBytes;
        }
    }

    public static Texture2D BinaryToTexture(byte[] bytes)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        return texture;
    }

    static Sprite CreatePlaceholderCardSprite()
    {
        if (_cachedPlaceholderCardSprite != null)
        {
            return _cachedPlaceholderCardSprite;
        }

        if (ContinuousController.instance != null && ContinuousController.instance.ReverseCard != null)
        {
            _cachedPlaceholderCardSprite = ContinuousController.instance.ReverseCard;
            return _cachedPlaceholderCardSprite;
        }

        Sprite resourcePlaceholder = Resources.Load<Sprite>("Placeholders/EmptyCard");
        if (resourcePlaceholder != null)
        {
            _cachedPlaceholderCardSprite = resourcePlaceholder;
            return _cachedPlaceholderCardSprite;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Color32 fallback = new Color32(30, 30, 30, 255);
        texture.SetPixels32(new[] { fallback, fallback, fallback, fallback });
        texture.Apply(false, false);
        _cachedPlaceholderCardSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
        return _cachedPlaceholderCardSprite;
    }

    public static async Task<Sprite> GetSprite(string fileName, bool isCard = false, bool isLauncher = false)
    {
        if (isCard)
        {
            foreach (string texturesPath in GetReadableAssetPaths("Textures", isLauncher))
            {
                string cardDir = NormalizePath(Path.Combine(texturesPath, "Card"));

                foreach (string candidate in GetCardCandidatePaths(cardDir, fileName))
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    Sprite localSprite = await GetCardImageDataLocal(candidate);
                    if (localSprite != null)
                    {
                        return localSprite;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(fileName) && LoggedMissingCardImageNames.Add(fileName))
            {
                Debug.LogWarning($"[StreamingAssetsUtility] Card image not found for '{fileName}'. Using placeholder.");
            }

            return CreatePlaceholderCardSprite();
        }

        return await GetSpriteImage(fileName, isLauncher);
    }

    public static async Task<Sprite> GetSpriteImage(string fileName, bool isLauncher = false)
    {
        foreach (string texturesPath in GetReadableAssetPaths("Textures", isLauncher))
        {
            string jpgPath = NormalizePath(Path.Combine(texturesPath, $"{fileName}.jpg"));
            string pngPath = NormalizePath(Path.Combine(texturesPath, $"{fileName}.png"));

            string path = File.Exists(jpgPath) ? jpgPath : pngPath;

            if (!File.Exists(path))
            {
                continue;
            }

            byte[] imageBuff = await ReadFile(path);
            Texture2D tex = BinaryToTexture(imageBuff);
            if (tex == null)
            {
                continue;
            }

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
        }

        return null;
    }

    public static async Task<Sprite> GetTokenImageData(string path)
    {
        if (File.Exists(path))
        {
            byte[] imageBuff = await ReadFile(path);
            Texture2D tex = BinaryToTexture(imageBuff);
            if (tex == null)
            {
                return null;
            }

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
        }

        return null;
    }

    public static async Task<Sprite> GetCardImageDataLocal(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] imageBuff = await ReadFile(path);
        Texture2D texture = BinaryToTexture(imageBuff);
        if (texture == null)
        {
            return null;
        }

        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
    }

    public static async Task<Sprite> GetCardImageData(string fileName, string filePath)
    {
        await Task.Yield();
        return CreatePlaceholderCardSprite();
    }

    public static bool IsCardExists(CEntity_Base cEntity_Base)
    {
        foreach (string texturesPath in GetReadableAssetPaths("Textures", false))
        {
            string cardDir = NormalizePath(Path.Combine(texturesPath, "Card"));

            foreach (string candidate in GetCardCandidatePaths(cardDir, cEntity_Base.CardSpriteName))
            {
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static string GetText(string fileName)
    {
        foreach (string basePath in GetReadableAssetPaths("", false))
        {
            string path = NormalizePath(Path.Combine(basePath, $"{fileName}.txt"));
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        return "";
    }

    public static string GetStreamingAssetPath(string subPath, bool isLauncher)
    {
        foreach (string path in GetReadableAssetPaths(subPath, isLauncher))
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return GetLegacyStreamingAssetPath(subPath, isLauncher);
    }

    public static string GetWritablePersistentPath(string subPath)
    {
        string path = Path.Combine(Application.persistentDataPath, subPath ?? "");
        path = NormalizePath(path);

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return path;
    }

    static List<string> GetReadableAssetPaths(string subPath, bool isLauncher)
    {
        List<string> paths = new List<string>();
        HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string normalized = NormalizePath(path);
            if (seenPaths.Add(normalized))
            {
                paths.Add(normalized);
            }
        }

        AddPath(Path.Combine(Application.streamingAssetsPath, subPath ?? ""));
        AddPath(Path.Combine(Application.persistentDataPath, subPath ?? ""));
        AddPath(GetLegacyStreamingAssetPath(subPath, isLauncher));

#if UNITY_EDITOR
        string projectAssetsPath = Path.GetFullPath(Path.Combine(Application.dataPath, subPath ?? ""));
        AddPath(projectAssetsPath);
#endif

        return paths;
    }

    static string GetLegacyStreamingAssetPath(string subPath, bool isLauncher)
    {
        if (isLauncher)
        {
            string path = Application.streamingAssetsPath;
            path = GetOneUpperDirectoryPath(path);
            path = Path.Combine(path, $"Assets/{subPath}").Replace("\\", "/");
            return path;
        }

        string nonLauncherPath = Application.streamingAssetsPath;
        nonLauncherPath = GetOneUpperDirectoryPath(nonLauncherPath);
        nonLauncherPath = GetOneUpperDirectoryPath(nonLauncherPath);
        nonLauncherPath = Path.Combine(nonLauncherPath, $"Assets/{subPath}").Replace("\\", "/");
        return nonLauncherPath;
    }

    static IEnumerable<string> GetCardCandidatePaths(string cardDir, string fileName)
    {
        foreach (string candidateName in GetCardNameCandidates(fileName))
        {
            foreach (string ext in CardImageExtensions)
            {
                yield return NormalizePath(Path.Combine(cardDir, $"{candidateName}{ext}"));
            }
        }
    }

    static IEnumerable<string> GetCardNameCandidates(string fileName)
    {
        HashSet<string> candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                candidates.Add(value.Trim());
            }
        }

        AddCandidate(fileName);
        AddCandidate(fileName?.Replace("_", "-"));
        AddCandidate(fileName?.Replace("-", "_"));
        AddCandidate(fileName?.ToUpperInvariant());
        AddCandidate(fileName?.ToLowerInvariant());

        string[] seed = new string[candidates.Count];
        candidates.CopyTo(seed);

        foreach (string value in seed)
        {
            string withoutParallel = ParallelSuffixRegex.Replace(value, "");
            AddCandidate(withoutParallel);
            AddCandidate(withoutParallel.Replace("_", "-"));
            AddCandidate(withoutParallel.Replace("-", "_"));

            string withoutErrata = ErrataSuffixRegex.Replace(withoutParallel, "");
            AddCandidate(withoutErrata);
            AddCandidate(withoutErrata.Replace("_", "-"));
            AddCandidate(withoutErrata.Replace("-", "_"));
        }

        return candidates;
    }

    static string NormalizePath(string path)
    {
        return (path ?? "").Replace("\\", "/");
    }

    static string GetOneUpperDirectoryPath(string path)
    {
        if (String.IsNullOrEmpty(path))
        {
            return "";
        }

        path = path.Replace("\\", "/");
        if (!path.Contains("/"))
        {
            return path;
        }

        path = path.Substring(0, path.LastIndexOf("/") + 1);

        if (path.Length >= 1 && path[path.Length - 1] == '/')
        {
            path = path.Substring(0, path.LastIndexOf("/"));
        }

        return path.Substring(0, path.LastIndexOf("/") + 1);
    }
}
