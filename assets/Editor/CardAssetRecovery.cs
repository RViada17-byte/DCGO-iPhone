using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DCGO.CardEntities
{
    public static class CardAssetRecovery
    {
        private const string LoaderAssetPath = "Assets/CardBaseEntity/CardEntity_JSONLoader.asset";
        private const string SourceJsonUrl = "https://raw.githubusercontent.com/TakaOtaku/Digimon-Card-App/main/src/assets/cardlists/DigimonCards.json";

        [MenuItem("DCGO/Card Assets/Recover BT11-025")]
        public static void RecoverBT11_025Menu()
        {
            RecoverCardAsset("BT11-025", withoutImages: false);
        }

        [MenuItem("DCGO/Card Assets/Recover BT9-058")]
        public static void RecoverBT9_058Menu()
        {
            RecoverCardAsset("BT9-058", withoutImages: false);
        }

        public static void RecoverBT11_025Batch()
        {
            RecoverCardAsset("BT11-025", withoutImages: false);
        }

        public static void RecoverBT9_058Batch()
        {
            RecoverCardAsset("BT9-058", withoutImages: false);
        }

        public static void RecoverCardAsset(string cardId, bool withoutImages)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Card id is required.", nameof(cardId));
            }

            LoadJSON_CardEntity loaderAsset = AssetDatabase.LoadAssetAtPath<LoadJSON_CardEntity>(LoaderAssetPath);
            if (loaderAsset == null)
            {
                throw new InvalidOperationException($"Loader asset not found at '{LoaderAssetPath}'.");
            }

            RootObject root = DownloadSourceCards();
            List<CardData> matches = root.cards?
                .Where(card =>
                    card != null &&
                    (string.Equals(card.cardNumber, cardId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(card.id, cardId, StringComparison.OrdinalIgnoreCase)))
                .ToList() ?? new List<CardData>();

            if (matches.Count == 0)
            {
                throw new InvalidOperationException($"No source card found for '{cardId}'.");
            }

            Type editorType = typeof(JSONLoader_CardEntity);
            Editor editor = Editor.CreateEditor(loaderAsset, editorType);
            if (editor == null)
            {
                throw new InvalidOperationException($"Could not create editor of type '{editorType.FullName}'.");
            }

            try
            {
                SetPrivateField(editorType, editor, "_loadJSON", loaderAsset);
                SetPrivateField(editorType, editor, "_cardData", root.cards);
                SetPrivateField(editorType, editor, "onlyAA", false);
                SetPrivateField(editorType, editor, "updateExisting", true);
                SetPrivateField(editorType, editor, "debugMode", false);
                SetPrivateField(editorType, editor, "withoutImages", withoutImages);
                SetPrivateField(editorType, editor, "cardIDString", cardId);

                MethodInfo createMethod = editorType.GetMethod("SetDataToScriptableObject", BindingFlags.Instance | BindingFlags.NonPublic);
                if (createMethod == null)
                {
                    throw new MissingMethodException(editorType.FullName, "SetDataToScriptableObject");
                }

                createMethod.Invoke(editor, new object[] { matches });
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Recovered card asset for {cardId}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        private static RootObject DownloadSourceCards()
        {
            using WebClient client = new WebClient();
            client.Encoding = Encoding.UTF8;
            string json = client.DownloadString(SourceJsonUrl);
            RootObject root = JsonUtility.FromJson<RootObject>("{\"cards\":" + json + "}");
            if (root == null || root.cards == null || root.cards.Count == 0)
            {
                throw new InvalidOperationException("Could not load source card JSON.");
            }

            return root;
        }

        private static void SetPrivateField(Type editorType, object target, string fieldName, object value)
        {
            FieldInfo field = editorType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(editorType.FullName, fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
