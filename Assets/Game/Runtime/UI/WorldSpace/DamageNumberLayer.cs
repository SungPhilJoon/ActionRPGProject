using System.Collections.Generic;
using Framework;
using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// 데미지/회복 숫자. 월드 좌표를 매 프레임 스크린 좌표로 옮기는 오버레이 텍스트이며, 텍스트 객체는 직접 풀링한다.
/// </summary>
public class DamageNumberLayer : MonoBehaviour
{
    private class Entry
    {
        public TextMeshProUGUI Text;
        public Vector3 World;
        public float Age;
        public float Lifetime;
    }

    private static readonly Color PhysicalColor = new Color(1.0f, 0.95f, 0.85f);
    private static readonly Color SpellColor = new Color(0.85f, 0.55f, 1.0f);
    private static readonly Color SunColor = new Color(1.0f, 0.7f, 0.2f);
    private static readonly Color CritColor = new Color(1.0f, 0.85f, 0.2f);
    private static readonly Color HealColor = new Color(0.4f, 1.0f, 0.45f);
    private static readonly Color PlayerHurtColor = new Color(1.0f, 0.3f, 0.3f);

    private readonly List<Entry> Active = new List<Entry>();
    private readonly Stack<Entry> Free = new Stack<Entry>();
    private RectTransform Rect;
    private Camera WorldCamera;

    public void Setup(Camera InCamera)
    {
        Rect = GetComponent<RectTransform>();
        WorldCamera = InCamera;
    }

    private void OnEnable()
    {
        EventBus<DamageEvent>.Subscribe(onDamage);
        EventBus<HealEvent>.Subscribe(onHeal);
    }

    private void OnDisable()
    {
        EventBus<DamageEvent>.Unsubscribe(onDamage);
        EventBus<HealEvent>.Unsubscribe(onHeal);
    }

    private void onDamage(DamageEvent InEvent)
    {
        float total = InEvent.Amount + InEvent.Absorbed;
        if (total < 0.5f)
        {
            return;
        }

        Color color = PhysicalColor;
        if (InEvent.Target != null && InEvent.Target.IsPlayer == true)
        {
            color = PlayerHurtColor;
        }
        else if (InEvent.IsCritical == true)
        {
            color = CritColor;
        }
        else if (InEvent.DamageType == EDamageType.Spell)
        {
            color = SpellColor;
        }
        else if (InEvent.DamageType == EDamageType.Sun)
        {
            color = SunColor;
        }

        float size = InEvent.IsCritical == true ? 34.0f : 26.0f;
        spawn(InEvent.Position, Mathf.RoundToInt(total).ToString(), color, size);
    }

    private void onHeal(HealEvent InEvent)
    {
        if (InEvent.Amount < 1.0f)
        {
            return;
        }

        spawn(InEvent.Position, "+" + Mathf.RoundToInt(InEvent.Amount), HealColor, 24.0f);
    }

    private void spawn(Vector3 InWorld, string InText, Color InColor, float InSize)
    {
        Entry entry = Free.Count > 0 ? Free.Pop() : create();
        entry.Text.gameObject.SetActive(true);
        entry.Text.text = InText;
        entry.Text.color = InColor;
        entry.Text.fontSize = InSize;
        entry.World = InWorld + new Vector3(Random.Range(-0.4f, 0.4f), 0.0f, Random.Range(-0.4f, 0.4f));
        entry.Age = 0.0f;
        entry.Lifetime = 1.0f;
        Active.Add(entry);
    }

    private Entry create()
    {
        var entry = new Entry();
        entry.Text = UIFactory.CreateText(transform, "Number", string.Empty, 26.0f, TextAlignmentOptions.Center);
        entry.Text.fontStyle = FontStyles.Bold;
        entry.Text.rectTransform.sizeDelta = new Vector2(200.0f, 50.0f);
        return entry;
    }

    private void LateUpdate()
    {
        if (WorldCamera == null || Rect == null)
        {
            return;
        }

        for (int i = Active.Count - 1; i >= 0; --i)
        {
            var entry = Active[i];
            entry.Age += Time.deltaTime;
            if (entry.Age >= entry.Lifetime)
            {
                entry.Text.gameObject.SetActive(false);
                Active.RemoveAt(i);
                Free.Push(entry);
                continue;
            }

            Vector3 world = entry.World + Vector3.up * entry.Age * 1.5f;
            Vector3 screen = WorldCamera.WorldToScreenPoint(world);
            if (screen.z < 0.0f)
            {
                entry.Text.gameObject.SetActive(false);
                continue;
            }

            entry.Text.gameObject.SetActive(true);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, CanvasUtil.GetEventCamera(Rect), out local);
            entry.Text.rectTransform.anchoredPosition = local;
            var color = entry.Text.color;
            color.a = 1.0f - Mathf.Clamp01((entry.Age - entry.Lifetime * 0.6f) / (entry.Lifetime * 0.4f));
            entry.Text.color = color;
        }
    }
}
