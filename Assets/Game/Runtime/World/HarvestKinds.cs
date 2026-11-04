using Framework;

/// <summary>채집 종류별 무기 효율 (EHarvestType 마다 한 클래스, 2026-11-01 ResourceNode 의 switch → 다형성).</summary>
public abstract class HarvestKind
{
    public static HarvestKind Of(EHarvestType InType)
    {
        return KindRegistry<HarvestKind>.Get((int)InType);
    }

    /// <summary>InWeapon 으로 이 종류를 칠 때 내구도 감소 배율.</summary>
    public abstract float GetMultiplier(WeaponRow InWeapon);
}

[Kind(EHarvestType.Wood)]
public sealed class WoodHarvest : HarvestKind
{
    public override float GetMultiplier(WeaponRow InWeapon)
    {
        return InWeapon.HarvestWood;
    }
}

[Kind(EHarvestType.Stone)]
public sealed class StoneHarvest : HarvestKind
{
    public override float GetMultiplier(WeaponRow InWeapon)
    {
        return InWeapon.HarvestStone;
    }
}

[Kind(EHarvestType.Plant)]
public sealed class PlantHarvest : HarvestKind
{
    public override float GetMultiplier(WeaponRow InWeapon)
    {
        return InWeapon.HarvestPlant;
    }
}
