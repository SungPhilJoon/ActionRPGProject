using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// 햇빛 노출 판정. 낮 동안 태양 방향으로 레이를 쏴 막히지 않으면 SunBurn(주기 피해) 이펙트를 건다.
/// 노출 시간이 길수록 피해가 SunDamageRampSeconds 에 걸쳐 0→100% 로 올라간다 (V Rising 의 서서히 타들어감).
/// 그늘 판정은 그림자 맵이 아니라 물리 레이캐스트 (SunBlocker/Structure/Ground 레이어) — URP.md 참고.
/// 그림자를 드리우는 메시에는 빌더가 SunBlocker 레이어 MeshCollider(ShadeColliderUtil)를 붙여, 보이는 그림자와 판정 형태를 맞춘다.
/// </summary>
public class SunExposure : MonoBehaviour
{
    private const float CheckInterval = 0.15f;
    /// <summary>먼 산·언덕 그림자까지 포함 (그림자 거리 90m 보다 길게).</summary>
    private const float RayLength = 400.0f;
    private const float RayStartOffset = 0.05f;
    private const float GizmoLength = 15.0f;
    /// <summary>판정 지점 높이 (발밑 / 몸통).</summary>
    private static readonly float[] SampleHeights = { 0.2f, 1.0f };

    private Actor Owner;
    private float CheckTimer;
    private float ExposureSeconds;
    private int BurnHandle;
    private GameplayEffectSpec BurnSpec;

    public bool IsExposed { get; private set; }

    public float Exposure01
    {
        get
        {
            float ramp = Mathf.Max(0.1f, Tables.Const(EConst.SunDamageRampSeconds));
            return Mathf.Clamp01(ExposureSeconds / ramp);
        }
    }

    public void Initialize(Actor InOwner)
    {
        Owner = InOwner;
    }

    private void Update()
    {
        if (Owner == null || Owner.IsDead == true)
        {
            setExposed(false);
            ExposureSeconds = 0.0f;
            return;
        }

        CheckTimer -= Time.deltaTime;
        if (CheckTimer <= 0.0f)
        {
            CheckTimer = CheckInterval;
            setExposed(checkSunlight());
        }

        if (IsExposed == true)
        {
            ExposureSeconds += Time.deltaTime;
            if (BurnSpec != null)
            {
                BurnSpec.SetByCaller((int)ESetByCaller.Magnitude, Exposure01);
            }
        }
        else
        {
            ExposureSeconds = Mathf.Max(0.0f, ExposureSeconds - Time.deltaTime * 2.0f);
        }
    }

    private bool checkSunlight()
    {
        var dayNight = Managers.Get<DayNightManager>();
        if (dayNight == null || dayNight.IsSunUp == false)
        {
            return false;
        }

        if (Owner.Asc.Tags.HasTag((int)ETag.Cheat_God) == true)
        {
            return false;
        }

        // 화면의 그림자는 발밑 지면에 보이므로 발 높이와 몸통 높이를 둘 다 검사하고, 하나라도 가려지면 그늘로 본다.
        Vector3 toSun = dayNight.DirectionToSun;
        for (int i = 0; i < SampleHeights.Length; ++i)
        {
            if (isBlocked(getSampleOrigin(i, toSun), toSun) == true)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 getSampleOrigin(int InIndex, Vector3 InToSun)
    {
        // 시작점을 태양 쪽으로 멀리 띄우면 벽에 붙어 있을 때 벽 너머에서 레이가 시작돼 그늘을 놓친다 → 아주 조금만 띄운다 (Player 레이어는 마스크에 없음).
        return Owner.transform.position + Vector3.up * SampleHeights[InIndex] + InToSun * RayStartOffset;
    }

    private static bool isBlocked(Vector3 InOrigin, Vector3 InToSun)
    {
        return Physics.Raycast(InOrigin, InToSun, RayLength, LayerId.SunBlockMask, QueryTriggerInteraction.Collide);
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        var dayNight = Managers.Get<DayNightManager>();
        if (Owner == null || dayNight == null)
        {
            return;
        }

        Vector3 toSun = dayNight.DirectionToSun;
        for (int i = 0; i < SampleHeights.Length; ++i)
        {
            Vector3 origin = getSampleOrigin(i, toSun);
            Gizmos.color = isBlocked(origin, toSun) == true ? Color.cyan : Color.yellow;
            Gizmos.DrawLine(origin, origin + toSun * GizmoLength);
        }
    }

#endif

    private void setExposed(bool InExposed)
    {
        if (IsExposed == InExposed)
        {
            return;
        }

        IsExposed = InExposed;
        if (InExposed == true)
        {
            BurnSpec = new GameplayEffectSpec(EffectLibrary.Get(EffectLibrary.SunBurnEffectId), null, 0);
            BurnSpec.SetByCaller((int)ESetByCaller.Magnitude, Exposure01);
            BurnHandle = Owner.Asc.ApplyGameplayEffectSpecToSelf(BurnSpec);
        }
        else
        {
            if (BurnHandle != 0 && Owner != null)
            {
                Owner.Asc.RemoveActiveEffect(BurnHandle);
            }

            BurnHandle = 0;
            BurnSpec = null;
        }
    }
}
