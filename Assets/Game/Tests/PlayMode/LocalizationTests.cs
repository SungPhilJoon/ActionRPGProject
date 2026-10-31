using System.Collections;
using Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Unity Localization: Tables.Text 가 현재 언어(GameText 컬렉션, 키 Id = StringTable Index)를 돌려주고,
/// 언어를 바꾸면 LanguageChangedEvent 와 함께 바뀐 문자열이 나온다. 모든 문자열에 한국어가 있다.
/// 끝나면 원래 언어로 되돌린다 (선택이 PlayerPrefs 에 저장되므로).
/// </summary>
public class LocalizationTests : GamePlayTestBase
{
    [UnityTest]
    public IEnumerator SwitchLanguage_ChangesTexts()
    {
        Assert.IsTrue(GameLocalization.IsReady, "Localization 테이블 준비");
        var original = GameLocalization.Language;
        int changed = 0;
        System.Action<LanguageChangedEvent> onChanged = e => changed += 1;
        EventBus<LanguageChangedEvent>.Subscribe(onChanged);
        try
        {
            GameLocalization.SetLanguage(SystemLanguage.Korean);
            yield return WaitFor(() => GameLocalization.Language == SystemLanguage.Korean && Tables.Text(EText.Inventory) == "인벤토리", 5.0f, "한국어 테이블");
            Assert.AreEqual("무료", Tables.Text(EText.Free));

            changed = 0;
            GameLocalization.SetLanguage(SystemLanguage.English);
            yield return WaitFor(() => changed > 0, 5.0f, "LanguageChangedEvent");
            Assert.AreEqual("Inventory", Tables.Text(EText.Inventory), "영어 테이블");
            Assert.AreEqual("Medusa Sentinel", Tables.Text(1001));
        }
        finally
        {
            EventBus<LanguageChangedEvent>.Unsubscribe(onChanged);
            GameLocalization.SetLanguage(original);
        }

        yield return WaitFor(() => GameLocalization.Language == original, 5.0f, "원래 언어 복구");
    }

    [Test]
    public void EveryString_HasKorean()
    {
        foreach (var row in Tables.Root.String.All)
        {
            Assert.IsFalse(string.IsNullOrEmpty(row.Korean), $"StringTable {row.Index} 한국어 없음");
        }
    }
}
