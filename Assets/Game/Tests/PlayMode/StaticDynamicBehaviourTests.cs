using System.Collections;
using System.Collections.Generic;
using Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

/// <summary>
/// Framework StaticBehaviour / DynamicBehaviour 검증 (월드 씬 없이 큐브로).
///  - Static: 런타임에 생성해도 그 프레임에 정적 배치로 합쳐진다. 그 아래 Dynamic 자식은 빠진다.
///  - Dynamic: Show/Hide 는 렌더러 · 콜라이더만 끄고 GameObject 는 켜 둔다. SetTint 는 MaterialPropertyBlock 없이 공유 머티리얼 변형을 쓴다.
/// </summary>
public class StaticDynamicBehaviourTests
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly List<GameObject> Created = new List<GameObject>();

    [TearDown]
    public void CleanUp()
    {
        foreach (var go in Created)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }

        Created.Clear();
    }

    [Test]
    public void SrpBatcher_IsEnabled()
    {
        Assert.IsNotNull(GraphicsSettings.currentRenderPipeline, "URP 렌더 파이프라인이 설정되어 있어야 함");
        Assert.IsTrue(GraphicsSettings.useScriptableRenderPipelineBatching, "SRP Batcher 가 켜져 있어야 함 (URP Asset → SRP Batcher)");
    }

    [UnityTest]
    public IEnumerator StaticBehaviour_SpawnedAtRuntime_IsStaticBatchedSameFrame()
    {
        var root = makeRoot("StaticRoot", 4);
        var behaviour = root.AddComponent<StaticBehaviour>();
        Assert.IsFalse(behaviour.IsBatched, "LateUpdate 전에는 아직 합쳐지지 않음");

        yield return null;

        Assert.IsTrue(behaviour.IsBatched, "생성된 프레임 LateUpdate 에 합쳐져야 함");
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            Assert.IsTrue(renderer.isPartOfStaticBatch, $"{renderer.name} 가 정적 배치에 들어가야 함");
        }

        Assert.AreEqual(0, StaticBatching.PendingCount);
    }

    [UnityTest]
    public IEnumerator StaticBehaviour_DynamicChild_IsNotBatched()
    {
        var root = makeRoot("StaticWithDynamicChild", 2);
        var dynamicChild = makeCube("DynamicChild", root.transform, new Vector3(0.0f, 2.0f, 0.0f));
        dynamicChild.AddComponent<DynamicBehaviour>();
        root.AddComponent<StaticBehaviour>();

        yield return null;

        Assert.IsTrue(root.transform.GetChild(0).GetComponent<MeshRenderer>().isPartOfStaticBatch, "정적 부모의 큐브는 합쳐져야 함");
        Assert.IsFalse(dynamicChild.GetComponent<MeshRenderer>().isPartOfStaticBatch, "DynamicBehaviour 자식은 정적 배치에서 빠져야 함");
    }

    [UnityTest]
    public IEnumerator DynamicBehaviour_HideShow_TogglesRenderersAndCollidersOnly()
    {
        var cube = makeCube("Dynamic", null, Vector3.zero);
        var behaviour = cube.AddComponent<DynamicBehaviour>();
        var renderer = cube.GetComponent<MeshRenderer>();
        var collider = cube.GetComponent<Collider>();
        yield return null;

        behaviour.Hide();
        Assert.IsFalse(behaviour.IsShown);
        Assert.IsFalse(renderer.enabled, "숨기면 렌더러가 꺼져야 함");
        Assert.IsFalse(collider.enabled, "ToggleColliders 면 콜라이더도 꺼져야 함");
        Assert.IsTrue(cube.activeSelf, "GameObject 는 켜진 채로 (SetActive 를 쓰지 않음)");

        behaviour.OnSpawn();
        Assert.IsTrue(behaviour.IsShown);
        Assert.IsTrue(renderer.enabled);
        Assert.IsTrue(collider.enabled);

        behaviour.OnDespawn();
        Assert.IsFalse(renderer.enabled, "풀에 넣으면(OnDespawn) 숨겨져야 함");
    }

    [UnityTest]
    public IEnumerator DynamicBehaviour_SetTint_SharesMaterialWithoutPropertyBlock()
    {
        var a = makeCube("TintA", null, Vector3.zero).AddComponent<DynamicBehaviour>();
        var b = makeCube("TintB", null, Vector3.right * 2.0f).AddComponent<DynamicBehaviour>();
        var rendererA = a.GetComponent<MeshRenderer>();
        var rendererB = b.GetComponent<MeshRenderer>();
        var baseMaterial = rendererA.sharedMaterial;
        if (baseMaterial.HasProperty(BaseColorId) == false)
        {
            Assert.Ignore("기본 머티리얼에 _BaseColor 가 없습니다 (URP Lit 가 아님)");
        }

        Color baseColor = baseMaterial.GetColor(BaseColorId);
        yield return null;

        a.SetTint(Color.red);
        b.SetTint(Color.red);
        Assert.AreNotSame(baseMaterial, rendererA.sharedMaterial, "틴트는 원본이 아닌 변형 머티리얼");
        Assert.AreSame(rendererA.sharedMaterial, rendererB.sharedMaterial, "같은 (원본, 색) 은 한 머티리얼을 공유해야 함");
        Assert.AreSame(baseMaterial.shader, rendererA.sharedMaterial.shader, "같은 셰이더 → 같은 SRP 배치");
        Assert.AreEqual(Color.red, rendererA.sharedMaterial.GetColor(BaseColorId));
        Assert.AreEqual(baseColor, baseMaterial.GetColor(BaseColorId), "원본 머티리얼은 바뀌지 않아야 함");
        Assert.IsFalse(rendererA.HasPropertyBlock(), "MaterialPropertyBlock 을 쓰면 SRP Batcher 에서 빠진다");

        b.SetTint(Color.blue);
        Assert.AreNotSame(rendererA.sharedMaterial, rendererB.sharedMaterial, "다른 색은 다른 변형");

        a.ClearTint();
        Assert.AreSame(baseMaterial, rendererA.sharedMaterial, "ClearTint 는 원본으로");
        Assert.IsFalse(a.Tint.HasValue);
    }

    // ------------------------------------------------------------------ helpers

    private GameObject makeRoot(string InName, int InCubes)
    {
        var root = new GameObject(InName);
        root.transform.position = new Vector3(0.0f, -500.0f, 0.0f);
        Created.Add(root);
        for (int i = 0; i < InCubes; ++i)
        {
            makeCube($"Cube{i}", root.transform, new Vector3(i * 1.5f, 0.0f, 0.0f));
        }

        return root;
    }

    private GameObject makeCube(string InName, Transform InParent, Vector3 InLocalPosition)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = InName;
        if (InParent != null)
        {
            cube.transform.SetParent(InParent, false);
        }
        else
        {
            cube.transform.position = new Vector3(0.0f, -500.0f, 0.0f);
            Created.Add(cube);
        }

        cube.transform.localPosition += InLocalPosition;
        return cube;
    }
}
