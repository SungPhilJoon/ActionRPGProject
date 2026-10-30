using System.Collections.Generic;
using Framework.Editor;
using UnityEngine;

/// <summary>
/// 어빌리티 타입별 "처리 방식" 설명서. GameAbilities.cs 의 각 구현이 AbilityTable 컬럼을 어떤 의미로 쓰는지와
/// 실행 타임라인(타격/이동 잠금/종료 시점)을 툴에 보여주기 위한 데이터다.
/// 구현을 바꾸면 이 파일도 같이 고친다 (Tool.md 1.6 참고).
/// </summary>
public static class AbilityGuide
{
    public struct Field
    {
        public string Column;
        public string Label;
        public string Tip;

        public Field(string InColumn, string InLabel, string InTip)
        {
            Column = InColumn;
            Label = InLabel;
            Tip = InTip;
        }
    }

    public struct Marker
    {
        public float Time;
        public string Label;
        public Color Color;
    }

    public static readonly Color HitColor = new Color(1.0f, 0.35f, 0.3f);
    public static readonly Color LockColor = new Color(0.4f, 0.7f, 1.0f);
    public static readonly Color EndColor = new Color(0.85f, 0.85f, 0.85f);
    public static readonly Color EffectColor = new Color(0.5f, 0.9f, 0.5f);

    /// <summary>OnActivate 부터 종료까지의 처리 순서 (한국어 설명).</summary>
    public static string Flow(EAbilityType InType)
    {
        switch (InType)
        {
            case EAbilityType.MeleeCombo:
                return "1) 조준 방향으로 회전, MoveLock 동안 이동 잠금, Anims[콤보 단계] 재생\n" +
                       "2) CastTime 뒤 부채꼴(Range, Angle) 안의 적에게 피해 + HitEffects, CastCue/HitCue\n" +
                       "3) MoveLock 뒤 종료. ComboWindow 안에 다시 발동하면 다음 단계 (Count 단계, 마지막 단계 피해 ×1.5)";
            case EAbilityType.Projectile:
                return "1) 조준 방향으로 회전, MoveLock 동안 이동 잠금, Anims[0] 재생\n" +
                       "2) CastTime 뒤 발사체 Count 발을 Spread 각도로 발사 (Speed 속도, Range 사거리, Radius 판정)\n" +
                       "3) max(MoveLock, CastTime) 뒤 종료. 피해는 발사체가 맞을 때 (발사 후 거리/속도만큼 늦음)";
            case EAbilityType.Dash:
                return "1) Anims[0] 재생, 조준 방향으로 Range 만큼 CastTime 동안 대시\n" +
                       "2) Damage > 0 이면 대시 중 경로(Radius)의 적에게 1회씩 피해\n" +
                       "3) CastTime 뒤 SelfEffects 부여 후 종료 (공격 속도 영향 없음)";
            case EAbilityType.AreaBurst:
                return "1) 조준 방향으로 회전, Anims[0] 재생, MoveLock 동안 이동 잠금\n" +
                       "2) CastTime 뒤 원형(Radius) 피해. 중심 = Range > 0 이면 조준 지점(최대 Range), 0 이면 자신\n" +
                       "3) Count 번 반복 (간격 Spread 초), 마지막 타격 뒤 SelfEffects 부여 후 종료";
            case EAbilityType.SelfBuff:
                return "1) Anims[0] 재생, MoveLock 동안 이동 잠금\n" +
                       "2) CastTime 뒤 SelfEffects 부여 (보호막/가속 등), CastCue\n" +
                       "3) max(CastTime, MoveLock) 뒤 종료 (공격 속도 영향 없음)";
            case EAbilityType.Leap:
                return "1) 조준 방향으로 회전, Anims[0] 재생, 조준 지점까지(최대 Range) CastTime 동안 도약\n" +
                       "2) 착지(CastTime) 순간 Radius 원형 피해\n" +
                       "3) 착지 후 MoveLock 만큼 경직, CastTime + MoveLock 뒤 종료";
            case EAbilityType.Feed:
                return "1) Range 안에서 체력 비율이 낮은 적을 찾아 기절시키고 Anims[0] 재생\n" +
                       "2) ConstTable.FeedDuration 동안 채널링 → 대상 처치 + 혈액 획득 (플레이어 전용)";
            default:
                return string.Empty;
        }
    }

    /// <summary>이 타입이 쓰는 형태/타이밍 컬럼 (공통 컬럼 Cooldown/Damage/Effects/Anims 제외).</summary>
    public static Field[] Fields(EAbilityType InType)
    {
        switch (InType)
        {
            case EAbilityType.MeleeCombo:
                return new[]
                {
                    new Field("CastTime", "타격 시점 (초)", "발동 후 부채꼴 판정까지. 애니메이션에서 무기가 닿는 프레임에 맞춘다."),
                    new Field("MoveLock", "동작 길이 (초)", "이동 잠금 시간이자 어빌리티 종료 시점."),
                    new Field("Range", "부채꼴 반경", "판정 거리 (+ 대상 반지름 0.3 안쪽은 각도 무시)."),
                    new Field("Angle", "부채꼴 각도", "전체 각도 (도)."),
                    new Field("Count", "콤보 단계 수", "Anims 를 이 순서로 순환. 마지막 단계는 피해 ×1.5."),
                    new Field("ComboWindow", "콤보 유예 (초)", "종료 후 이 시간 안에 다시 발동하면 다음 단계."),
                };
            case EAbilityType.Projectile:
                return new[]
                {
                    new Field("CastTime", "발사 시점 (초)", "발동 후 발사까지."),
                    new Field("MoveLock", "이동 잠금 (초)", "종료 = max(MoveLock, CastTime)."),
                    new Field("Count", "발사 수", ""),
                    new Field("Spread", "퍼짐 각도", "Count > 1 일 때 부채꼴 전체 각도."),
                    new Field("Speed", "탄속", "m/s"),
                    new Field("Range", "사거리", "AI 는 이 거리 안에서 사용."),
                    new Field("Radius", "판정 반지름", ""),
                    new Field("Projectile", "발사체 (AssetTable)", "발사체 프리팹 AssetTable Index."),
                };
            case EAbilityType.Dash:
                return new[]
                {
                    new Field("Range", "대시 거리", ""),
                    new Field("CastTime", "대시 시간 (초)", "이동 시간이자 종료 시점."),
                    new Field("Radius", "경로 피해 반지름", "Damage > 0 일 때만."),
                };
            case EAbilityType.AreaBurst:
                return new[]
                {
                    new Field("CastTime", "첫 타격 (초)", ""),
                    new Field("Radius", "반지름", ""),
                    new Field("Range", "조준 거리", "0 이면 자신 중심 (AI 사용 거리 = Radius)."),
                    new Field("Count", "타격 횟수", ""),
                    new Field("Spread", "타격 간격 (초)", "최소 0.1."),
                    new Field("MoveLock", "이동 잠금 (초)", ""),
                };
            case EAbilityType.SelfBuff:
                return new[]
                {
                    new Field("CastTime", "효과 시점 (초)", "SelfEffects 부여 시점."),
                    new Field("MoveLock", "이동 잠금 (초)", "종료 = max(CastTime, MoveLock) + 0.05."),
                };
            case EAbilityType.Leap:
                return new[]
                {
                    new Field("Range", "최대 도약 거리", ""),
                    new Field("CastTime", "체공 시간 (초)", "착지 = 타격 시점."),
                    new Field("Radius", "착지 반경", ""),
                    new Field("MoveLock", "착지 후 경직 (초)", "종료 = CastTime + MoveLock."),
                };
            case EAbilityType.Feed:
                return new[]
                {
                    new Field("Range", "흡혈 거리", ""),
                };
            default:
                return new Field[0];
        }
    }

    /// <summary>
    /// 실행 타임라인 (공격 속도 배율 1 기준, 초). 몬스터는 AttackSpeed 속성이 기본 0 이라 배율 1 이다.
    /// MeleeCombo/Projectile/AreaBurst 는 공격 속도가 오르면 CastTime·MoveLock 이 그만큼 짧아진다.
    /// </summary>
    public static List<Marker> Timeline(EAbilityType InType, CsvRow InRow, out float OutEnd)
    {
        var markers = new List<Marker>();
        float cast = InRow.GetFloat("CastTime");
        float moveLock = InRow.GetFloat("MoveLock");
        OutEnd = 0.0f;
        switch (InType)
        {
            case EAbilityType.MeleeCombo:
                markers.Add(marker(cast, "타격", HitColor));
                OutEnd = moveLock;
                break;
            case EAbilityType.Projectile:
                markers.Add(marker(cast, "발사", HitColor));
                markers.Add(marker(moveLock, "이동 잠금 끝", LockColor));
                OutEnd = Mathf.Max(moveLock, cast);
                break;
            case EAbilityType.Dash:
                markers.Add(marker(cast, "대시 끝 / SelfEffects", EffectColor));
                OutEnd = cast;
                break;
            case EAbilityType.AreaBurst:
            {
                int count = Mathf.Max(1, InRow.GetInt("Count"));
                float interval = Mathf.Max(0.1f, InRow.GetFloat("Spread"));
                for (int i = 0; i < count; ++i)
                {
                    markers.Add(marker(cast + i * interval, count > 1 ? $"타격 {i + 1}" : "타격", HitColor));
                }

                markers.Add(marker(moveLock, "이동 잠금 끝", LockColor));
                OutEnd = cast + (count - 1) * interval + 0.1f;
                break;
            }
            case EAbilityType.SelfBuff:
                markers.Add(marker(cast, "SelfEffects", EffectColor));
                markers.Add(marker(moveLock, "이동 잠금 끝", LockColor));
                OutEnd = Mathf.Max(cast, moveLock) + 0.05f;
                break;
            case EAbilityType.Leap:
                markers.Add(marker(cast, "착지 타격", HitColor));
                OutEnd = cast + moveLock;
                markers.Add(marker(OutEnd, "경직 끝", LockColor));
                break;
        }

        markers.Add(marker(OutEnd, "종료", EndColor));
        return markers;
    }

    /// <summary>첫 피해가 들어가는 예상 시점 (발사체는 비행 시간 제외). 피해가 없는 타입이면 -1.</summary>
    public static float FirstHit(EAbilityType InType, CsvRow InRow)
    {
        switch (InType)
        {
            case EAbilityType.MeleeCombo:
            case EAbilityType.Projectile:
            case EAbilityType.AreaBurst:
            case EAbilityType.Leap:
                return InRow.GetFloat("CastTime");
            case EAbilityType.Dash:
                return InRow.GetFloat("Damage") > 0.0f ? 0.0f : -1.0f;
            default:
                return -1.0f;
        }
    }

    /// <summary>MonsterBrain.GetAbilityRange 와 같은 규칙: AI 가 이 거리 안에서만 이 어빌리티를 고른다.</summary>
    public static float AiRange(EAbilityType InType, CsvRow InRow, float InAttackRange)
    {
        float range = InRow.GetFloat("Range");
        if (InType == EAbilityType.AreaBurst && range <= 0.0f)
        {
            return InRow.GetFloat("Radius");
        }

        return range > 0.0f ? range : InAttackRange;
    }

    private static Marker marker(float InTime, string InLabel, Color InColor)
    {
        return new Marker { Time = InTime, Label = InLabel, Color = InColor };
    }
}
