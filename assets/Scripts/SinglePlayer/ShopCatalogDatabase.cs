using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ShopCatalogDatabase
{
    private const string ShopCatalogFileName = "shop_catalog.json";

    private static ShopCatalogDatabase _instance;

    public static ShopCatalogDatabase Instance => _instance ??= new ShopCatalogDatabase();

    private readonly List<ShopProductDef> _products = new List<ShopProductDef>();
    private readonly Dictionary<string, ShopProductDef> _productsById = new Dictionary<string, ShopProductDef>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ShopProductDef> Products => _products;

    public ShopCatalogDatabase()
    {
        Load();
    }

    public ShopProductDef GetProduct(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return null;
        }

        _productsById.TryGetValue(productId.Trim(), out ShopProductDef product);
        return product;
    }

    public void Reload()
    {
        Load();
    }

    private void Load()
    {
        _products.Clear();
        _productsById.Clear();

        string json = TryReadCatalogJson();
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            ShopCatalogDef catalog = JsonUtility.FromJson<ShopCatalogDef>(json);
            if (catalog?.products == null)
            {
                return;
            }

            for (int index = 0; index < catalog.products.Length; index++)
            {
                ShopProductDef product = catalog.products[index];
                if (product == null || string.IsNullOrWhiteSpace(product.id))
                {
                    continue;
                }

                _products.Add(product);
                if (!_productsById.ContainsKey(product.id))
                {
                    _productsById.Add(product.id, product);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"ShopCatalogDatabase: Failed to parse {ShopCatalogFileName}. {exception.Message}");
        }
    }

    private static string TryReadCatalogJson()
    {
        List<string> candidatePaths = GetCandidateCatalogPaths();
        for (int index = 0; index < candidatePaths.Count; index++)
        {
            string path = candidatePaths[index];

            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ShopCatalogDatabase: Failed to read '{path}'. {exception.Message}");
            }
        }

        return string.Empty;
    }

    private static List<string> GetCandidateCatalogPaths()
    {
        List<string> paths = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPath(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            string fullPath = NormalizePath(Path.Combine(root, ShopCatalogFileName));
            if (seen.Add(fullPath))
            {
                paths.Add(fullPath);
            }
        }

        AddPath(StreamingAssetsUtility.GetStreamingAssetPath("", false));
        AddPath(Application.streamingAssetsPath);
        AddPath(Application.persistentDataPath);

#if UNITY_EDITOR
        AddPath(Path.Combine(Application.dataPath, "StreamingAssets"));
#endif

        return paths;
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/");
    }
}
