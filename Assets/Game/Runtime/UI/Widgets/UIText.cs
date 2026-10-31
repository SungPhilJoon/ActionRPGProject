using Framework;
using TMPro;
using UnityEngine;

/// <summary>
/// StringTable 고정 문구 (창 제목, 버튼 이름 등). 프리팹에 TextId 만 저장하고 켜질 때 · 언어가 바뀔 때 현재 언어 문자열로 바꾼다.
/// 디자이너는 인스펙터에서 TextId 만 바꾸면 된다 (프리팹에 미리 보이는 문구는 굽던 시점의 값).
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class UIText : BaseBehaviour
{
    [Tooltip("StringTable.csv Index (EText)")]
    public int TextId;

    private void OnEnable()
    {
        EventBus<LanguageChangedEvent>.Subscribe(onLanguageChanged);
        Apply();
    }

    private void OnDisable()
    {
        EventBus<LanguageChangedEvent>.Unsubscribe(onLanguageChanged);
    }

    private void onLanguageChanged(LanguageChangedEvent InEvent)
    {
        Apply();
    }

    public void Apply()
    {
        if (TextId == 0 || Tables.IsReady == false)
        {
            return;
        }

        GetComponent<TextMeshProUGUI>().text = Tables.Text(TextId);
    }
}
