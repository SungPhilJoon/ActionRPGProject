using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    /// <summary>
    /// SRP Batcher 친화적인 머티리얼 변형 캐시 (Flyweight).
    /// MaterialPropertyBlock 은 렌더러를 SRP Batcher 에서 빼 버리므로, 색만 다른 머티리얼은 원본을 복제한 공유 머티리얼로 만든다.
    ///  - 키 = (원본 머티리얼, 셰이더 속성 id, 8비트 RGBA). 같은 키는 한 머티리얼을 공유한다.
    ///  - 복제본은 원본과 셰이더 · 키워드가 같아 같은 셰이더 배리언트 → SRP Batcher 가 한 배치로 묶는다.
    /// 색은 채널당 8비트로 양자화된다 (HDR 색이 필요하면 별도 머티리얼 에셋을 쓴다). 씬을 완전히 바꿀 때 Clear() 로 해제한다.
    /// </summary>
    public static class SharedMaterialVariants
    {
        private readonly struct Key : IEquatable<Key>
        {
            private readonly int MaterialId;
            private readonly int PropertyId;
            private readonly int Rgba;

            public Key(int InMaterialId, int InPropertyId, int InRgba)
            {
                MaterialId = InMaterialId;
                PropertyId = InPropertyId;
                Rgba = InRgba;
            }

            public bool Equals(Key InOther)
            {
                return MaterialId == InOther.MaterialId && PropertyId == InOther.PropertyId && Rgba == InOther.Rgba;
            }

            public override bool Equals(object InOther)
            {
                return InOther is Key && Equals((Key)InOther) == true;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = MaterialId;
                    hash = hash * 397 ^ PropertyId;
                    return hash * 397 ^ Rgba;
                }
            }
        }

        private static readonly Dictionary<Key, Material> Cache = new Dictionary<Key, Material>();

        public static int Count
        {
            get { return Cache.Count; }
        }

        /// <summary>InBase 의 InPropertyId 색을 InColor 로 바꾼 공유 머티리얼. 원본이 null 이거나 그 속성이 없으면 원본을 그대로 돌려준다.</summary>
        public static Material Get(Material InBase, int InPropertyId, Color InColor)
        {
            if (InBase == null || InBase.HasProperty(InPropertyId) == false)
            {
                return InBase;
            }

            Color32 color = InColor;
            int rgba = color.r << 24 | color.g << 16 | color.b << 8 | color.a;
            var key = new Key(InBase.GetInstanceID(), InPropertyId, rgba);
            Material variant;
            if (Cache.TryGetValue(key, out variant) == true && variant != null)
            {
                return variant;
            }

            variant = new Material(InBase);
            variant.name = $"{InBase.name} (Variant {color.r:X2}{color.g:X2}{color.b:X2}{color.a:X2})";
            variant.hideFlags = HideFlags.DontSave;
            variant.SetColor(InPropertyId, color);
            Cache[key] = variant;
            return variant;
        }

        /// <summary>만든 변형을 모두 파괴한다 (아직 쓰는 렌더러가 있으면 머티리얼이 빠지므로 씬 전환 뒤에 부른다).</summary>
        public static void Clear()
        {
            foreach (var pair in Cache)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.Destroy(pair.Value);
                }
            }

            Cache.Clear();
        }
    }
}
