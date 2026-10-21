using Framework;
using UnityEngine;

/// <summary>
/// 모든 데이터 테이블을 묶는 루트 에셋. Addressables 로 이 에셋 하나만 로드하면
/// 직렬화 참조로 연결된 테이블들이 함께 로드된다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Tables/GameTables")]
public class GameTables : ScriptableObject
{
    public ConstTable Const;
    public TagTable Tag;
    public StringTable String;
    public AssetTable Asset;
    public CharacterTable Character;
    public AbilityTable Ability;
    public EffectTable Effect;
    public ItemTable Item;
    public WeaponTable Weapon;
    public RecipeTable Recipe;
    public StructureTable Structure;
    public ResourceNodeTable ResourceNode;
    public SpawnTable Spawn;
    public LootTable Loot;
    public BloodTypeTable BloodType;
    public ShopTable Shop;
    public CueTable Cue;
    public InputTable Input;
    public AnimationTable Animation;

    public TableAssetBase[] GetAll()
    {
        return new TableAssetBase[]
        {
            Const, Tag, String, Asset, Character, Ability, Effect, Item, Weapon, Recipe,
            Structure, ResourceNode, Spawn, Loot, BloodType, Shop, Cue, Input, Animation,
        };
    }

    public void OnBind()
    {
        TableAssetBase[] tables = GetAll();
        foreach (TableAssetBase table in tables)
        {
            table.OnBind();
        }
    }
}

/// <summary>
/// 테이블 정적 접근점. 부트스트랩이 GameTables 를 로드한 뒤 Bind 한다.
/// </summary>
public static class Tables
{
    public static GameTables Root { get; private set; }

    /// <summary>InRoot 가 null 이면 바인딩만 해제한다 (부트스트랩 OnDestroy).</summary>
    public static void Bind(GameTables InRoot)
    {
        Root = InRoot;
        if (Root != null)
        {
            Root.OnBind();
        }
    }

    public static bool IsReady
    {
        get { return Root != null; }
    }

    public static float Const(EConst InConst)
    {
        ConstRow row;
        if (Root.Const.TryGet((int)InConst, out row) == false)
        {
            Log.Warning($"Const {InConst} missing");
            return 0.0f;
        }

        return row.Value;
    }

    public static int ConstInt(EConst InConst)
    {
        return Mathf.RoundToInt(Const(InConst));
    }

    public static string Text(int InId)
    {
        StringRow row;
        if (Root == null || Root.String.TryGet(InId, out row) == false)
        {
            return $"#{InId}";
        }

        return row.Text;
    }

    public static string Text(EText InText)
    {
        return Text((int)InText);
    }

    /// <summary>AnimationTable 행. 0 이거나 없으면 null.</summary>
    public static AnimationRow Animation(int InAnimId)
    {
        AnimationRow row;
        if (InAnimId == 0 || Root == null || Root.Animation.TryGet(InAnimId, out row) == false)
        {
            return null;
        }

        return row;
    }

    public static string Address(int InAssetId)
    {
        AssetRow row;
        if (InAssetId == 0 || Root.Asset.TryGet(InAssetId, out row) == false)
        {
            return null;
        }

        return row.Address;
    }
}
