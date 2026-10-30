using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MonsterAuthoring 인스펙터의 3D 미리보기. PreviewRenderUtility 의 별도 프리뷰 씬에 모델을 띄우고
/// 선택한 클립을 AnimationClip.SampleAnimation 으로 샘플링한다 (씬/에셋은 건드리지 않음).
/// 드래그로 회전, 휠로 확대/축소.
/// </summary>
public class MonsterPreview : IDisposable
{
    private PreviewRenderUtility Utility;
    private GameObject Source;
    private GameObject Instance;
    private GameObject AnimatorRoot;
    private Vector2 Orbit = new Vector2(150.0f, -12.0f);
    private float Zoom = 1.0f;

    public void SetModel(GameObject InModel, float InScale)
    {
        if (Source != InModel)
        {
            destroyInstance();
            Source = InModel;
            if (InModel != null)
            {
                ensureUtility();
                Instance = Utility.InstantiatePrefabInScene(InModel);
                var animator = Instance.GetComponentInChildren<Animator>(true);
                AnimatorRoot = animator != null ? animator.gameObject : Instance;
            }
        }

        if (Instance != null)
        {
            Instance.transform.localScale = Vector3.one * InScale;
        }
    }

    public void Sample(AnimationClip InClip, float InTime)
    {
        if (InClip != null && AnimatorRoot != null)
        {
            InClip.SampleAnimation(AnimatorRoot, InTime);
        }
    }

    /// <summary>InRadius 가 0 보다 크면 발밑에 판정 반지름 원을 그린다.</summary>
    public void Draw(Rect InRect, float InRadius)
    {
        if (Instance == null)
        {
            EditorGUI.HelpBox(InRect, "모델이 없습니다.", MessageType.Info);
            return;
        }

        handleInput(InRect);
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        var bounds = computeBounds();
        float size = Mathf.Max(bounds.extents.magnitude, 0.5f);
        var camera = Utility.camera;
        camera.fieldOfView = 30.0f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = size * 20.0f;
        var rotation = Quaternion.Euler(-Orbit.y, Orbit.x, 0.0f);
        float distance = size * 3.2f * Zoom;
        camera.transform.position = bounds.center + rotation * new Vector3(0.0f, 0.0f, -distance);
        camera.transform.rotation = rotation;

        Utility.lights[0].intensity = 1.2f;
        Utility.lights[0].transform.rotation = Quaternion.Euler(40.0f, 140.0f, 0.0f);
        Utility.lights[1].intensity = 0.6f;
        Utility.ambientColor = new Color(0.25f, 0.25f, 0.28f);

        Utility.BeginPreview(InRect, GUIStyle.none);
        Utility.Render(true);
        Utility.EndAndDrawPreview(InRect);

        if (InRadius > 0.0f)
        {
            drawRadius(InRect, camera, InRadius);
        }
    }

    public void Dispose()
    {
        destroyInstance();
        if (Utility != null)
        {
            Utility.Cleanup();
            Utility = null;
        }
    }

    // ------------------------------------------------------------------ internal

    private void ensureUtility()
    {
        if (Utility == null)
        {
            Utility = new PreviewRenderUtility();
            Utility.camera.clearFlags = CameraClearFlags.SolidColor;
            Utility.camera.backgroundColor = new Color(0.17f, 0.17f, 0.19f);
        }
    }

    private void destroyInstance()
    {
        if (Instance != null)
        {
            UnityEngine.Object.DestroyImmediate(Instance);
        }

        Instance = null;
        AnimatorRoot = null;
        Source = null;
    }

    private Bounds computeBounds()
    {
        var renderers = Instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(Instance.transform.position + Vector3.up, Vector3.one * 2.0f);
        }

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; ++i)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private void handleInput(Rect InRect)
    {
        var current = Event.current;
        if (InRect.Contains(current.mousePosition) == false)
        {
            return;
        }

        if (current.type == EventType.MouseDrag && current.button == 0)
        {
            Orbit.x += current.delta.x * 0.6f;
            Orbit.y = Mathf.Clamp(Orbit.y - current.delta.y * 0.6f, -80.0f, 80.0f);
            current.Use();
            GUI.changed = true;
        }
        else if (current.type == EventType.ScrollWheel)
        {
            Zoom = Mathf.Clamp(Zoom * (1.0f + current.delta.y * 0.05f), 0.3f, 4.0f);
            current.Use();
            GUI.changed = true;
        }
    }

    private void drawRadius(Rect InRect, Camera InCamera, float InRadius)
    {
        const int segments = 40;
        var center = Instance.transform.position;
        Handles.BeginGUI();
        Handles.color = new Color(1.0f, 0.4f, 0.3f, 0.9f);
        Vector2 previous = Vector2.zero;
        for (int i = 0; i <= segments; ++i)
        {
            float angle = i * Mathf.PI * 2.0f / segments;
            var world = center + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * InRadius;
            var viewport = InCamera.WorldToViewportPoint(world);
            var point = new Vector2(InRect.x + viewport.x * InRect.width, InRect.y + (1.0f - viewport.y) * InRect.height);
            if (i > 0)
            {
                Handles.DrawLine(previous, point);
            }

            previous = point;
        }

        Handles.EndGUI();
    }
}
