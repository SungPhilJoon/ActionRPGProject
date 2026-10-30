using Framework;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 씬에 배치된 오브젝트 참조 모음. 런타임 코드는 이름 검색(GameObject.Find) 대신 이 직렬화 참조를 쓴다.
/// 에디터 WorldBuilder 가 씬을 만들 때 연결해 준다.
/// </summary>
public class SceneContext : BaseBehaviour
{
    public Light Sun;
    public Volume DayVolume;
    public Volume NightVolume;
    public TopDownCamera Camera;
    public Transform PlayerStart;
    public Terrain Terrain;
    public FoliageRoot Foliage;

    public static SceneContext Current { get; private set; }

    protected override void OnUnityAwake()
    {
        base.OnUnityAwake();
        Current = this;
    }

    protected override void OnUnityDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }

        base.OnUnityDestroy();
    }
}
