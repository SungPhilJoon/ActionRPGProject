using Cysharp.Threading.Tasks;
using Framework;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using LocaleStringTable = UnityEngine.Localization.Tables.StringTable;

/// <summary>
/// Unity Localization 연결부. 게임 문자열은 String Table Collection "GameText"(en/ko)에 있고, 키 Id = StringTable Index 다.
///  - 부트스트랩이 InitializeAsync 로 Localization 초기화 + 현재 언어 테이블을 기다린 뒤 Tables.Text 가 TryGet 으로 읽는다 (동기, int 키).
///  - 언어가 바뀌면 새 테이블을 받아 LanguageChangedEvent 를 발행한다 → 문구를 캐시한 UI 가 다시 채운다.
///  - 시작 언어는 LocalizationSettings 의 Startup Selector (PlayerPrefs 에 저장된 선택 → 없으면 한국어).
/// 언어는 문자열 코드가 아니라 SystemLanguage 로 다룬다.
/// </summary>
public static class GameLocalization
{
    private static LocalizedStringTable Source;
    private static LocaleStringTable Table;
    private static int Version;

    public static bool IsReady
    {
        get { return Table != null; }
    }

    /// <summary>지금 선택된 언어 (준비 전이면 English).</summary>
    public static SystemLanguage Language
    {
        get
        {
            if (IsReady == false || LocalizationSettings.SelectedLocale == null)
            {
                return SystemLanguage.English;
            }

            return toLanguage(LocalizationSettings.SelectedLocale);
        }
    }

    public static async UniTask InitializeAsync(LocalizedStringTable InSource)
    {
        Shutdown();
        if (InSource == null || InSource.IsEmpty == true || LocalizationSettings.HasSettings == false)
        {
            Log.Warning("GameLocalization: GameText table not linked. Run 'Game/Data/Import All CSV' (syncs Unity Localization).");
            return;
        }

        Source = InSource;
        await LocalizationSettings.InitializationOperation.Task.AsUniTask();
        LocalizationSettings.SelectedLocaleChanged += onLocaleChanged;
        await loadTable();
    }

    public static void Shutdown()
    {
        if (Source != null && LocalizationSettings.HasSettings == true)
        {
            LocalizationSettings.SelectedLocaleChanged -= onLocaleChanged;
        }

        Source = null;
        Table = null;
        Version += 1;
    }

    /// <summary>키 Id(= StringTable Index) 의 현재 언어 문자열. 테이블이 없거나 비어 있으면 false (호출 쪽이 CSV 원문으로 대체).</summary>
    public static bool TryGet(int InId, out string OutText)
    {
        OutText = null;
        if (Table == null)
        {
            return false;
        }

        var entry = Table.GetEntry(InId);
        if (entry == null || string.IsNullOrEmpty(entry.Value) == true)
        {
            return false;
        }

        OutText = entry.Value;
        return true;
    }

    /// <summary>InLanguage 로 바꾼다 (프로젝트에 그 Locale 이 없으면 무시). 선택은 PlayerPrefs 에 저장돼 다음 실행에도 유지된다.</summary>
    public static void SetLanguage(SystemLanguage InLanguage)
    {
        if (LocalizationSettings.HasSettings == false)
        {
            return;
        }

        var locale = LocalizationSettings.AvailableLocales.GetLocale(InLanguage);
        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
        }
    }

    /// <summary>사용 가능한 언어를 차례로 돌린다 (타이틀 언어 버튼, 치트 lang).</summary>
    public static void Next()
    {
        if (LocalizationSettings.HasSettings == false)
        {
            return;
        }

        var locales = LocalizationSettings.AvailableLocales.Locales;
        if (locales.Count == 0)
        {
            return;
        }

        int index = locales.IndexOf(LocalizationSettings.SelectedLocale);
        LocalizationSettings.SelectedLocale = locales[(index + 1) % locales.Count];
    }

    private static void onLocaleChanged(Locale InLocale)
    {
        loadTable().Forget();
    }

    private static async UniTask loadTable()
    {
        int version = ++Version;
        var source = Source;
        if (source == null)
        {
            return;
        }

        var table = await LocalizationSettings.StringDatabase.GetTableAsync(source.TableReference).Task.AsUniTask();
        // 기다리는 사이 다시 바뀌었거나 종료됐으면 버린다.
        if (version != Version)
        {
            return;
        }

        Table = table;
        EventBus<LanguageChangedEvent>.Publish(new LanguageChangedEvent());
    }

    private static SystemLanguage toLanguage(Locale InLocale)
    {
        var korean = new LocaleIdentifier(SystemLanguage.Korean);
        return InLocale.Identifier == korean ? SystemLanguage.Korean : SystemLanguage.English;
    }
}
