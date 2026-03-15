using System.IO;
using UnityEditor;
using UnityEngine;

public static class GameSaveDebugUtility
{
    [MenuItem("DCGO/Save/Print Canonical Save Info")]
    public static void PrintCanonicalSaveInfo()
    {
        string savePath = GameSaveManager.CanonicalSavePath;
        bool exists = File.Exists(savePath);
        Debug.Log($"[GameSave] editor utility path={savePath} exists={exists}");
        GameSaveManager.LogCanonicalSaveStatus("editor utility");
    }
}
