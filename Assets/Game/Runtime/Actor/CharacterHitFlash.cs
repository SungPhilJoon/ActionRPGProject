using Framework;
using UnityEngine;

/// <summary>
/// 피격 플래시: 이 액터가 피해를 받으면 Game/Character 셰이더의 _HitFlash 를 잠깐 올렸다 내린다 (MaterialPropertyBlock).
/// 플래시가 끝나면 블록을 지워 SRP Batcher 배칭으로 돌아간다. Actor.Initialize 가 붙인다.
/// </summary>
public class CharacterHitFlash : BaseBehaviour
{
    private static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");
    private static readonly Color FlashColor = new Color(1.0f, 0.92f, 0.86f, 1.0f);
    private static readonly Color PlayerFlashColor = new Color(1.0f, 0.25f, 0.2f, 1.0f);
    private const float Duration = 0.14f;
    private const float Peak = 0.75f;

    private Actor Owner;
    private Renderer[] Renderers;
    private MaterialPropertyBlock Block;
    private float Remaining;

    public void Setup(Actor InOwner)
    {
        Owner = InOwner;
        Renderers = GetComponentsInChildren<Renderer>(true);
        Block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        EventBus<DamageEvent>.Subscribe(onDamage);
    }

    private void OnDisable()
    {
        EventBus<DamageEvent>.Unsubscribe(onDamage);
        if (Remaining > 0.0f)
        {
            Remaining = 0.0f;
            apply(0.0f);
        }
    }

    private void onDamage(DamageEvent InEvent)
    {
        if (Owner == null || InEvent.Target != Owner || InEvent.Amount + InEvent.Absorbed < 0.5f)
        {
            return;
        }

        Remaining = Duration;
    }

    private void LateUpdate()
    {
        if (Remaining <= 0.0f)
        {
            return;
        }

        Remaining -= Time.deltaTime;
        apply(Remaining > 0.0f ? Peak * (Remaining / Duration) : 0.0f);
    }

    private void apply(float InStrength)
    {
        if (Renderers == null)
        {
            return;
        }

        var color = Owner != null && Owner.IsPlayer == true ? PlayerFlashColor : FlashColor;
        color.a = InStrength;
        for (int i = 0; i < Renderers.Length; ++i)
        {
            var renderer = Renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer == true)
            {
                continue;
            }

            if (InStrength <= 0.0f)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }

            Block.Clear();
            Block.SetColor(HitFlashId, color);
            renderer.SetPropertyBlock(Block);
        }
    }
}
