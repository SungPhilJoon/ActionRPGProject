using System;
using System.Collections.Generic;
using Framework.Editor;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using LocaleStringTable = UnityEngine.Localization.Tables.StringTable;

/// <summary>
/// StringTable.csv(Index, Text=영어, Korean) → Unity Localization 동기화 (Game > Data > Sync Localization, CSV 임포트 마지막에 자동).
///  - Localization Settings · Locale(English, Korean) 이 없으면 만든다. 시작 언어 = PlayerPrefs 저장값 → 없으면 한국어. 한국어에 없는 문구는 영어로 대체(Fallback).
///  - String Table Collection "GameText": 키 Id = StringTable Index (코드는 int 로 조회), 키 이름 = EText 이름(있으면) 아니면 Text{Index}.
///    CSV 에 없는 키는 지우고, 값은 CSV 로 덮어쓴다 → 번역은 CSV 에서 고친다 (Localization Tables 창에서 고친 값은 다음 임포트에 덮어써진다).
///  - 테이블은 Preload (초기화 때 함께 로드) — 런타임 Tables.Text 가 동기로 읽는다.
///  - GameTables.TextTable 에 컬렉션 GUID 참조를 넣는다 (런타임은 이름 문자열을 쓰지 않는다).
/// 테이블 · Locale 은 Localization 패키지가 Addressables(Localization-* 그룹)에 등록한다.
/// </summary>
public static class LocalizationSync
{
    public const string Folder = EditorPaths.Root + "/Data/Localization";
    public const string CollectionName = "GameText";
    private const SystemLanguage DefaultLanguage = SystemLanguage.Korean;

    [MenuItem("Game/Data/Sync Localization (StringTable → Unity Localization)", priority = 12)]
    public static void Sync()
    {
        var tables = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
        if (tables == null || tables.String == null)
        {
            Debug.LogWarning("[Localization] GameTables / StringTable missing. Run 'Game/Data/Import All CSV' first.");
            return;
        }

        EditorAssetUtil.EnsureFolder(Folder + "/Locales");
        EditorAssetUtil.EnsureFolder(Folder + "/Tables");
        var settings = ensureSettings();
        var english = ensureLocale(SystemLanguage.English);
        var korean = ensureLocale(SystemLanguage.Korean);
        if (korean.Metadata.GetMetadata<FallbackLocale>() == null)
        {
            korean.Metadata.AddMetadata(new FallbackLocale(english));
            EditorUtility.SetDirty(korean);
        }

        configureStartup(settings);

        var collection = LocalizationEditorSettings.GetStringTableCollection(CollectionName);
        if (collection == null)
        {
            collection = LocalizationEditorSettings.CreateStringTableCollection(CollectionName, Folder + "/Tables");
        }

        var englishTable = getOrAddTable(collection, english);
        var koreanTable = getOrAddTable(collection, korean);
        int count = fill(collection, englishTable, koreanTable, tables.String);

        tables.TextTable = new LocalizedStringTable(collection.SharedData.TableCollectionNameGuid);
        EditorUtility.SetDirty(tables);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Localization] {CollectionName}: {count} keys (en/ko) synced");
    }

    // ------------------------------------------------------------------ settings · locales

    private static LocalizationSettings ensureSettings()
    {
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings != null)
        {
            return settings;
        }

        string path = Folder + "/LocalizationSettings.asset";
        settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }

        LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        return settings;
    }

    private static Locale ensureLocale(SystemLanguage InLanguage)
    {
        var identifier = new LocaleIdentifier(InLanguage);
        var locale = LocalizationEditorSettings.GetLocale(identifier);
        if (locale != null)
        {
            return locale;
        }

        locale = Locale.CreateLocale(InLanguage);
        AssetDatabase.CreateAsset(locale, $"{Folder}/Locales/{InLanguage}.asset");
        LocalizationEditorSettings.AddLocale(locale);
        return locale;
    }

    /// <summary>시작 언어: 플레이어가 고른 언어(PlayerPrefs) → 없으면 DefaultLanguage. 시스템 언어는 보지 않는다 (데모는 한국어 우선).</summary>
    private static void configureStartup(LocalizationSettings InSettings)
    {
        var selectors = InSettings.GetStartupLocaleSelectors();
        selectors.Clear();
        selectors.Add(new PlayerPrefLocaleSelector());
        selectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier(DefaultLanguage) });
        EditorUtility.SetDirty(InSettings);
    }

    private static LocaleStringTable getOrAddTable(StringTableCollection InCollection, Locale InLocale)
    {
        var table = InCollection.GetTable(InLocale.Identifier) as LocaleStringTable;
        if (table == null)
        {
            table = InCollection.AddNewTable(InLocale.Identifier) as LocaleStringTable;
        }

        LocalizationEditorSettings.SetPreloadTableFlag(table, true);
        return table;
    }

    // ------------------------------------------------------------------ entries

    private static int fill(StringTableCollection InCollection, LocaleStringTable InEnglish, LocaleStringTable InKorean, StringTable InSource)
    {
        var shared = InCollection.SharedData;
        var keep = new HashSet<long>();
        var rows = InSource.All;
        for (int i = 0; i < rows.Count; ++i)
        {
            var row = rows[i];
            long id = row.Index;
            keep.Add(id);

            string key = keyName(row.Index);
            var entry = shared.GetEntry(id);
            if (entry == null)
            {
                shared.AddKey(key, id);
            }
            else if (entry.Key != key)
            {
                shared.RenameKey(id, key);
            }

            InEnglish.AddEntry(id, row.Text);
            if (string.IsNullOrEmpty(row.Korean) == true)
            {
                // 번역이 없으면 비워 둔다 → FallbackLocale(영어) 로 표시.
                InKorean.RemoveEntry(id);
            }
            else
            {
                InKorean.AddEntry(id, row.Korean);
            }
        }

        var stale = new List<long>();
        foreach (var entry in shared.Entries)
        {
            if (keep.Contains(entry.Id) == false)
            {
                stale.Add(entry.Id);
            }
        }

        foreach (long id in stale)
        {
            shared.RemoveKey(id);
            InEnglish.RemoveEntry(id);
            InKorean.RemoveEntry(id);
        }

        EditorUtility.SetDirty(shared);
        EditorUtility.SetDirty(InEnglish);
        EditorUtility.SetDirty(InKorean);
        return keep.Count;
    }

    private static string keyName(int InIndex)
    {
        return Enum.IsDefined(typeof(EText), InIndex) == true ? ((EText)InIndex).ToString() : $"Text{InIndex}";
    }
}

/// <summary>StringTable.csv 를 저장하면 (Framework TableImportPostprocessor 가 테이블을 다시 변환한 뒤) Localization 도 다시 맞춘다.</summary>
public class LocalizationSyncPostprocessor : AssetPostprocessor
{
    private const string StringCsv = EditorPaths.Csv + "/StringTable.csv";

    private static void OnPostprocessAllAssets(string[] InImported, string[] InDeleted, string[] InMoved, string[] InMovedFrom)
    {
        if (Array.IndexOf(InImported, StringCsv) < 0)
        {
            return;
        }

        EditorApplication.delayCall += LocalizationSync.Sync;
    }
}
