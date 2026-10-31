using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지도에 고정 표식으로 그릴 오브젝트 목록 (상인, 성 심장, 관 …). 오브젝트가 켜질 때 Add, 꺼질 때 Remove 한다.
/// 플레이어 · 몬스터 · 퀘스트 대상은 PlayerCharacter.Local / ActorRegistry / QuestManager 에서 직접 얻으므로 여기 넣지 않는다.
/// MapView(미니맵 · 월드맵)가 매 프레임 읽는다. FindObjectsOfType 대신 목록을 쓴다 (InteractableRegistry 와 같은 방식).
/// </summary>
public static class MapMarkers
{
    public struct Entry
    {
        public EMapMarker Kind;
        public Transform Target;
    }

    private static readonly List<Entry> Entries = new List<Entry>();

    public static IReadOnlyList<Entry> All
    {
        get { return Entries; }
    }

    public static void Add(EMapMarker InKind, Transform InTarget)
    {
        if (InTarget == null)
        {
            return;
        }

        Remove(InTarget);
        Entries.Add(new Entry { Kind = InKind, Target = InTarget });
    }

    public static void Remove(Transform InTarget)
    {
        for (int i = Entries.Count - 1; i >= 0; --i)
        {
            if (Entries[i].Target == InTarget)
            {
                Entries.RemoveAt(i);
            }
        }
    }

    public static void Clear()
    {
        Entries.Clear();
    }
}
